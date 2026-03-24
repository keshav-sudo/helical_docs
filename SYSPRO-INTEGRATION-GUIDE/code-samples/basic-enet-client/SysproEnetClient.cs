using System.Net.Http;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace SysproIntegration.Services;

/// <summary>
/// Basic SYSPRO e.net client for API communication.
/// This is the foundation for all SYSPRO operations.
/// </summary>
public class SysproEnetClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<SysproEnetClient> _logger;
    private readonly string _baseUrl;

    public SysproEnetClient(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<SysproEnetClient> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _baseUrl = _config["Syspro:BaseUrl"]!.TrimEnd('/');
    }

    /// <summary>
    /// Authenticate with SYSPRO and get a session ID.
    /// The session ID is required for all subsequent operations.
    /// </summary>
    public async Task<SysproSession> LogonAsync(
        string? operatorCode = null,
        string? password = null,
        string? companyId = null)
    {
        var op = operatorCode ?? _config["Syspro:DefaultOperator"]!;
        var pwd = password ?? _config["Syspro:DefaultPassword"]!;
        var company = companyId ?? _config["Syspro:CompanyId"]!;

        _logger.LogInformation("Logging into SYSPRO as {Operator} for company {Company}", op, company);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Operator"] = op,
            ["Password"] = pwd,
            ["CompanyId"] = company,
            ["OperatorPassword"] = pwd
        });

        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/saborw/Logon", content);
            var sessionId = await response.Content.ReadAsStringAsync();

            // Check for error response
            if (sessionId.Contains("<Error>") || sessionId.Contains("Invalid"))
            {
                throw new SysproAuthenticationException($"Login failed: {sessionId}");
            }

            _logger.LogInformation("Successfully logged into SYSPRO. SessionId: {SessionStart}...",
                sessionId.Substring(0, Math.Min(8, sessionId.Length)));

            return new SysproSession
            {
                SessionId = sessionId.Trim(),
                CreatedAt = DateTime.UtcNow,
                Operator = op,
                CompanyId = company
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to SYSPRO at {Url}", _baseUrl);
            throw new SysproConnectionException($"Cannot reach SYSPRO at {_baseUrl}", ex);
        }
    }

    /// <summary>
    /// Query data from SYSPRO (READ operations).
    /// Returns raw XML response.
    /// </summary>
    public async Task<string> QueryAsync(
        string sessionId,
        string businessObject,
        string xmlParameters)
    {
        _logger.LogDebug("Querying SYSPRO BO: {BusinessObject}", businessObject);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = sessionId,
            ["BusinessObject"] = businessObject,
            ["XmlIn"] = xmlParameters
        });

        var response = await _httpClient.PostAsync($"{_baseUrl}/saborw/Query", content);
        var result = await response.Content.ReadAsStringAsync();

        // Check for errors in response
        if (result.Contains("<ErrorDescription>"))
        {
            var error = ExtractError(result);
            _logger.LogWarning("SYSPRO query error: {Error}", error);
            throw new SysproBusinessException(error);
        }

        return result;
    }

    /// <summary>
    /// Execute a transaction in SYSPRO (CREATE/UPDATE/DELETE operations).
    /// Returns raw XML response.
    /// </summary>
    public async Task<string> TransactionAsync(
        string sessionId,
        string businessObject,
        string xmlParameters,
        string xmlDocument)
    {
        _logger.LogDebug("Executing SYSPRO transaction: {BusinessObject}", businessObject);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = sessionId,
            ["BusinessObject"] = businessObject,
            ["XmlParameters"] = xmlParameters,
            ["XmlIn"] = xmlDocument
        });

        var response = await _httpClient.PostAsync($"{_baseUrl}/saborw/Transaction", content);
        var result = await response.Content.ReadAsStringAsync();

        // Check for errors
        if (result.Contains("<ErrorDescription>"))
        {
            var error = ExtractError(result);
            _logger.LogWarning("SYSPRO transaction error: {Error}", error);
            throw new SysproBusinessException(error);
        }

        return result;
    }

    /// <summary>
    /// Release the SYSPRO session. Always call this to free up licenses.
    /// </summary>
    public async Task LogoffAsync(string sessionId)
    {
        _logger.LogDebug("Logging off from SYSPRO session");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = sessionId
        });

        try
        {
            await _httpClient.PostAsync($"{_baseUrl}/saborw/Logoff", content);
            _logger.LogInformation("Successfully logged off from SYSPRO");
        }
        catch (Exception ex)
        {
            // Log but don't throw - session will expire anyway
            _logger.LogWarning(ex, "Error during SYSPRO logoff (session will expire automatically)");
        }
    }

    private static string ExtractError(string xmlResponse)
    {
        try
        {
            var doc = XDocument.Parse(xmlResponse);
            var errorDesc = doc.Descendants("ErrorDescription").FirstOrDefault()?.Value;
            return errorDesc ?? "Unknown SYSPRO error";
        }
        catch
        {
            return xmlResponse;
        }
    }
}

/// <summary>
/// Represents an active SYSPRO session
/// </summary>
public class SysproSession
{
    public string SessionId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string Operator { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;

    public bool IsExpired(TimeSpan maxAge) => DateTime.UtcNow - LastUsed > maxAge;
}

// Custom exceptions for better error handling
public class SysproConnectionException : Exception
{
    public SysproConnectionException(string message, Exception? inner = null) 
        : base(message, inner) { }
}

public class SysproAuthenticationException : Exception
{
    public SysproAuthenticationException(string message) : base(message) { }
}

public class SysproBusinessException : Exception
{
    public SysproBusinessException(string message) : base(message) { }
}
