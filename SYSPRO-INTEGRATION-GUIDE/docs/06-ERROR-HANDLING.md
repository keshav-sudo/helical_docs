# Part 6: Error Handling & Resilience (Production-Grade)

[← Back to Main Guide](../README.md) | [Previous: Real Project](./05-REAL-PROJECT.md) | [Next: Security →](./07-SECURITY-AUTH.md)

---

## 6.1 Exception Hierarchy — Complete Implementation

```
┌──────────────────────────────────────────────────────────────────┐
│                    EXCEPTION HIERARCHY                            │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Exception (System base)                                         │
│  └── SysproException (Base for all SYSPRO errors)               │
│      ├── SysproValidationException (Business rule violated)     │
│      │   • DO NOT RETRY                                          │
│      │   • Return 400 to caller                                 │
│      │   • Log as Warning                                        │
│      │                                                           │
│      ├── SysproSessionException (Session invalid/expired)       │
│      │   • RETRY with new session (once)                        │
│      │   • If retry fails → 502                                 │
│      │   • Log as Warning (first), Error (second)               │
│      │                                                           │
│      ├── SysproConnectionException (Can't reach server)         │
│      │   • RETRY with exponential backoff                       │
│      │   • Circuit breaker may open                             │
│      │   • Return 503 to caller                                 │
│      │   • Log as Error                                          │
│      │                                                           │
│      ├── SysproLicenseException (No licenses available)         │
│      │   • WAIT 30-60 seconds, then retry                       │
│      │   • Return 503 with Retry-After header                   │
│      │   • Log as Warning + alert ops team                      │
│      │                                                           │
│      ├── SysproTimeoutException (Response not received in time) │
│      │   • DANGEROUS: transaction may have succeeded            │
│      │   • Check before retrying                                │
│      │   • Return 504 to caller                                 │
│      │   • Log as Error + alert ops team                        │
│      │                                                           │
│      └── SysproRecordLockException (Record locked by user)      │
│          • WAIT 5-10 seconds, retry (max 3 times)              │
│          • Return 409 if all retries fail                       │
│          • Log as Warning                                        │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

### Complete Exception Classes

```csharp
namespace SysproIntegration.Core.Exceptions;

/// <summary>Base exception for all SYSPRO-related errors.</summary>
public class SysproException : Exception
{
    public string? BusinessObject { get; }
    public string? XmlRequest { get; }       // XML that caused the error
    public string? XmlResponse { get; }      // Raw SYSPRO response

    public SysproException(string message) : base(message) { }
    public SysproException(string message, Exception inner) : base(message, inner) { }
    
    public SysproException(string message, string? bo, string? xmlReq, string? xmlRes) 
        : base(message)
    {
        BusinessObject = bo;
        XmlRequest = xmlReq;
        XmlResponse = xmlRes;
    }
}

/// <summary>SYSPRO returned a business validation error. DO NOT retry.</summary>
public class SysproValidationException : SysproException
{
    public List<string> Errors { get; }
    
    public SysproValidationException(string businessObject, string errorMessage)
        : base($"SYSPRO {businessObject} validation error: {errorMessage}")
    {
        BusinessObject = businessObject;
        Errors = new List<string> { errorMessage };
    }

    public SysproValidationException(string businessObject, List<string> errors)
        : base($"SYSPRO {businessObject} validation errors: {string.Join("; ", errors)}")
    {
        BusinessObject = businessObject;
        Errors = errors;
    }
    
    public new string? BusinessObject { get; }
}

/// <summary>Session expired or invalid. Retry with new session.</summary>
public class SysproSessionException : SysproException
{
    public string? SessionId { get; }
    
    public SysproSessionException(string message, string? sessionId = null) 
        : base(message) 
    {
        SessionId = sessionId;
    }
}

/// <summary>Cannot reach SYSPRO server. Retry with backoff.</summary>
public class SysproConnectionException : SysproException
{
    public SysproConnectionException(string message, Exception? inner = null) 
        : base(message, inner ?? new Exception()) { }
}

/// <summary>No e.net licenses available. Wait and retry.</summary>
public class SysproLicenseException : SysproException
{
    public SysproLicenseException(string message) : base(message) { }
}

/// <summary>
/// SYSPRO didn't respond in time. DANGEROUS — transaction may have posted.
/// Must verify before retrying.
/// </summary>
public class SysproTimeoutException : SysproException
{
    public string? PotentialOrderNumber { get; }
    
    public SysproTimeoutException(string message, string? potentialOrder = null) 
        : base(message)
    {
        PotentialOrderNumber = potentialOrder;
    }
}

/// <summary>Record locked by another user/session.</summary>
public class SysproRecordLockException : SysproException
{
    public string? LockedRecord { get; }
    
    public SysproRecordLockException(string message, string? lockedRecord = null)
        : base(message)
    {
        LockedRecord = lockedRecord;
    }
}
```

---

## 6.2 Global Exception Handling Middleware

```csharp
using System.Net;
using System.Text.Json;
using SysproIntegration.Core.Exceptions;

namespace SysproIntegration.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, 
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, title, detail, logLevel) = ex switch
        {
            SysproValidationException ve => (
                HttpStatusCode.BadRequest,
                "SYSPRO Validation Error",
                ve.Message,
                LogLevel.Warning),

            SysproSessionException => (
                HttpStatusCode.BadGateway,
                "SYSPRO Session Error",
                "Unable to establish SYSPRO session. Please try again.",
                LogLevel.Warning),

            SysproConnectionException => (
                HttpStatusCode.ServiceUnavailable,
                "SYSPRO Unavailable",
                "SYSPRO ERP system is currently unreachable. Please try again later.",
                LogLevel.Error),

            SysproLicenseException => (
                HttpStatusCode.ServiceUnavailable,
                "SYSPRO License Limit",
                "Maximum concurrent connections reached. Please try again in 30 seconds.",
                LogLevel.Warning),

            SysproTimeoutException => (
                HttpStatusCode.GatewayTimeout,
                "SYSPRO Timeout",
                "SYSPRO did not respond in time. Your transaction MAY have been processed." +
                " Check SYSPRO before retrying.",
                LogLevel.Error),

            SysproRecordLockException => (
                HttpStatusCode.Conflict,
                "Record Locked",
                "The record is currently being edited by another user.",
                LogLevel.Warning),

            MappingException => (
                HttpStatusCode.BadRequest,
                "Data Mapping Error",
                ex.Message,
                LogLevel.Warning),

            _ => (
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred. Please contact support.",
                LogLevel.Error)
        };

        _logger.Log(logLevel, ex, 
            "Request {Method} {Path} failed with {Status}: {Message}",
            context.Request.Method, context.Request.Path, statusCode, ex.Message);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = $"https://httpstatuses.com/{(int)statusCode}",
            title,
            status = (int)statusCode,
            detail,
            traceId = context.TraceIdentifier,
            timestamp = DateTime.UtcNow
        };

        // Add Retry-After header for 503 responses
        if (statusCode == HttpStatusCode.ServiceUnavailable)
            context.Response.Headers["Retry-After"] = "30";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }
}
```

---

## 6.3 Polly Resilience Policies — Production Configuration

```csharp
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;

namespace SysproIntegration.Infrastructure.Resilience;

public static class ResiliencePolicies
{
    /// <summary>
    /// Retry policy: 3 retries with exponential backoff + jitter.
    /// Only retries on transient HTTP errors (5xx, 408, network failures).
    /// Does NOT retry on 4xx (business validation errors).
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()         // 5xx, 408, network failures
            .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt =>
                {
                    // Exponential backoff: 1s, 2s, 4s (with ±25% jitter)
                    var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1));
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(
                        (int)(-baseDelay.TotalMilliseconds * 0.25),
                        (int)(baseDelay.TotalMilliseconds * 0.25)));
                    return baseDelay + jitter;
                },
                onRetry: (outcome, delay, retryCount, context) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogWarning(
                        "SYSPRO retry #{RetryCount} after {Delay}ms. " +
                        "Status: {StatusCode}. Reason: {Reason}",
                        retryCount, 
                        delay.TotalMilliseconds,
                        outcome.Result?.StatusCode,
                        outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase);
                });
    }

    /// <summary>
    /// Circuit breaker: Opens after 5 consecutive failures.
    /// Stays open for 30 seconds before trying again.
    /// Prevents cascading failures when SYSPRO is down.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, duration) =>
                {
                    // CIRCUIT OPENED — SYSPRO is considered DOWN
                    // All requests will immediately fail for 30 seconds
                    Console.WriteLine(
                        $"⚠ CIRCUIT BREAKER OPENED for {duration.TotalSeconds}s. " +
                        $"SYSPRO is unreachable.");
                },
                onReset: () =>
                {
                    Console.WriteLine("✅ CIRCUIT BREAKER RESET. SYSPRO is reachable again.");
                },
                onHalfOpen: () =>
                {
                    Console.WriteLine("🔄 CIRCUIT BREAKER HALF-OPEN. Testing SYSPRO...");
                });
    }

    /// <summary>
    /// Combined policy: Timeout → Retry → Circuit Breaker
    /// Applied in order: outermost → innermost
    /// </summary>
    public static AsyncPolicyWrap<HttpResponseMessage> GetCombinedPolicy()
    {
        var timeout = Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(30), 
            TimeoutStrategy.Pessimistic);

        return Policy.WrapAsync(
            GetCircuitBreakerPolicy(),
            GetRetryPolicy(),
            timeout);
    }
}

// Extension to pass logger through Polly context
public static class PollyContextExtensions
{
    private const string LoggerKey = "ILogger";

    public static Context WithLogger(this Context context, ILogger logger)
    {
        context[LoggerKey] = logger;
        return context;
    }

    public static ILogger? GetLogger(this Context context)
    {
        return context.TryGetValue(LoggerKey, out var logger) ? logger as ILogger : null;
    }
}
```

### How Polly Works — Visual Explanation

```
SCENARIO: SYSPRO goes down for 2 minutes

Request 1 → e.net call fails (network error)
  ├── Retry 1 (after 1s) → fails
  ├── Retry 2 (after 2s) → fails
  └── Retry 3 (after 4s) → fails
  Circuit breaker: failure count = 1 (out of 5)

Request 2 → e.net call fails
  └── (3 retries) → all fail
  Circuit breaker: failure count = 2

... Requests 3, 4, 5 also fail ...

Request 6 → Circuit breaker: failure count = 5 → OPENS CIRCUIT
  └── Returns IMMEDIATELY with 503 (no e.net call made)
  └── "SYSPRO is unreachable" (fast fail — protects SYSPRO from being hammered)

... Next 30 seconds: ALL requests fail immediately (circuit open) ...

After 30s → Circuit breaker enters HALF-OPEN state
Request 7 → Makes ONE test call to SYSPRO
  ├── If succeeds → RESET circuit → normal operation
  └── If fails → REOPEN circuit for another 30s
```

---

## 6.4 Dead Letter Queue (DLQ) — Handling Permanently Failed Orders

```csharp
using Microsoft.Data.SqlClient;

namespace SysproIntegration.Infrastructure.Services;

/// <summary>
/// Handles orders that have failed all retry attempts.
/// These need human review — NOT automatic retry.
/// </summary>
public class DeadLetterService
{
    private readonly string _localConnStr;
    private readonly ILogger<DeadLetterService> _logger;

    public DeadLetterService(IConfiguration config, ILogger<DeadLetterService> logger)
    {
        _localConnStr = config.GetConnectionString("LocalDb")!;
        _logger = logger;
    }

    /// <summary>Move a failed order to the dead letter queue.</summary>
    public async Task MoveToDeadLetterAsync(Guid orderId, string reason)
    {
        await using var conn = new SqlConnection(_localConnStr);
        await conn.OpenAsync();

        const string sql = @"
            UPDATE Orders 
            SET SyncStatus = 'DeadLetter',
                OrderStatus = 'Failed',
                SyncErrorMessage = @Reason,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id;

            INSERT INTO DeadLetterLog (OrderId, Reason, XmlRequest, XmlResponse, CreatedAt)
            SELECT @Id, @Reason, SysproXmlRequest, SysproXmlResponse, GETUTCDATE()
            FROM Orders WHERE Id = @Id;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", orderId);
        cmd.Parameters.AddWithValue("@Reason", reason);
        await cmd.ExecuteNonQueryAsync();

        _logger.LogError(
            "Order {OrderId} moved to DEAD LETTER QUEUE. Reason: {Reason}",
            orderId, reason);

        // Alert operations team
        await SendAlertAsync(orderId, reason);
    }

    /// <summary>Manually retry a dead-lettered order after fixing the issue.</summary>
    public async Task RequeueAsync(Guid orderId)
    {
        await using var conn = new SqlConnection(_localConnStr);
        await conn.OpenAsync();

        const string sql = @"
            UPDATE Orders 
            SET SyncStatus = 'Pending',
                SyncAttempts = 0,
                SyncErrorMessage = NULL,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND SyncStatus = 'DeadLetter'";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", orderId);
        var rows = await cmd.ExecuteNonQueryAsync();

        if (rows > 0)
            _logger.LogInformation("Order {OrderId} requeued from dead letter", orderId);
        else
            _logger.LogWarning("Order {OrderId} not found in dead letter queue", orderId);
    }

    private async Task SendAlertAsync(Guid orderId, string reason)
    {
        // Implementation options:
        // 1. Send email via SMTP/SendGrid
        // 2. Post to Slack/Teams webhook
        // 3. Create PagerDuty incident
        // 4. Push to Azure Monitor / Application Insights

        _logger.LogCritical(
            "🚨 DEAD LETTER ALERT: Order {OrderId} requires manual review. " +
            "Reason: {Reason}", orderId, reason);
    }
}
```

**Dead Letter SQL Table:**
```sql
CREATE TABLE DeadLetterLog (
    Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
    OrderId         UNIQUEIDENTIFIER NOT NULL REFERENCES Orders(Id),
    Reason          NVARCHAR(MAX) NOT NULL,
    XmlRequest      NVARCHAR(MAX) NULL,
    XmlResponse     NVARCHAR(MAX) NULL,
    ReviewedBy      NVARCHAR(50) NULL,
    ReviewedAt      DATETIME2 NULL,
    Resolution      NVARCHAR(MAX) NULL,  -- What the operator did to fix it
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_DLQ_CreatedAt (CreatedAt)
);
```

---

## 6.5 Timeout Handling — The Most Dangerous Error

```
┌──────────────────────────────────────────────────────────────────┐
│               WHY TIMEOUTS ARE DANGEROUS                          │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  YOUR CODE                     SYSPRO                            │
│  ─────────                     ──────                            │
│                                                                   │
│  Transaction("SORTOI", xml) ──────────────►                      │
│                                            Processing...         │
│  ⏱ ...30 seconds pass...                  Creating SO...        │
│                                            Writing to SQL...     │
│  "TIMEOUT!" ← You give up                 Posted to GL...       │
│  Connection closed                         Done! ✅              │
│                                                                   │
│  You think: ❌ "It failed"                                       │
│  SYSPRO says: ✅ "SO 000456 created"                             │
│                                                                   │
│  RESULT: Order 000456 exists in SYSPRO but your system           │
│  doesn't know about it. If you RETRY, you'll create a           │
│  DUPLICATE order (000457).                                        │
│                                                                   │
│  SOLUTION: Before retrying, QUERY SYSPRO first:                  │
│  1. Query by CustomerPoNumber (if unique)                        │
│  2. If order already exists → update local DB with SO number     │
│  3. If order doesn't exist → safe to retry                      │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

### Timeout-Safe Transaction Handler

```csharp
/// <summary>
/// Wraps e.net transactions with timeout-safe retry logic.
/// If a timeout occurs, queries SYSPRO first before retrying.
/// </summary>
public async Task<SalesOrderResponse> SafeCreateOrderAsync(
    CreateSalesOrderRequest request)
{
    try
    {
        return await _salesOrderService.CreateSalesOrderAsync(request);
    }
    catch (SysproTimeoutException)
    {
        _logger.LogWarning("Timeout creating order for PO {Po}. Checking if order was created...",
            request.CustomerPoNumber);
        
        // Wait a moment for SYSPRO to finish processing
        await Task.Delay(5000);

        // Check if the order was actually created
        if (!string.IsNullOrWhiteSpace(request.CustomerPoNumber))
        {
            var existingOrder = await CheckOrderExistsByPo(
                request.CustomerId, request.CustomerPoNumber);
            
            if (existingOrder != null)
            {
                _logger.LogInformation(
                    "Order was created despite timeout. SO: {SO}", existingOrder);
                
                return new SalesOrderResponse 
                { 
                    SalesOrderNumber = existingOrder,
                    Customer = request.CustomerId
                };
            }
        }

        // Order was NOT created — safe to retry
        _logger.LogInformation("Order was NOT created. Retrying...");
        return await _salesOrderService.CreateSalesOrderAsync(request);
    }
}
```

---

## 6.6 Structured Logging for SYSPRO Integration

```csharp
// Every e.net call should log this structure:
_logger.LogInformation(
    "SYSPRO {Direction} | BO={BusinessObject} | Session={SessionId} | " +
    "Status={Status} | Duration={DurationMs}ms | OrderId={OrderId} | " +
    "SysproSO={SysproSO}",
    "Outbound",           // Direction: Outbound (to SYSPRO), Inbound (from SYSPRO)
    "SORTOI",             // BusinessObject
    sessionId[..8],       // SessionId (first 8 chars only)
    "Success",            // Status: Success, Error, Timeout
    sw.ElapsedMilliseconds,
    localOrderId,
    sysproSalesOrderNumber);
```

**Serilog Configuration for Production:**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "System.Net.Http": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/syspro-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "fileSizeLimitBytes": 104857600,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "Seq",
        "Args": { "serverUrl": "http://seq:5341" }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithEnvironmentName"]
  }
}
```

---

## 6.7 Operations Runbook — Common Error Scenarios

| # | Alert / Error | Root Cause | Immediate Action | Long-term Fix |
|---|--------------|------------|------------------|---------------|
| 1 | "Circuit breaker OPENED" | SYSPRO server or network down | Check SYSPRO Application Server service, check network | Set up monitoring + auto-restart |
| 2 | "No e.net license available" | Too many concurrent sessions | Check SYSPRO → Admin → License Details for orphaned sessions | Increase license count, optimize pool size |
| 3 | "Invalid Logon ID" (frequent) | Sessions expiring faster than expected | Check SYSPRO session timeout setting | Reduce pool session lifetime buffer |
| 4 | Dead letter queue growing | Data quality issues (bad customer/stock codes) | Review DLQ entries, fix data, requeue | Add pre-validation step |
| 5 | "Duplicate CustomerPO" bulk | Retry creating already-posted orders | Query SYSPRO to confirm, update local DB | Implement idempotency check before retry |
| 6 | High latency (> 3s per order) | SYSPRO server overloaded or large BOM calculations | Check SYSPRO server resources (CPU, RAM) | Scale SYSPRO, optimize queries, cache |
| 7 | "Record locked by another user" | SYSPRO desktop user editing same record | Wait and retry, contact user | Time-based lock with auto-release |
| 8 | 500 errors after SYSPRO upgrade | XML schema changed between versions | Compare old/new XML schemas, update builders | Version-aware XML builders |

---

[← Back to Main Guide](../README.md) | [Previous: Real Project](./05-REAL-PROJECT.md) | [Next: Security →](./07-SECURITY-AUTH.md)
