# Part 4: .NET Implementation (Step-by-Step)

[← Back to Main Guide](../README.md) | [Previous: e.net Solutions](./03-ENET-SOLUTIONS.md) | [Next: Real Project →](./05-REAL-PROJECT.md)

---

## 4.1 Project Setup

### Create .NET 8 Web API Project

```bash
# Create solution
dotnet new sln -n SysproIntegration

# Create projects (Clean Architecture)
dotnet new webapi -n SysproIntegration.Api -o src/SysproIntegration.Api
dotnet new classlib -n SysproIntegration.Core -o src/SysproIntegration.Core
dotnet new classlib -n SysproIntegration.Infrastructure -o src/SysproIntegration.Infrastructure
dotnet new xunit -n SysproIntegration.Tests -o tests/SysproIntegration.Tests

# Add to solution
dotnet sln add src/SysproIntegration.Api
dotnet sln add src/SysproIntegration.Core
dotnet sln add src/SysproIntegration.Infrastructure
dotnet sln add tests/SysproIntegration.Tests

# Add references
dotnet add src/SysproIntegration.Api reference src/SysproIntegration.Core
dotnet add src/SysproIntegration.Api reference src/SysproIntegration.Infrastructure
dotnet add src/SysproIntegration.Infrastructure reference src/SysproIntegration.Core
dotnet add tests/SysproIntegration.Tests reference src/SysproIntegration.Core
dotnet add tests/SysproIntegration.Tests reference src/SysproIntegration.Infrastructure

# Add packages
cd src/SysproIntegration.Api
dotnet add package Serilog.AspNetCore
dotnet add package Swashbuckle.AspNetCore

cd ../SysproIntegration.Infrastructure
dotnet add package Polly
dotnet add package Polly.Extensions.Http
dotnet add package Microsoft.Data.SqlClient
dotnet add package Microsoft.Extensions.Http.Polly
```

### Folder Structure

```
SysproIntegration/
├── src/
│   ├── SysproIntegration.Api/                      # Web API layer
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs
│   │   │   ├── SalesOrderController.cs
│   │   │   ├── InventoryController.cs
│   │   │   └── CustomerController.cs
│   │   ├── Middleware/
│   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   └── RequestLoggingMiddleware.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   ├── SysproIntegration.Core/                     # Domain layer (no dependencies)
│   │   ├── Entities/
│   │   │   ├── SalesOrder.cs
│   │   │   ├── SalesOrderLine.cs
│   │   │   ├── Customer.cs
│   │   │   └── InventoryItem.cs
│   │   ├── Interfaces/
│   │   │   ├── ISysproSessionManager.cs
│   │   │   ├── ISalesOrderService.cs
│   │   │   ├── IInventoryService.cs
│   │   │   └── ICustomerService.cs
│   │   ├── DTOs/
│   │   │   ├── CreateSalesOrderRequest.cs
│   │   │   ├── SalesOrderResponse.cs
│   │   │   ├── InventoryQueryRequest.cs
│   │   │   ├── InventoryResponse.cs
│   │   │   ├── CreateCustomerRequest.cs
│   │   │   └── CustomerResponse.cs
│   │   └── Exceptions/
│   │       ├── SysproException.cs
│   │       ├── SysproSessionException.cs
│   │       └── SysproValidationException.cs
│   │
│   └── SysproIntegration.Infrastructure/           # Implementation layer
│       ├── Syspro/
│       │   ├── SysproSessionManager.cs             # Session pool management
│       │   ├── SysproEnetClient.cs                 # Low-level e.net wrapper
│       │   ├── XmlBuilders/
│       │   │   ├── SalesOrderXmlBuilder.cs
│       │   │   ├── CustomerXmlBuilder.cs
│       │   │   └── InventoryXmlBuilder.cs
│       │   └── XmlParsers/
│       │       ├── SalesOrderXmlParser.cs
│       │       ├── CustomerXmlParser.cs
│       │       └── InventoryXmlParser.cs
│       ├── Services/
│       │   ├── SalesOrderService.cs
│       │   ├── InventoryService.cs
│       │   └── CustomerService.cs
│       ├── Configuration/
│       │   └── SysproSettings.cs
│       └── Resilience/
│           └── ResiliencePolicies.cs
│
└── tests/
    └── SysproIntegration.Tests/
        ├── Services/
        └── XmlBuilders/
```

---

## 4.2 Configuration

### appsettings.json

```json
{
  "Syspro": {
    "ServerUrl": "http://syspro-server:30661",
    "WcfEndpoint": "net.tcp://syspro-server:30662/SYSPROWCFService/Logon",
    "Operator": "API_USER",
    "OperatorPassword": "USE_SECRET_MANAGER_NOT_PLAINTEXT",
    "CompanyId": "A",
    "CompanyPassword": "",
    "Language": "05",
    "DefaultWarehouse": "WH01",
    "SessionPoolSize": 5,
    "SessionTimeoutMinutes": 15,
    "MaxRetryAttempts": 3,
    "RetryDelayMs": 1000,
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerDurationSeconds": 30
  },
  "ConnectionStrings": {
    "SysproDb": "Server=syspro-sql;Database=SysproCompanyA;User Id=readonly_user;Password=USE_SECRET_MANAGER;",
    "LocalDb": "Server=local-sql;Database=SysproIntegration;Trusted_Connection=true;"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/syspro-.log", "rollingInterval": "Day" } }
    ]
  }
}
```

### SysproSettings.cs

```csharp
namespace SysproIntegration.Infrastructure.Configuration;

public class SysproSettings
{
    public const string SectionName = "Syspro";
    
    public string ServerUrl { get; set; } = string.Empty;
    public string WcfEndpoint { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string OperatorPassword { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string CompanyPassword { get; set; } = string.Empty;
    public string Language { get; set; } = "05";
    public string DefaultWarehouse { get; set; } = "WH01";
    public int SessionPoolSize { get; set; } = 5;
    public int SessionTimeoutMinutes { get; set; } = 15;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}
```

---

## 4.3 Core e.net Client

### SysproEnetClient.cs — Low-Level e.net Wrapper

```csharp
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SysproIntegration.Infrastructure.Configuration;

namespace SysproIntegration.Infrastructure.Syspro;

/// <summary>
/// Low-level wrapper around SYSPRO e.net Solutions.
/// In production, this calls SYSPRO's WCF service or COM object.
/// </summary>
public class SysproEnetClient
{
    private readonly SysproSettings _settings;
    private readonly ILogger<SysproEnetClient> _logger;
    private readonly HttpClient _httpClient;

    public SysproEnetClient(
        IOptions<SysproSettings> settings,
        ILogger<SysproEnetClient> logger,
        HttpClient httpClient)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Authenticate with SYSPRO and obtain a session ID.
    /// </summary>
    public async Task<string> LogonAsync()
    {
        var xmlLogon = $@"
        <Logon>
            <Operator>{_settings.Operator}</Operator>
            <OperatorPassword>{_settings.OperatorPassword}</OperatorPassword>
            <CompanyId>{_settings.CompanyId}</CompanyId>
            <CompanyPassword>{_settings.CompanyPassword}</CompanyPassword>
            <Language>{_settings.Language}</Language>
        </Logon>";

        _logger.LogInformation("SYSPRO Logon attempt for operator {Operator}, company {Company}",
            _settings.Operator, _settings.CompanyId);

        // In production, this calls SYSPRO's WCF endpoint:
        // var client = new SYSPROWCFServiceClient();
        // string sessionId = client.Logon(xmlLogon);
        
        var response = await CallEnetAsync("Logon", xmlLogon);
        
        // Validate — a GUID means success, anything else is an error
        if (Guid.TryParse(response.Trim(), out _))
        {
            _logger.LogInformation("SYSPRO Logon successful. SessionId={SessionId}", 
                response.Trim()[..8] + "...");
            return response.Trim();
        }

        _logger.LogError("SYSPRO Logon failed: {Response}", response);
        throw new SysproSessionException($"Login failed: {response}");
    }

    /// <summary>
    /// Release a SYSPRO session (free the license seat).
    /// </summary>
    public async Task LogoffAsync(string sessionId)
    {
        try
        {
            await CallEnetAsync("Logoff", $"<Logoff><SessionId>{sessionId}</SessionId></Logoff>");
            _logger.LogInformation("SYSPRO Logoff successful for session {SessionId}", 
                sessionId[..8] + "...");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SYSPRO Logoff failed for session {SessionId}", 
                sessionId[..8] + "...");
        }
    }

    /// <summary>
    /// Execute a Transaction business object (create/post documents).
    /// </summary>
    public async Task<string> TransactionAsync(
        string sessionId, 
        string businessObject, 
        string xmlParameters, 
        string xmlDocument)
    {
        _logger.LogInformation(
            "SYSPRO Transaction: BO={BusinessObject}, Session={SessionId}",
            businessObject, sessionId[..8] + "...");

        var xmlRequest = $@"
        <Transaction>
            <SessionId>{sessionId}</SessionId>
            <BusinessObject>{businessObject}</BusinessObject>
            <Parameters>{xmlParameters}</Parameters>
            <Document>{xmlDocument}</Document>
        </Transaction>";

        var response = await CallEnetAsync("Transaction", xmlRequest);
        ValidateResponse(response, businessObject);
        return response;
    }

    /// <summary>
    /// Execute a Query business object (read single record).
    /// </summary>
    public async Task<string> QueryAsync(
        string sessionId, 
        string businessObject, 
        string xmlFilter)
    {
        _logger.LogInformation(
            "SYSPRO Query: BO={BusinessObject}, Session={SessionId}",
            businessObject, sessionId[..8] + "...");

        var xmlRequest = $@"
        <Query>
            <SessionId>{sessionId}</SessionId>
            <BusinessObject>{businessObject}</BusinessObject>
            <Filter>{xmlFilter}</Filter>
        </Query>";

        return await CallEnetAsync("Query", xmlRequest);
    }

    /// <summary>
    /// Execute a Setup/Add (create master records like customers, stock codes).
    /// </summary>
    public async Task<string> SetupAddAsync(
        string sessionId, 
        string businessObject, 
        string xmlParameters, 
        string xmlDocument)
    {
        _logger.LogInformation(
            "SYSPRO SetupAdd: BO={BusinessObject}, Session={SessionId}",
            businessObject, sessionId[..8] + "...");

        var xmlRequest = $@"
        <SetupAdd>
            <SessionId>{sessionId}</SessionId>
            <BusinessObject>{businessObject}</BusinessObject>
            <Parameters>{xmlParameters}</Parameters>
            <Document>{xmlDocument}</Document>
        </SetupAdd>";

        var response = await CallEnetAsync("SetupAdd", xmlRequest);
        ValidateResponse(response, businessObject);
        return response;
    }

    // ─── Private helpers ─────────────────────────────────────

    private async Task<string> CallEnetAsync(string method, string xmlPayload)
    {
        // PRODUCTION: Use WCF or COM interop here
        // This is the HTTP REST-style call for SYSPRO 8+ e.net Services
        var content = new StringContent(xmlPayload, System.Text.Encoding.UTF8, "application/xml");
        var response = await _httpClient.PostAsync(
            $"{_settings.ServerUrl}/SYSPROWCFService/{method}", content);
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private void ValidateResponse(string xmlResponse, string businessObject)
    {
        var doc = XDocument.Parse(xmlResponse);
        var errorElements = doc.Descendants("ErrorDescription")
            .Where(e => !string.IsNullOrWhiteSpace(e.Value))
            .ToList();

        if (errorElements.Any())
        {
            var errors = string.Join("; ", errorElements.Select(e => e.Value));
            _logger.LogError("SYSPRO {BusinessObject} error: {Errors}", businessObject, errors);
            throw new SysproValidationException(businessObject, errors);
        }
    }
}
```

---

## 4.4 Session Pool Manager

```csharp
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SysproIntegration.Core.Interfaces;
using SysproIntegration.Infrastructure.Configuration;

namespace SysproIntegration.Infrastructure.Syspro;

/// <summary>
/// Manages a pool of SYSPRO sessions to avoid login/logout per request.
/// Each session = 1 license seat. Pool prevents license exhaustion.
/// </summary>
public class SysproSessionManager : ISysproSessionManager, IDisposable
{
    private readonly SysproEnetClient _client;
    private readonly SysproSettings _settings;
    private readonly ILogger<SysproSessionManager> _logger;
    private readonly ConcurrentBag<SessionInfo> _availableSessions = new();
    private readonly ConcurrentDictionary<string, DateTime> _activeSessions = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly Timer _cleanupTimer;

    public SysproSessionManager(
        SysproEnetClient client,
        IOptions<SysproSettings> settings,
        ILogger<SysproSessionManager> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
        _semaphore = new SemaphoreSlim(_settings.SessionPoolSize, _settings.SessionPoolSize);
        
        // Cleanup stale sessions every 5 minutes
        _cleanupTimer = new Timer(CleanupStaleSessions, null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Get a session from the pool (or create a new one).
    /// Caller MUST return the session via ReleaseSession.
    /// </summary>
    public async Task<string> AcquireSessionAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);

        try
        {
            // Try to reuse an existing session
            if (_availableSessions.TryTake(out var sessionInfo))
            {
                // Check if session is still valid (not expired)
                if (DateTime.UtcNow - sessionInfo.LastUsed < 
                    TimeSpan.FromMinutes(_settings.SessionTimeoutMinutes - 2))
                {
                    _activeSessions[sessionInfo.SessionId] = DateTime.UtcNow;
                    _logger.LogDebug("Reusing SYSPRO session {SessionId}", 
                        sessionInfo.SessionId[..8]);
                    return sessionInfo.SessionId;
                }

                // Session likely expired, logoff and create new one
                _logger.LogDebug("Session {SessionId} expired, creating new one", 
                    sessionInfo.SessionId[..8]);
                await _client.LogoffAsync(sessionInfo.SessionId);
            }

            // Create new session
            var newSessionId = await _client.LogonAsync();
            _activeSessions[newSessionId] = DateTime.UtcNow;
            return newSessionId;
        }
        catch
        {
            _semaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Return a session to the pool for reuse.
    /// </summary>
    public void ReleaseSession(string sessionId)
    {
        _activeSessions.TryRemove(sessionId, out _);
        _availableSessions.Add(new SessionInfo(sessionId, DateTime.UtcNow));
        _semaphore.Release();
        _logger.LogDebug("Released SYSPRO session {SessionId} back to pool", 
            sessionId[..8]);
    }

    /// <summary>
    /// Mark a session as invalid (e.g., after a session error). 
    /// Don't return to pool.
    /// </summary>
    public void InvalidateSession(string sessionId)
    {
        _activeSessions.TryRemove(sessionId, out _);
        _semaphore.Release();
        _logger.LogWarning("Invalidated SYSPRO session {SessionId}", sessionId[..8]);
        
        // Fire-and-forget logoff
        _ = _client.LogoffAsync(sessionId);
    }

    private void CleanupStaleSessions(object? state)
    {
        var staleCount = 0;
        var tempBag = new ConcurrentBag<SessionInfo>();

        while (_availableSessions.TryTake(out var session))
        {
            if (DateTime.UtcNow - session.LastUsed < 
                TimeSpan.FromMinutes(_settings.SessionTimeoutMinutes - 2))
            {
                tempBag.Add(session);
            }
            else
            {
                staleCount++;
                _ = _client.LogoffAsync(session.SessionId);
            }
        }

        foreach (var session in tempBag)
            _availableSessions.Add(session);

        if (staleCount > 0)
            _logger.LogInformation("Cleaned up {Count} stale SYSPRO sessions", staleCount);
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
        while (_availableSessions.TryTake(out var session))
            _client.LogoffAsync(session.SessionId).GetAwaiter().GetResult();
    }

    private record SessionInfo(string SessionId, DateTime LastUsed);
}
```

---

## 4.5 XML Builders

### SalesOrderXmlBuilder.cs

```csharp
using System.Xml.Linq;
using SysproIntegration.Core.DTOs;

namespace SysproIntegration.Infrastructure.Syspro.XmlBuilders;

public static class SalesOrderXmlBuilder
{
    public static string BuildParameters()
    {
        return new XElement("SetupSorToi",
            new XElement("Parameters",
                new XElement("PostSalesOrders", "Y"),
                new XElement("ValidateSalesOrderLines", "Y"),
                new XElement("AllowDuplicateOrderNumber", "N"),
                new XElement("DefaultOrderType", "O"),
                new XElement("AllowNonStockedItems", "N"),
                new XElement("ApplyIfEntireDocumentValid", "Y"),
                new XElement("IgnoreWarnings", "N")
            )
        ).ToString();
    }

    public static string BuildDocument(CreateSalesOrderRequest request)
    {
        var lines = request.Lines.Select(line =>
            new XElement("StockLine",
                new XElement("StockCode", line.StockCode),
                new XElement("OrderQty", line.Quantity),
                new XElement("Price", line.UnitPrice),
                new XElement("PriceUom", line.UnitOfMeasure ?? "EA"),
                new XElement("Warehouse", line.Warehouse ?? "WH01")
            )
        );

        var doc = new XElement("SalesOrders",
            new XElement("Orders",
                new XElement("OrderHeader",
                    new XElement("Customer", request.CustomerId),
                    new XElement("OrderDate", request.OrderDate.ToString("yyyy-MM-dd")),
                    new XElement("CustomerPoNumber", request.CustomerPoNumber ?? ""),
                    new XElement("Warehouse", request.Warehouse ?? "WH01"),
                    new XElement("SalesOrderInitSalesOrder", "Y")
                ),
                new XElement("OrderDetails", lines)
            )
        );

        return doc.ToString();
    }
}
```

---

## 4.6 Service Layer — Sales Order Service

```csharp
using Microsoft.Extensions.Logging;
using SysproIntegration.Core.DTOs;
using SysproIntegration.Core.Interfaces;
using SysproIntegration.Infrastructure.Syspro;
using SysproIntegration.Infrastructure.Syspro.XmlBuilders;
using SysproIntegration.Infrastructure.Syspro.XmlParsers;

namespace SysproIntegration.Infrastructure.Services;

public class SalesOrderService : ISalesOrderService
{
    private readonly SysproEnetClient _enetClient;
    private readonly ISysproSessionManager _sessionManager;
    private readonly ILogger<SalesOrderService> _logger;

    public SalesOrderService(
        SysproEnetClient enetClient,
        ISysproSessionManager sessionManager,
        ILogger<SalesOrderService> logger)
    {
        _enetClient = enetClient;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<SalesOrderResponse> CreateSalesOrderAsync(
        CreateSalesOrderRequest request)
    {
        var sessionId = await _sessionManager.AcquireSessionAsync();
        
        try
        {
            _logger.LogInformation(
                "Creating sales order for customer {CustomerId} with {LineCount} lines",
                request.CustomerId, request.Lines.Count);

            var xmlParams = SalesOrderXmlBuilder.BuildParameters();
            var xmlDoc = SalesOrderXmlBuilder.BuildDocument(request);

            var xmlResponse = await _enetClient.TransactionAsync(
                sessionId, "SORTOI", xmlParams, xmlDoc);

            var result = SalesOrderXmlParser.Parse(xmlResponse);

            _logger.LogInformation(
                "Sales order {SalesOrder} created successfully for customer {CustomerId}",
                result.SalesOrderNumber, request.CustomerId);

            return result;
        }
        catch (SysproSessionException)
        {
            _sessionManager.InvalidateSession(sessionId);
            throw;
        }
        finally
        {
            _sessionManager.ReleaseSession(sessionId);
        }
    }

    public async Task<SalesOrderResponse> GetSalesOrderAsync(string salesOrderNumber)
    {
        var sessionId = await _sessionManager.AcquireSessionAsync();
        
        try
        {
            var xmlFilter = $@"
            <Filter>
                <SalesOrder>{salesOrderNumber}</SalesOrder>
            </Filter>";

            var xmlResponse = await _enetClient.QueryAsync(
                sessionId, "SORQRY", xmlFilter);

            return SalesOrderXmlParser.Parse(xmlResponse);
        }
        finally
        {
            _sessionManager.ReleaseSession(sessionId);
        }
    }
}
```

---

## 4.7 Inventory Service

```csharp
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SysproIntegration.Core.DTOs;
using SysproIntegration.Core.Interfaces;

namespace SysproIntegration.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private readonly string _connectionString;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IConfiguration config,
        ILogger<InventoryService> logger)
    {
        _connectionString = config.GetConnectionString("SysproDb")!;
        _logger = logger;
    }

    /// <summary>
    /// Direct SQL read for fast inventory queries.
    /// This is Pattern B — read-only, no business rule validation needed.
    /// </summary>
    public async Task<List<InventoryResponse>> GetInventoryAsync(
        InventoryQueryRequest request)
    {
        var results = new List<InventoryResponse>();

        const string sql = @"
            SELECT 
                m.StockCode,
                m.Description,
                w.Warehouse,
                w.QtyOnHand,
                w.QtyAllocated,
                (w.QtyOnHand - w.QtyAllocated) AS AvailableQty,
                w.QtyOnOrder,
                m.StockUom,
                w.UnitCost,
                w.LastReceiptDate
            FROM InvMaster m
            INNER JOIN InvWarehouse w ON m.StockCode = w.StockCode
            WHERE (@Warehouse IS NULL OR w.Warehouse = @Warehouse)
              AND (@StockCode IS NULL OR m.StockCode LIKE @StockCode + '%')
              AND (@AvailableOnly = 0 OR (w.QtyOnHand - w.QtyAllocated) > 0)
            ORDER BY m.StockCode";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Warehouse", 
            (object?)request.Warehouse ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StockCode", 
            (object?)request.StockCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AvailableOnly", 
            request.AvailableOnly ? 1 : 0);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new InventoryResponse
            {
                StockCode = reader.GetString(0).Trim(),
                Description = reader.GetString(1).Trim(),
                Warehouse = reader.GetString(2).Trim(),
                QtyOnHand = reader.GetDecimal(3),
                QtyAllocated = reader.GetDecimal(4),
                AvailableQty = reader.GetDecimal(5),
                QtyOnOrder = reader.GetDecimal(6),
                UnitOfMeasure = reader.GetString(7).Trim(),
                UnitCost = reader.GetDecimal(8),
                LastReceiptDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
            });
        }

        _logger.LogInformation("Inventory query returned {Count} results", results.Count);
        return results;
    }
}
```

---

## 4.8 Customer Service

```csharp
using Microsoft.Extensions.Logging;
using SysproIntegration.Core.DTOs;
using SysproIntegration.Core.Interfaces;
using SysproIntegration.Infrastructure.Syspro;

namespace SysproIntegration.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly SysproEnetClient _enetClient;
    private readonly ISysproSessionManager _sessionManager;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        SysproEnetClient enetClient,
        ISysproSessionManager sessionManager,
        ILogger<CustomerService> logger)
    {
        _enetClient = enetClient;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<CustomerResponse> CreateCustomerAsync(CreateCustomerRequest request)
    {
        var sessionId = await _sessionManager.AcquireSessionAsync();

        try
        {
            var xmlParams = @"
            <SetupArsTos>
                <Parameters>
                    <CustomerExistsAction>E</CustomerExistsAction>
                </Parameters>
            </SetupArsTos>";

            var xmlDoc = $@"
            <SetupArsTos>
                <Item>
                    <Key>
                        <Customer>{request.CustomerId}</Customer>
                    </Key>
                    <Name>{EscapeXml(request.Name)}</Name>
                    <ShortName>{EscapeXml(request.ShortName)}</ShortName>
                    <SoldToAddr1>{EscapeXml(request.Address1)}</SoldToAddr1>
                    <SoldToAddr2>{EscapeXml(request.Address2)}</SoldToAddr2>
                    <SoldToAddr3>{EscapeXml(request.City)}</SoldToAddr3>
                    <SoldToAddr4>{EscapeXml(request.State)}</SoldToAddr4>
                    <SoldToAddr5>{request.PostalCode}</SoldToAddr5>
                    <Telephone>{request.Phone}</Telephone>
                    <Email>{request.Email}</Email>
                    <CreditLimit>{request.CreditLimit}</CreditLimit>
                    <TermsCode>{request.TermsCode ?? "30"}</TermsCode>
                    <TaxStatus>
                        <TaxStatusCode>{request.TaxCode ?? "E"}</TaxStatusCode>
                    </TaxStatus>
                    <Currency>{request.Currency ?? "USD"}</Currency>
                    <Branch>{request.Branch ?? "01"}</Branch>
                    <Salesperson>{request.Salesperson ?? "01"}</Salesperson>
                </Item>
            </SetupArsTos>";

            var response = await _enetClient.SetupAddAsync(
                sessionId, "ARSTOP", xmlParams, xmlDoc);

            _logger.LogInformation("Customer {CustomerId} created: {Name}",
                request.CustomerId, request.Name);

            return new CustomerResponse
            {
                CustomerId = request.CustomerId,
                Name = request.Name,
                Success = true,
                Message = "Customer created successfully"
            };
        }
        catch (SysproSessionException)
        {
            _sessionManager.InvalidateSession(sessionId);
            throw;
        }
        finally
        {
            _sessionManager.ReleaseSession(sessionId);
        }
    }

    private static string EscapeXml(string? value) =>
        System.Security.SecurityElement.Escape(value ?? string.Empty);
}
```

---

## 4.9 API Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using SysproIntegration.Core.DTOs;
using SysproIntegration.Core.Interfaces;

namespace SysproIntegration.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesOrderController : ControllerBase
{
    private readonly ISalesOrderService _salesOrderService;
    private readonly ILogger<SalesOrderController> _logger;

    public SalesOrderController(
        ISalesOrderService salesOrderService,
        ILogger<SalesOrderController> logger)
    {
        _salesOrderService = salesOrderService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new sales order in SYSPRO.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SalesOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> CreateSalesOrder(
        [FromBody] CreateSalesOrderRequest request)
    {
        // 1. Validate locally BEFORE calling SYSPRO
        if (string.IsNullOrWhiteSpace(request.CustomerId))
            return BadRequest(new ProblemDetails 
            { 
                Title = "Validation Error",
                Detail = "CustomerId is required" 
            });

        if (request.Lines == null || !request.Lines.Any())
            return BadRequest(new ProblemDetails 
            { 
                Title = "Validation Error",
                Detail = "At least one order line is required" 
            });

        try
        {
            var result = await _salesOrderService.CreateSalesOrderAsync(request);
            return CreatedAtAction(
                nameof(GetSalesOrder), 
                new { salesOrderNumber = result.SalesOrderNumber }, 
                result);
        }
        catch (SysproValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "SYSPRO Validation Error",
                Detail = ex.Message,
                Extensions = { ["sysproErrors"] = ex.Errors }
            });
        }
        catch (SysproSessionException)
        {
            return StatusCode(502, new ProblemDetails
            {
                Title = "SYSPRO Unavailable",
                Detail = "Unable to connect to SYSPRO. Please try again."
            });
        }
    }

    [HttpGet("{salesOrderNumber}")]
    public async Task<IActionResult> GetSalesOrder(string salesOrderNumber)
    {
        var result = await _salesOrderService.GetSalesOrderAsync(salesOrderNumber);
        return result == null ? NotFound() : Ok(result);
    }
}
```

---

## 4.10 Complete DTOs (Data Transfer Objects)

### CreateSalesOrderRequest.cs

```csharp
using System.ComponentModel.DataAnnotations;

namespace SysproIntegration.Core.DTOs;

public class CreateSalesOrderRequest
{
    [Required(ErrorMessage = "CustomerId is required")]
    [StringLength(15, ErrorMessage = "CustomerId max 15 chars")]
    public string CustomerId { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; } = DateTime.Today;

    [StringLength(30, ErrorMessage = "CustomerPoNumber max 30 chars")]
    public string? CustomerPoNumber { get; set; }

    [StringLength(10)]
    public string? Warehouse { get; set; }

    [StringLength(20)]
    public string? Salesperson { get; set; }

    [StringLength(10)]
    public string? Branch { get; set; }

    public DateTime? RequestedShipDate { get; set; }

    [StringLength(100)]
    public string? ShippingInstructions { get; set; }

    [StringLength(100)]
    public string? SpecialInstructions { get; set; }

    // Ship-to address (optional — defaults to customer's address)
    public string? ShipAddress1 { get; set; }
    public string? ShipAddress2 { get; set; }
    public string? ShipCity { get; set; }
    public string? ShipState { get; set; }
    public string? ShipPostalCode { get; set; }

    [Required(ErrorMessage = "At least one line is required")]
    [MinLength(1, ErrorMessage = "At least one line is required")]
    public List<CreateSalesOrderLineRequest> Lines { get; set; } = new();
}

public class CreateSalesOrderLineRequest
{
    [Required]
    [StringLength(30)]
    public string StockCode { get; set; } = string.Empty;

    [Range(0.001, 999999, ErrorMessage = "Quantity must be > 0")]
    public decimal Quantity { get; set; }

    [Range(0, 9999999, ErrorMessage = "UnitPrice must be >= 0")]
    public decimal UnitPrice { get; set; }

    [StringLength(10)]
    public string? UnitOfMeasure { get; set; } = "EA";

    [StringLength(10)]
    public string? Warehouse { get; set; }

    public DateTime? LineShipDate { get; set; }

    [Range(0, 100)]
    public decimal? DiscountPercent { get; set; }
}
```

### SalesOrderResponse.cs

```csharp
namespace SysproIntegration.Core.DTOs;

public class SalesOrderResponse
{
    public string SalesOrderNumber { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string OrderStatusText { get; set; } = string.Empty;
    public string? CustomerPoNumber { get; set; }
    public decimal OrderTotalValue { get; set; }
    public decimal TaxValue { get; set; }
    public decimal OrderTotalIncTax { get; set; }
    public List<SalesOrderLineResponse> Lines { get; set; } = new();
}

public class SalesOrderLineResponse
{
    public int LineNumber { get; set; }
    public string StockCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal OrderedQty { get; set; }
    public decimal ShippedQty { get; set; }
    public decimal BackorderQty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string Warehouse { get; set; } = string.Empty;
}
```

### InventoryQueryRequest.cs and InventoryResponse.cs

```csharp
namespace SysproIntegration.Core.DTOs;

public class InventoryQueryRequest
{
    public string? StockCode { get; set; }
    public string? Warehouse { get; set; }
    public string? ProductClass { get; set; }
    public bool AvailableOnly { get; set; } = false;
    public int PageSize { get; set; } = 50;
    public int Page { get; set; } = 1;
}

public class InventoryResponse
{
    public string StockCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public decimal QtyOnHand { get; set; }
    public decimal QtyAllocated { get; set; }
    public decimal AvailableQty { get; set; }
    public decimal QtyOnOrder { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public DateTime? LastReceiptDate { get; set; }
    public string StockStatus => AvailableQty <= 0 ? "OUT_OF_STOCK" : 
        AvailableQty <= 10 ? "LOW_STOCK" : "IN_STOCK";
}
```

### CreateCustomerRequest.cs and CustomerResponse.cs

```csharp
using System.ComponentModel.DataAnnotations;

namespace SysproIntegration.Core.DTOs;

public class CreateCustomerRequest
{
    [Required]
    [StringLength(15)]
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? ShortName { get; set; }

    [StringLength(40)]
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }

    [Phone]
    public string? Phone { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    [Range(0, 99999999)]
    public decimal CreditLimit { get; set; } = 10000;

    public string? TermsCode { get; set; } = "30";
    public string? TaxCode { get; set; } = "E";
    public string? Currency { get; set; } = "USD";
    public string? Branch { get; set; } = "01";
    public string? Salesperson { get; set; }
}

public class CustomerResponse
{
    public string CustomerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CreditStatus { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal? Balance { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
}
```

---

## 4.11 XML Parsers (Missing From Original)

### SalesOrderXmlParser.cs

```csharp
using System.Xml.Linq;
using SysproIntegration.Core.DTOs;

namespace SysproIntegration.Infrastructure.Syspro.XmlParsers;

public static class SalesOrderXmlParser
{
    public static SalesOrderResponse Parse(string xmlResponse)
    {
        var doc = XDocument.Parse(xmlResponse);

        // Check for top-level errors first
        var topError = doc.Descendants("ErrorDescription")
            .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Value));
        if (topError != null)
            throw new SysproValidationException("SORTOI", topError.Value);

        var header = doc.Descendants("OrderHeader").FirstOrDefault();
        if (header == null)
            throw new SysproException("Unexpected XML response: no OrderHeader found");

        var status = GetValue(header, "OrderStatus", "");
        
        var response = new SalesOrderResponse
        {
            SalesOrderNumber = GetValue(header, "SalesOrder", ""),
            Customer = GetValue(header, "Customer", ""),
            OrderDate = DateTime.TryParse(GetValue(header, "OrderDate", ""), out var d) 
                ? d : DateTime.Today,
            OrderStatus = status,
            OrderStatusText = MapStatusText(status),
            CustomerPoNumber = GetValue(header, "CustomerPoNumber", null),
            OrderTotalValue = GetDecimal(header, "OrderTotalValue"),
            TaxValue = GetDecimal(header, "TaxValue"),
            OrderTotalIncTax = GetDecimal(header, "OrderTotalIncTax")
        };

        // Parse order lines
        var lineNumber = 1;
        foreach (var line in doc.Descendants("StockLine"))
        {
            response.Lines.Add(new SalesOrderLineResponse
            {
                LineNumber = lineNumber++,
                StockCode = GetValue(line, "StockCode", "").Trim(),
                Description = GetValue(line, "MStockDes", GetValue(line, "Description", "")),
                OrderedQty = GetDecimal(line, "OrderQty", GetDecimal(line, "MOrderQty")),
                ShippedQty = GetDecimal(line, "MShipQty"),
                BackorderQty = GetDecimal(line, "MBackOrderQty"),
                UnitPrice = GetDecimal(line, "Price", GetDecimal(line, "MOrderPrice")),
                LineTotal = GetDecimal(line, "LineTotal"),
                Warehouse = GetValue(line, "Warehouse", "").Trim()
            });
        }

        return response;
    }

    private static string GetValue(XElement parent, string name, string? fallback) =>
        parent.Element(name)?.Value?.Trim() ?? fallback ?? "";

    private static decimal GetDecimal(XElement parent, string name, decimal fallback = 0) =>
        decimal.TryParse(parent.Element(name)?.Value, out var val) ? val : fallback;

    private static string MapStatusText(string status) => status switch
    {
        "1" => "Open",
        "2" => "In Progress",
        "3" => "Complete",
        "4" => "Forward Order",
        "8" => "Cancelled",
        "9" => "Suspended",
        _ => $"Unknown ({status})"
    };
}
```

### CustomerXmlParser.cs

```csharp
using System.Xml.Linq;
using SysproIntegration.Core.DTOs;

namespace SysproIntegration.Infrastructure.Syspro.XmlParsers;

public static class CustomerXmlParser
{
    public static CustomerResponse Parse(string xmlResponse)
    {
        var doc = XDocument.Parse(xmlResponse);

        var error = doc.Descendants("ErrorDescription")
            .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Value));
        if (error != null)
            throw new SysproValidationException("ARSTOP", error.Value);

        var item = doc.Descendants("Item").FirstOrDefault()
                   ?? doc.Descendants("Customer").FirstOrDefault();

        return new CustomerResponse
        {
            CustomerId = item?.Element("Customer")?.Value?.Trim() 
                         ?? item?.Element("Key")?.Element("Customer")?.Value?.Trim() ?? "",
            Name = item?.Element("Name")?.Value?.Trim() ?? "",
            CreditStatus = item?.Element("CreditStatus")?.Value?.Trim(),
            CreditLimit = decimal.TryParse(item?.Element("CreditLimit")?.Value, out var cl) 
                ? cl : null,
            Balance = decimal.TryParse(item?.Element("Balance")?.Value, out var bal) 
                ? bal : null,
            Success = true,
            Message = "Success"
        };
    }
}
```

---

## 4.12 Mapping Service (Data Translation Layer)

This is one of the **most important** pieces in a real integration — translating your system's codes to SYSPRO codes.

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SysproIntegration.Infrastructure.Services;

/// <summary>
/// Translates external system codes to SYSPRO codes.
/// e.g., Shopify product "SKU-BLUE-HAT" → SYSPRO "HAT-001"
/// e.g., Your customer "CUST-42" → SYSPRO "0000042"
/// </summary>
public class MappingService
{
    private readonly string _connectionString;

    // In-memory cache (refresh every 15 min)
    private Dictionary<string, string> _stockCodeMap = new();
    private Dictionary<string, string> _customerMap = new();
    private Dictionary<string, string> _warehouseMap = new();
    private DateTime _lastRefresh = DateTime.MinValue;

    public MappingService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("LocalDb")!;
    }

    public async Task<string> MapStockCodeAsync(string externalCode)
    {
        await RefreshCacheIfStale();
        
        if (_stockCodeMap.TryGetValue(externalCode, out var sysproCode))
            return sysproCode;
        
        throw new MappingException(
            $"No SYSPRO stock code mapping for external code '{externalCode}'. " +
            $"Add mapping to CodeMappings table.");
    }

    public async Task<string> MapCustomerAsync(string externalCustomerId)
    {
        await RefreshCacheIfStale();
        
        if (_customerMap.TryGetValue(externalCustomerId, out var sysproCustomer))
            return sysproCustomer;
        
        throw new MappingException(
            $"No SYSPRO customer mapping for '{externalCustomerId}'.");
    }

    public async Task<string> MapWarehouseAsync(string externalWarehouse)
    {
        await RefreshCacheIfStale();
        return _warehouseMap.GetValueOrDefault(externalWarehouse, "WH01");
    }

    private async Task RefreshCacheIfStale()
    {
        if (DateTime.UtcNow - _lastRefresh < TimeSpan.FromMinutes(15))
            return;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        _stockCodeMap = await LoadMappings(conn, "StockCode");
        _customerMap = await LoadMappings(conn, "Customer");
        _warehouseMap = await LoadMappings(conn, "Warehouse");
        _lastRefresh = DateTime.UtcNow;
    }

    private static async Task<Dictionary<string, string>> LoadMappings(
        SqlConnection conn, string mappingType)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        const string sql = @"
            SELECT ExternalCode, SysproCode 
            FROM CodeMappings 
            WHERE MappingType = @Type AND IsActive = 1";
        
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Type", mappingType);
        
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            map[reader.GetString(0).Trim()] = reader.GetString(1).Trim();
        
        return map;
    }
}

public class MappingException : Exception
{
    public MappingException(string message) : base(message) { }
}
```

**SQL for the mapping table:**
```sql
CREATE TABLE CodeMappings (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    MappingType     NVARCHAR(50) NOT NULL,    -- 'StockCode', 'Customer', 'Warehouse'
    ExternalCode    NVARCHAR(100) NOT NULL,   -- Code from YOUR system / Shopify / etc.
    SysproCode      NVARCHAR(100) NOT NULL,   -- Corresponding SYSPRO code
    IsActive        BIT NOT NULL DEFAULT 1,
    Notes           NVARCHAR(255) NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    UNIQUE (MappingType, ExternalCode)
);

-- Example data
INSERT INTO CodeMappings (MappingType, ExternalCode, SysproCode, Notes) VALUES
('StockCode', 'SKU-BLUE-HAT', 'HAT-001', 'Blue baseball hat'),
('StockCode', 'SKU-RED-SHIRT', 'SHRT-005', 'Red polo shirt'),
('Customer',  'shopify-cust-42', '0000042', 'Mapped from Shopify'),
('Customer',  'sf-lead-100', '0000100', 'Mapped from Salesforce'),
('Warehouse', 'east', 'WH01', 'East coast warehouse'),
('Warehouse', 'west', 'WH02', 'West coast warehouse');
```

---

## 4.13 Pre-Validation Service

Validate data **before** calling SYSPRO. This saves e.net sessions and gives faster error feedback.

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SysproIntegration.Core.DTOs;

namespace SysproIntegration.Infrastructure.Services;

/// <summary>
/// Pre-validates orders BEFORE calling SYSPRO e.net.
/// Why?: e.net calls take 500ms-2s. Local SQL validation takes 10ms.
/// Catches 80% of errors instantly without consuming a SYSPRO session.
/// </summary>
public class PreValidationService
{
    private readonly string _sysproConnStr;

    public PreValidationService(IConfiguration config)
    {
        _sysproConnStr = config.GetConnectionString("SysproDb")!;
    }

    public async Task<List<string>> ValidateOrderAsync(CreateSalesOrderRequest request)
    {
        var errors = new List<string>();
        await using var conn = new SqlConnection(_sysproConnStr);
        await conn.OpenAsync();

        // 1. Check customer exists and is active
        var custStatus = await GetCustomerStatus(conn, request.CustomerId);
        if (custStatus == null)
            errors.Add($"Customer '{request.CustomerId}' does not exist in SYSPRO");
        else if (custStatus == "S")
            errors.Add($"Customer '{request.CustomerId}' is STOPPED — no transactions allowed");
        else if (custStatus == "H")
            errors.Add($"Customer '{request.CustomerId}' is on CREDIT HOLD — requires approval");

        // 2. Check each stock code exists and has stock
        foreach (var line in request.Lines)
        {
            var stockExists = await StockCodeExists(conn, line.StockCode);
            if (!stockExists)
            {
                errors.Add($"Stock code '{line.StockCode}' does not exist in SYSPRO");
                continue;
            }

            var warehouse = line.Warehouse ?? request.Warehouse ?? "WH01";
            var available = await GetAvailableQty(conn, line.StockCode, warehouse);
            if (available < line.Quantity)
            {
                errors.Add($"Stock code '{line.StockCode}' in '{warehouse}': " +
                    $"requested {line.Quantity}, available {available}");
            }
        }

        // 3. Check for duplicate PO number
        if (!string.IsNullOrWhiteSpace(request.CustomerPoNumber))
        {
            var existingSo = await GetExistingPoOrder(conn, request.CustomerId, 
                request.CustomerPoNumber);
            if (existingSo != null)
                errors.Add($"Customer PO '{request.CustomerPoNumber}' already exists " +
                    $"on SO {existingSo}");
        }

        return errors;
    }

    private static async Task<string?> GetCustomerStatus(SqlConnection conn, string customerId)
    {
        const string sql = "SELECT CreditStatus FROM ArCustomer WHERE Customer = @Id";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", customerId);
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString()?.Trim();
    }

    private static async Task<bool> StockCodeExists(SqlConnection conn, string stockCode)
    {
        const string sql = "SELECT COUNT(1) FROM InvMaster WHERE StockCode = @Code";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Code", stockCode);
        return (int)(await cmd.ExecuteScalarAsync())! > 0;
    }

    private static async Task<decimal> GetAvailableQty(
        SqlConnection conn, string stockCode, string warehouse)
    {
        const string sql = @"
            SELECT ISNULL(QtyOnHand - QtyAllocated, 0) 
            FROM InvWarehouse 
            WHERE StockCode = @Code AND Warehouse = @Wh";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Code", stockCode);
        cmd.Parameters.AddWithValue("@Wh", warehouse);
        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToDecimal(result) : 0;
    }

    private static async Task<string?> GetExistingPoOrder(
        SqlConnection conn, string customer, string poNumber)
    {
        const string sql = @"
            SELECT TOP 1 SalesOrder FROM CusSorMaster 
            WHERE Customer = @Cust AND CustomerPoNumber = @Po";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Cust", customer);
        cmd.Parameters.AddWithValue("@Po", poNumber);
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString()?.Trim();
    }
}
```

---

## 4.14 Enhanced Program.cs (Production-Ready)

```csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using SysproIntegration.Api.Middleware;
using SysproIntegration.Core.Interfaces;
using SysproIntegration.Infrastructure.Configuration;
using SysproIntegration.Infrastructure.Services;
using SysproIntegration.Infrastructure.Syspro;
using SysproIntegration.Infrastructure.Resilience;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, config) => 
    config.ReadFrom.Configuration(ctx.Configuration));

// ─── Configuration ──────────────────────────────────────────
builder.Services.Configure<SysproSettings>(
    builder.Configuration.GetSection(SysproSettings.SectionName));

// ─── SYSPRO e.net client + resilience ───────────────────────
builder.Services.AddHttpClient<SysproEnetClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(ResiliencePolicies.GetRetryPolicy())
.AddPolicyHandler(ResiliencePolicies.GetCircuitBreakerPolicy());

// ─── Services ───────────────────────────────────────────────
builder.Services.AddSingleton<ISysproSessionManager, SysproSessionManager>();
builder.Services.AddScoped<ISalesOrderService, SalesOrderService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<MappingService>();
builder.Services.AddScoped<PreValidationService>();

// ─── CORS ───────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() 
                ?? new[] { "http://localhost:3000" })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ─── Rate Limiting (.NET 8) ─────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.PermitLimit = 100;           // 100 requests
        opt.Window = TimeSpan.FromMinutes(1);  // per minute
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
    
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsync(
            "Rate limit exceeded. Try again later.", token);
    };
});

// ─── Health Checks ──────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("LocalDb")!,
        name: "local-db",
        timeout: TimeSpan.FromSeconds(5))
    .AddSqlServer(
        builder.Configuration.GetConnectionString("SysproDb")!,
        name: "syspro-db",
        timeout: TimeSpan.FromSeconds(5));

// ─── Swagger ────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "SYSPRO Integration API", 
        Version = "v1",
        Description = "REST API for SYSPRO ERP integration"
    });
});

var app = builder.Build();

// ─── Middleware Pipeline ────────────────────────────────────
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("api");
app.MapHealthChecks("/health");

app.Run();
```

---

## 4.15 Unit Tests — XML Builders & Parsers

```csharp
using System.Xml.Linq;
using SysproIntegration.Core.DTOs;
using SysproIntegration.Infrastructure.Syspro.XmlBuilders;
using SysproIntegration.Infrastructure.Syspro.XmlParsers;

namespace SysproIntegration.Tests.XmlBuilders;

public class SalesOrderXmlBuilderTests
{
    [Fact]
    public void BuildDocument_ValidRequest_GeneratesCorrectXml()
    {
        // Arrange
        var request = new CreateSalesOrderRequest
        {
            CustomerId = "0000100",
            OrderDate = new DateTime(2024, 3, 15),
            CustomerPoNumber = "PO-001",
            Warehouse = "WH01",
            Lines = new List<CreateSalesOrderLineRequest>
            {
                new() { StockCode = "A100", Quantity = 10, UnitPrice = 25.50m },
                new() { StockCode = "B200", Quantity = 5, UnitPrice = 100.00m }
            }
        };

        // Act
        var xml = SalesOrderXmlBuilder.BuildDocument(request);
        var doc = XDocument.Parse(xml);

        // Assert
        Assert.Equal("0000100", doc.Descendants("Customer").First().Value);
        Assert.Equal("2024-03-15", doc.Descendants("OrderDate").First().Value);
        Assert.Equal("PO-001", doc.Descendants("CustomerPoNumber").First().Value);
        
        var stockLines = doc.Descendants("StockLine").ToList();
        Assert.Equal(2, stockLines.Count);
        Assert.Equal("A100", stockLines[0].Element("StockCode")!.Value);
        Assert.Equal("10", stockLines[0].Element("OrderQty")!.Value);
        Assert.Equal("B200", stockLines[1].Element("StockCode")!.Value);
    }

    [Fact]
    public void BuildDocument_SpecialCharactersInPO_EscapesXml()
    {
        var request = new CreateSalesOrderRequest
        {
            CustomerId = "0000100",
            CustomerPoNumber = "PO&<>\"001",
            Lines = new() { new() { StockCode = "A100", Quantity = 1, UnitPrice = 10 } }
        };

        var xml = SalesOrderXmlBuilder.BuildDocument(request);
        // Should not throw — XElement handles escaping
        var doc = XDocument.Parse(xml);
        Assert.Contains("PO&amp;", doc.ToString());
    }

    [Fact]
    public void BuildParameters_Always_ContainsAtomicValidation()
    {
        var xml = SalesOrderXmlBuilder.BuildParameters();
        var doc = XDocument.Parse(xml);
        
        Assert.Equal("Y", doc.Descendants("ApplyIfEntireDocumentValid").First().Value);
        Assert.Equal("Y", doc.Descendants("PostSalesOrders").First().Value);
    }
}

public class SalesOrderXmlParserTests
{
    [Fact]
    public void Parse_SuccessResponse_ExtractsOrderNumber()
    {
        var xml = @"
        <SalesOrders>
          <Orders>
            <OrderHeader>
              <SalesOrder>000123</SalesOrder>
              <Customer>0000100</Customer>
              <OrderDate>2024-03-15</OrderDate>
              <OrderStatus>1</OrderStatus>
              <OrderTotalValue>305.00</OrderTotalValue>
              <TaxValue>30.50</TaxValue>
              <OrderTotalIncTax>335.50</OrderTotalIncTax>
            </OrderHeader>
            <OrderDetails>
              <StockLine>
                <StockCode>A100</StockCode>
                <OrderQty>10</OrderQty>
                <Price>25.50</Price>
              </StockLine>
            </OrderDetails>
          </Orders>
        </SalesOrders>";

        var result = SalesOrderXmlParser.Parse(xml);

        Assert.Equal("000123", result.SalesOrderNumber);
        Assert.Equal("0000100", result.Customer);
        Assert.Equal("Open", result.OrderStatusText);
        Assert.Equal(305.00m, result.OrderTotalValue);
        Assert.Single(result.Lines);
        Assert.Equal("A100", result.Lines[0].StockCode);
    }

    [Fact]
    public void Parse_ErrorResponse_ThrowsSysproValidationException()
    {
        var xml = @"
        <SalesOrders>
          <ErrorDescription>Customer 0000999 is on credit hold</ErrorDescription>
        </SalesOrders>";

        var ex = Assert.Throws<SysproValidationException>(
            () => SalesOrderXmlParser.Parse(xml));
        Assert.Contains("credit hold", ex.Message);
    }
}
```

---

[← Back to Main Guide](../README.md) | [Previous: e.net Solutions](./03-ENET-SOLUTIONS.md) | [Next: Real Project →](./05-REAL-PROJECT.md)
