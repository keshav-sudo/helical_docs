using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace SysproIntegration.Services;

/// <summary>
/// Manages a pool of SYSPRO sessions to:
/// 1. Reduce login overhead (login takes 500-2000ms)
/// 2. Stay within SYSPRO license limits
/// 3. Handle concurrent requests efficiently
/// </summary>
public class SysproSessionPool : IDisposable
{
    private readonly SysproEnetClient _client;
    private readonly ILogger<SysproSessionPool> _logger;
    private readonly IConfiguration _config;

    // Pool storage
    private readonly ConcurrentBag<SysproSession> _availableSessions;
    private readonly ConcurrentDictionary<string, SysproSession> _inUseSessions;

    // Configuration
    private readonly int _maxPoolSize;
    private readonly int _minPoolSize;
    private readonly TimeSpan _sessionMaxAge;
    private readonly TimeSpan _sessionIdleTimeout;

    // Synchronization
    private readonly SemaphoreSlim _poolLock;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public SysproSessionPool(
        SysproEnetClient client,
        ILogger<SysproSessionPool> logger,
        IConfiguration config)
    {
        _client = client;
        _logger = logger;
        _config = config;

        _availableSessions = new ConcurrentBag<SysproSession>();
        _inUseSessions = new ConcurrentDictionary<string, SysproSession>();

        // Read configuration with defaults
        _maxPoolSize = config.GetValue("Syspro:SessionPool:MaxSize", 10);
        _minPoolSize = config.GetValue("Syspro:SessionPool:MinSize", 2);
        _sessionMaxAge = TimeSpan.FromMinutes(config.GetValue("Syspro:SessionPool:MaxAgeMinutes", 30));
        _sessionIdleTimeout = TimeSpan.FromMinutes(config.GetValue("Syspro:SessionPool:IdleTimeoutMinutes", 10));

        _poolLock = new SemaphoreSlim(1, 1);

        // Run cleanup every 5 minutes
        _cleanupTimer = new Timer(CleanupExpiredSessions, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _logger.LogInformation(
            "Session pool initialized. MaxSize={MaxSize}, MinSize={MinSize}, MaxAge={MaxAge}min",
            _maxPoolSize, _minPoolSize, _sessionMaxAge.TotalMinutes);
    }

    /// <summary>
    /// Get a session from the pool. Creates new one if pool is empty.
    /// </summary>
    public async Task<PooledSession> AcquireSessionAsync(CancellationToken cancellationToken = default)
    {
        // Try to get from pool first
        while (_availableSessions.TryTake(out var session))
        {
            // Skip expired sessions
            if (session.IsExpired(_sessionMaxAge))
            {
                _logger.LogDebug("Session expired, discarding");
                await SafeLogoffAsync(session);
                continue;
            }

            // Found a valid session
            session.LastUsed = DateTime.UtcNow;
            _inUseSessions[session.SessionId] = session;
            _logger.LogDebug("Acquired session from pool. Pool size: {Available}", _availableSessions.Count);
            return new PooledSession(session, this);
        }

        // Pool is empty - create new session if under limit
        var totalSessions = _availableSessions.Count + _inUseSessions.Count;
        if (totalSessions >= _maxPoolSize)
        {
            _logger.LogWarning("Session pool exhausted! Max size: {MaxSize}", _maxPoolSize);
            throw new SysproPoolExhaustedException(
                $"No sessions available. Pool max size: {_maxPoolSize}. Consider increasing pool size or reducing concurrent requests.");
        }

        // Create new session
        _logger.LogInformation("Creating new SYSPRO session (pool was empty)");
        var newSession = await _client.LogonAsync();
        _inUseSessions[newSession.SessionId] = newSession;

        return new PooledSession(newSession, this);
    }

    /// <summary>
    /// Return a session to the pool for reuse.
    /// </summary>
    public void ReleaseSession(SysproSession session)
    {
        _inUseSessions.TryRemove(session.SessionId, out _);

        // Check if session is still valid
        if (session.IsExpired(_sessionMaxAge))
        {
            _logger.LogDebug("Session expired during use, not returning to pool");
            _ = SafeLogoffAsync(session);
            return;
        }

        session.LastUsed = DateTime.UtcNow;
        _availableSessions.Add(session);
        _logger.LogDebug("Session returned to pool. Pool size: {Available}", _availableSessions.Count);
    }

    /// <summary>
    /// Pre-warm the pool with minimum number of sessions
    /// </summary>
    public async Task WarmupAsync()
    {
        _logger.LogInformation("Warming up session pool to {MinSize} sessions", _minPoolSize);

        var tasks = Enumerable.Range(0, _minPoolSize)
            .Select(async _ =>
            {
                try
                {
                    var session = await _client.LogonAsync();
                    _availableSessions.Add(session);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create warmup session");
                }
            });

        await Task.WhenAll(tasks);
        _logger.LogInformation("Pool warmed up. Available sessions: {Count}", _availableSessions.Count);
    }

    private void CleanupExpiredSessions(object? state)
    {
        try
        {
            var expiredCount = 0;
            var newBag = new ConcurrentBag<SysproSession>();

            while (_availableSessions.TryTake(out var session))
            {
                if (session.IsExpired(_sessionIdleTimeout))
                {
                    _ = SafeLogoffAsync(session);
                    expiredCount++;
                }
                else
                {
                    newBag.Add(session);
                }
            }

            // Re-add valid sessions
            while (newBag.TryTake(out var session))
            {
                _availableSessions.Add(session);
            }

            if (expiredCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired sessions", expiredCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session cleanup");
        }
    }

    private async Task SafeLogoffAsync(SysproSession session)
    {
        try
        {
            await _client.LogoffAsync(session.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during session logoff");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cleanupTimer.Dispose();
        _poolLock.Dispose();

        // Logoff all sessions
        foreach (var session in _availableSessions)
        {
            _ = SafeLogoffAsync(session);
        }
        foreach (var session in _inUseSessions.Values)
        {
            _ = SafeLogoffAsync(session);
        }
    }

    // Public stats for monitoring
    public int AvailableCount => _availableSessions.Count;
    public int InUseCount => _inUseSessions.Count;
    public int TotalCount => AvailableCount + InUseCount;
}

/// <summary>
/// Wrapper that automatically returns session to pool when disposed
/// </summary>
public class PooledSession : IDisposable
{
    private readonly SysproSessionPool _pool;
    private bool _disposed;

    public SysproSession Session { get; }
    public string SessionId => Session.SessionId;

    internal PooledSession(SysproSession session, SysproSessionPool pool)
    {
        Session = session;
        _pool = pool;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pool.ReleaseSession(Session);
    }
}

public class SysproPoolExhaustedException : Exception
{
    public SysproPoolExhaustedException(string message) : base(message) { }
}
