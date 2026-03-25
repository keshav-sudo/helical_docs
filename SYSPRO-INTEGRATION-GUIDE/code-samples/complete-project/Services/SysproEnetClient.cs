using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using SysproIntegrationApi.Configuration;

namespace SysproIntegrationApi.Services;

/// <summary>
/// Core SYSPRO e.net client for HTTP/XML communication
/// </summary>
public class SysproEnetClient
{
    private readonly HttpClient _httpClient;
    private readonly SysproSettings _settings;
    private readonly ILogger<SysproEnetClient> _logger;

    public SysproEnetClient(
        IOptions<SysproSettings> settings, 
        ILogger<SysproEnetClient> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_settings.BaseUrl),
            Timeout = TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds)
        };
    }

    /// <summary>
    /// Login to SYSPRO and get a session ID
    /// </summary>
    public async Task<string> LogonAsync(CancellationToken ct = default)
    {
        var loginXml = $@"<Logon>
            <Operator>{_settings.Operator}</Operator>
            <OperatorPassword>{_settings.Password}</OperatorPassword>
            <CompanyId>{_settings.CompanyId}</CompanyId>
            <CompanyPassword>{_settings.CompanyPassword ?? ""}</CompanyPassword>
        </Logon>";

        var response = await PostAsync("/saborw/Logon", loginXml, ct);
        
        // Response should be a GUID
        var sessionId = response.Trim();
        
        if (!Guid.TryParse(sessionId, out _))
        {
            // Check if it's an error response
            if (response.Contains("ErrorMessage"))
            {
                var error = ExtractError(response);
                throw new SysproException($"Login failed: {error}");
            }
            throw new SysproException($"Invalid session ID received: {response}");
        }
        
        _logger.LogDebug("Logged in with session {SessionId}", sessionId[..8] + "...");
        return sessionId;
    }

    /// <summary>
    /// Logout and release the session
    /// </summary>
    public async Task LogoffAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            await PostAsync("/saborw/Logoff", sessionId, ct);
            _logger.LogDebug("Logged off session {SessionId}", sessionId[..8] + "...");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during logoff (session may have expired)");
        }
    }

    /// <summary>
    /// Execute a query business object
    /// </summary>
    public async Task<string> QueryAsync(
        string sessionId, 
        string businessObject, 
        string queryXml,
        CancellationToken ct = default)
    {
        var url = $"/saborw/Query?BusinessObject={businessObject}&SessionId={sessionId}";
        var response = await PostAsync(url, queryXml, ct);
        
        ThrowIfError(response, businessObject);
        return response;
    }

    /// <summary>
    /// Execute a transaction business object
    /// </summary>
    public async Task<string> TransactionAsync(
        string sessionId, 
        string businessObject, 
        string transactionXml,
        CancellationToken ct = default)
    {
        var url = $"/saborw/Transaction?BusinessObject={businessObject}&SessionId={sessionId}";
        var response = await PostAsync(url, transactionXml, ct);
        
        ThrowIfError(response, businessObject);
        return response;
    }

    private async Task<string> PostAsync(string path, string body, CancellationToken ct)
    {
        var content = new StringContent(body, Encoding.UTF8, "application/xml");
        var response = await _httpClient.PostAsync(path, content, ct);
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private void ThrowIfError(string response, string operation)
    {
        if (response.Contains("<ErrorMessage>") || response.Contains("<ErrorNumber>"))
        {
            var error = ExtractError(response);
            throw new SysproException($"{operation} failed: {error}");
        }
    }

    private string ExtractError(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var errorMsg = doc.Descendants("ErrorMessage").FirstOrDefault()?.Value;
            var errorNum = doc.Descendants("ErrorNumber").FirstOrDefault()?.Value;
            
            return errorMsg ?? errorNum ?? "Unknown error";
        }
        catch
        {
            return xml.Length > 200 ? xml[..200] + "..." : xml;
        }
    }
}

/// <summary>
/// Custom exception for SYSPRO errors
/// </summary>
public class SysproException : Exception
{
    public SysproException(string message) : base(message) { }
    public SysproException(string message, Exception inner) : base(message, inner) { }
}
