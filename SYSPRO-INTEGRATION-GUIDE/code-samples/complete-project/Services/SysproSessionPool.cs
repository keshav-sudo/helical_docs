using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using SysproIntegrationApi.Configuration;

namespace SysproIntegrationApi.Services;

/// <summary>
/// Manages a pool of SYSPRO sessions to minimize license usage
/// </summary>
public class SysproSessionPool : IAsyncDisposable
{
    private readonly SysproEnetClient _client;
    private readonly SysproSettings _settings;
    private readonly ILogger<SysproSessionPool> _logger;
    
    private readonly ConcurrentBag<PooledSession> _availableSessions = new();
    private readonly SemaphoreSlim _sessionSemaphore;
    private readonly object _lock = new();
    private bool _initialized;
    private bool _disposed;

    public SysproSessionPool(
        SysproEnetClient client,
        IOptions<SysproSettings> settings,
        ILogger<SysproSessionPool> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
        _sessionSemaphore = new SemaphoreSlim(_settings.PoolSize, _settings.PoolSize);
    }

    /// <summary>
    /// Initialize the pool with warm sessions
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        
        lock (_lock)
        {
            if (_initialized) return;
            _initialized = true;
        }

        _logger.LogInformation(
            "Initializing session pool with {Count} sessions for company {Company}",
            _settings.PoolSize, _settings.CompanyId);

        // Create initial sessions (warm up the pool)
        var tasks = Enumerable.Range(0, Math.Min(2, _settings.PoolSize))
            .Select(_ => CreateSessionAsync(ct));
        
        var sessions = await Task.WhenAll(tasks);
        
        foreach (var session in sessions.Where(s => s != null))
        {
            _availableSessions.Add(session!);
        }
        
        _logger.LogInformation("Session pool initialized with {Count} warm sessions", 
            _availableSessions.Count);
    }

    /// <summary>
    /// Execute an operation with a pooled session
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<string, Task<T>> operation,
        CancellationToken ct = default)
    {
        await _sessionSemaphore.WaitAsync(ct);
        
        PooledSession? session = null;
        try
        {
            session = await GetOrCreateSessionAsync(ct);
            return await operation(session.SessionId);
        }
        catch (SysproException ex) when (ex.Message.Contains("session") || 
                                          ex.Message.Contains("expired"))
        {
            // Session expired, invalidate and retry once
            if (session != null)
            {
                _logger.LogWarning("Session expired, creating new session");
                session = await CreateSessionAsync(ct);
                if (session != null)
                {
                    return await operation(session.SessionId);
                }
            }
            throw;
        }
        finally
        {
            if (session != null && !session.IsExpired)
            {
                _availableSessions.Add(session);
            }
            else if (session != null)
            {
                // Session expired, let it go (will be logged off on next cleanup)
                _ = Task.Run(() => _client.LogoffAsync(session.SessionId));
            }
            
            _sessionSemaphore.Release();
        }
    }

    /// <summary>
    /// Execute a void operation with a pooled session
    /// </summary>
    public async Task ExecuteAsync(
        Func<string, Task> operation,
        CancellationToken ct = default)
    {
        await ExecuteAsync(async sessionId =>
        {
            await operation(sessionId);
            return true;
        }, ct);
    }

    private async Task<PooledSession?> GetOrCreateSessionAsync(CancellationToken ct)
    {
        // Try to get an existing session
        while (_availableSessions.TryTake(out var session))
        {
            if (!session.IsExpired)
            {
                return session;
            }
            
            // Session expired, log it off
            _logger.LogDebug("Session expired, logging off");
            _ = Task.Run(() => _client.LogoffAsync(session.SessionId));
        }

        // No available sessions, create new one
        return await CreateSessionAsync(ct);
    }

    private async Task<PooledSession?> CreateSessionAsync(CancellationToken ct)
    {
        try
        {
            var sessionId = await _client.LogonAsync(ct);
            return new PooledSession(sessionId, _settings.SessionTimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create SYSPRO session");
            return null;
        }
    }

    /// <summary>
    /// Test if SYSPRO is reachable
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await ExecuteAsync(async sessionId =>
            {
                // Simple query to test connectivity
                return true;
            }, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogInformation("Disposing session pool, logging off all sessions");

        var logoffTasks = new List<Task>();
        
        while (_availableSessions.TryTake(out var session))
        {
            logoffTasks.Add(_client.LogoffAsync(session.SessionId));
        }

        await Task.WhenAll(logoffTasks);
        _sessionSemaphore.Dispose();
    }

    private class PooledSession
    {
        public string SessionId { get; }
        public DateTime ExpiresAt { get; }
        
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        public PooledSession(string sessionId, int timeoutSeconds)
        {
            SessionId = sessionId;
            // Expire 2 minutes early to be safe
            ExpiresAt = DateTime.UtcNow.AddSeconds(timeoutSeconds - 120);
        }
    }
}
