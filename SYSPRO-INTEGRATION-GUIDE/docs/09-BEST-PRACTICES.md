# Part 9: Best Practices & Common Pitfalls

[← Back to Main Guide](../README.md) | [Previous: Deployment](./08-DEPLOYMENT.md) | [Next: Mastery Roadmap →](./10-MASTERY-ROADMAP.md)

---

## 9.1 The 10 Commandments of SYSPRO Integration

```
┌──────────────────────────────────────────────────────────────────┐
│            THE 10 COMMANDMENTS OF SYSPRO INTEGRATION              │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  1. THOU SHALT NEVER WRITE DIRECTLY TO SYSPRO's SQL             │
│     Always use e.net for inserts/updates/deletes.                │
│     Direct SQL = broken audit trail + corrupted data.            │
│                                                                   │
│  2. THOU SHALT POOL THY SESSIONS                                 │
│     One session = one license seat. Login per request = 💀       │
│     Pool size = estimated_concurrent_users × 1.5                 │
│                                                                   │
│  3. THOU SHALT HANDLE TIMEOUTS AS UNCERTAIN                     │
│     Timeout ≠ failure. The transaction may have succeeded.       │
│     Query SYSPRO before retrying to prevent duplicates.          │
│                                                                   │
│  4. THOU SHALT USE IDEMPOTENCY KEYS                             │
│     CustomerPO + Date = unique key per transaction.              │
│     Your API should reject duplicates before calling SYSPRO.     │
│                                                                   │
│  5. THOU SHALT STAGE LOCALLY BEFORE SYSPRO                      │
│     Save orders to YOUR database first.                          │
│     Sync to SYSPRO asynchronously.                               │
│     Your frontend works even when SYSPRO is down.                │
│                                                                   │
│  6. THOU SHALT PRE-VALIDATE BEFORE e.net                        │
│     Check customer exists, stock on hand, PO duplicates          │
│     via direct SQL BEFORE consuming an e.net session.            │
│                                                                   │
│  7. THOU SHALT LOG EVERY e.net CALL                              │
│     XML request + XML response + duration + status.              │
│     Without this, debugging production issues is impossible.     │
│                                                                   │
│  8. THOU SHALT NOT TRUST SYSPRO'S XML SCHEMA TO BE STABLE       │
│     SYSPRO updates can change XML structure.                     │
│     Parse defensively. Use null checks. Log unknown elements.    │
│                                                                   │
│  9. THOU SHALT HAVE A DEAD LETTER QUEUE                         │
│     Failed orders need human review, not infinite retry.         │
│     Max 3 retries → dead letter → alert ops team.               │
│                                                                   │
│  10. THOU SHALT SEPARATE READ AND WRITE PATHS                   │
│      Reads → Direct SQL (fast, no license consumed).            │
│      Writes → e.net (validated, audited, correct).              │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

---

## 9.2 Anti-Patterns — What NOT To Do

### Anti-Pattern 1: Login-Per-Request

```csharp
// ❌ WRONG — Logging in for every API request
public async Task<SalesOrderResponse> CreateOrderAsync(CreateSalesOrderRequest request)
{
    var sessionId = await _enetClient.LogonAsync();    // 500-2000ms EVERY TIME
    try
    {
        var result = await _enetClient.TransactionAsync(sessionId, "SORTOI", params, doc);
        return Parse(result);
    }
    finally
    {
        await _enetClient.LogoffAsync(sessionId);      // Another 50-200ms
    }
}
// Total overhead: 550-2200ms per request (just for login/logout!)
// License consumed and released every request
// Under load: license exhaustion

// ✅ CORRECT — Use session pool
public async Task<SalesOrderResponse> CreateOrderAsync(CreateSalesOrderRequest request)
{
    var sessionId = await _sessionPool.AcquireAsync(); // 0-1ms (from pool)
    try
    {
        var result = await _enetClient.TransactionAsync(sessionId, "SORTOI", params, doc);
        return Parse(result);
    }
    finally
    {
        _sessionPool.Release(sessionId);               // 0ms (back to pool)
    }
}
// Total overhead: ~0ms for session management
```

### Anti-Pattern 2: String Concatenation for XML

```csharp
// ❌ WRONG — SQL injection equivalent for XML
var xml = $"<StockCode>{request.StockCode}</StockCode>";
// If StockCode = "</StockCode><Hacked>true</Hacked><StockCode>"
// → XML injection!

// ✅ CORRECT — Use XElement (auto-escapes)
var xml = new XElement("StockCode", request.StockCode).ToString();
// Special characters are properly escaped
```

### Anti-Pattern 3: No Error Classification

```csharp
// ❌ WRONG — Treating all errors the same
catch (Exception ex)
{
    _logger.LogError(ex, "SYSPRO error");
    return StatusCode(500, "Error occurred");
    // Retrying a validation error wastes resources
    // Returning 500 for a business rule error confuses the frontend
}

// ✅ CORRECT — Classify and handle differently
catch (SysproValidationException ex) { return BadRequest(ex.Message); }
catch (SysproSessionException) { /* retry with new session */ }
catch (SysproConnectionException) { return StatusCode(503); }
```

### Anti-Pattern 4: Synchronous Everything

```csharp
// ❌ WRONG — Making the user wait for SYSPRO
[HttpPost("/api/orders")]
public async Task<IActionResult> CreateOrder(OrderRequest req)
{
    // User stares at loading spinner for 2-5 seconds
    var result = await _syspro.CreateSalesOrder(req);  // 500ms-3s
    return Ok(result);
}

// ✅ CORRECT — Stage locally, sync in background
[HttpPost("/api/orders")]
public async Task<IActionResult> CreateOrder(OrderRequest req)
{
    // Save to local DB (50ms)
    var order = await _localDb.SaveOrderAsync(req);
    
    // Return immediately — user sees success
    return Created($"/api/orders/{order.Id}", new { 
        orderId = order.Id,
        status = "Pending SYSPRO sync",
        message = "Order received. SYSPRO confirmation will follow."
    });
    // Background worker handles SYSPRO sync
}
```

### Anti-Pattern 5: Ignoring SYSPRO Maintenance Windows

```csharp
// ❌ WRONG — No awareness of SYSPRO downtime
// Your system hammers SYSPRO during month-end processing
// → SYSPRO grinds to a halt → finance team angry

// ✅ CORRECT — Schedule-aware processing
public class OrderSyncWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Check if SYSPRO is in maintenance window
            if (IsMaintenanceWindow())
            {
                _logger.LogInformation(
                    "SYSPRO maintenance window active. Pausing sync.");
                await Task.Delay(TimeSpan.FromMinutes(15), ct);
                continue;
            }

            // Reduce throughput during month-end (days 28-31)
            if (IsMonthEnd())
            {
                _logger.LogInformation("Month-end: reducing sync rate");
                // Process 1 order at a time instead of batch
                await ProcessSingleOrder(ct);
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            else
            {
                await ProcessBatch(ct);
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }
    }

    private bool IsMaintenanceWindow()
    {
        var now = DateTime.Now;
        // Sunday 2 AM - 6 AM = maintenance window
        return now.DayOfWeek == DayOfWeek.Sunday && 
               now.Hour >= 2 && now.Hour < 6;
    }

    private bool IsMonthEnd()
    {
        var now = DateTime.Now;
        return now.Day >= 28;
    }
}
```

---

## 9.3 Performance Optimization

### Caching Strategy

```
┌──────────────────────────────────────────────────────────────────┐
│                    CACHING STRATEGY                               │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  HOT DATA (Cache in Redis / MemoryCache)                         │
│  ─────────────────────────────────────                            │
│  • Customer list (refresh every 15 min)                          │
│  • Stock code catalog (refresh every 30 min)                     │
│  • Price lists (refresh every 1 hour)                            │
│  • Inventory levels (refresh every 5 min)                        │
│  • SYSPRO codes / mappings (refresh every 15 min)                │
│                                                                   │
│  WARM DATA (Direct SQL read, no cache)                           │
│  ─────────────────────────────────────                            │
│  • Order status (read from staging DB)                           │
│  • Customer balance (read from SYSPRO SQL)                       │
│  • Specific stock item detail                                    │
│                                                                   │
│  COLD DATA (e.net query, no cache)                               │
│  ─────────────────────────────────                                │
│  • Order details from SYSPRO (after sync)                        │
│  • Invoice PDF generation                                        │
│  • Real-time credit check                                        │
│                                                                   │
│  CACHE INVALIDATION RULES:                                       │
│  ⚠ After creating a customer → invalidate customer cache        │
│  ⚠ After creating an order → invalidate inventory cache         │
│  ⚠ After SYSPRO maintenance → flush ALL caches                  │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

### SQL Query Optimization for SYSPRO Tables

```sql
-- ❌ SLOW: Full table scan on InvWarehouse
SELECT * FROM InvWarehouse WHERE QtyOnHand > 0;

-- ✅ FAST: Select only needed columns, filter on indexed columns
SELECT 
    iw.StockCode,
    iw.Warehouse,
    iw.QtyOnHand - iw.QtyAllocated AS Available 
FROM InvWarehouse iw
WHERE iw.Warehouse = 'WH01'
  AND iw.QtyOnHand - iw.QtyAllocated > 0;

-- ❌ SLOW: Joining every table
SELECT * FROM SorMaster sm
JOIN SorDetail sd ON sm.SalesOrder = sd.SalesOrder
JOIN InvMaster im ON sd.MStockCode = im.StockCode
JOIN InvWarehouse iw ON im.StockCode = iw.StockCode
JOIN ArCustomer ac ON sm.Customer = ac.Customer;

-- ✅ FAST: Only join what you need, use WITH (NOLOCK) for read-only
SELECT 
    sm.SalesOrder, sm.Customer, sm.OrderDate,
    sd.MStockCode, sd.MStockDes, sd.MOrderQty, sd.MOrderPrice
FROM SorMaster sm WITH (NOLOCK)
JOIN SorDetail sd WITH (NOLOCK) ON sm.SalesOrder = sd.SalesOrder
WHERE sm.Customer = @Customer
  AND sm.OrderDate >= @StartDate
ORDER BY sm.OrderDate DESC;
-- WITH (NOLOCK): Acceptable for read-only queries. 
-- Avoids locking SYSPRO tables (which would block SYSPRO users).
-- Trade-off: might read uncommitted data (acceptable for dashboards).
```

---

## 9.4 Data Reconciliation

### Daily Reconciliation Job

```csharp
/// <summary>
/// Runs daily to ensure local DB and SYSPRO are in sync.
/// Catches any orders that fell through the cracks.
/// </summary>
public class ReconciliationService
{
    public async Task<ReconciliationReport> RunDailyReconciliation(DateTime date)
    {
        var report = new ReconciliationReport { Date = date };

        // 1. Orders in local DB marked "Success" but not in SYSPRO
        var localSuccessOrders = await GetLocalSuccessOrders(date);
        foreach (var order in localSuccessOrders)
        {
            var existsInSyspro = await CheckSysproOrder(order.SysproSalesOrder);
            if (!existsInSyspro)
            {
                report.MissingFromSyspro.Add(order);
                // Alert: order marked success but doesn't exist in SYSPRO!
            }
        }

        // 2. Orders in SYSPRO but not in local DB
        var sysproOrders = await GetSysproOrdersForDate(date);
        foreach (var so in sysproOrders)
        {
            var existsLocally = await CheckLocalOrderBySysproSo(so);
            if (!existsLocally)
            {
                report.MissingFromLocal.Add(so);
                // This might be fine (orders created directly in SYSPRO)
                // Or might indicate a logging failure
            }
        }

        // 3. Amount mismatches
        foreach (var order in localSuccessOrders)
        {
            var sysproTotal = await GetSysproOrderTotal(order.SysproSalesOrder);
            if (Math.Abs(order.TotalValue - sysproTotal) > 0.01m)
            {
                report.AmountMismatches.Add(new {
                    Order = order.SysproSalesOrder,
                    LocalTotal = order.TotalValue,
                    SysproTotal = sysproTotal
                });
            }
        }

        return report;
    }
}
```

---

## 9.5 Testing Pyramid for SYSPRO Integration

```
                     ╱╲
                    ╱  ╲
                   ╱ E2E╲               2-3 tests
                  ╱      ╲              Full stack with SYSPRO sandbox
                 ╱────────╲             Run: before release
                ╱Integration╲           10-20 tests
               ╱             ╲          XML parsing, DB, service layer
              ╱───────────────╲         Run: on each PR
             ╱   Unit Tests    ╲        50-100 tests
            ╱                   ╲       XML builders, parsers, validation
           ╱─────────────────────╲      Run: on every commit
```

### Testing Without SYSPRO — Mock Strategy

```csharp
// Interface for testability
public interface ISysproTransactionService
{
    Task<string> ExecuteTransactionAsync(string bo, string paramsXml, string docXml);
}

// Real implementation (production)
public class SysproTransactionService : ISysproTransactionService
{
    public async Task<string> ExecuteTransactionAsync(
        string bo, string paramsXml, string docXml)
    {
        var session = await _pool.AcquireAsync();
        try { return await _client.TransactionAsync(session, bo, paramsXml, docXml); }
        finally { _pool.Release(session); }
    }
}

// Mock implementation (tests)
public class MockSysproTransactionService : ISysproTransactionService
{
    private readonly Dictionary<string, string> _responses = new();

    public void SetResponse(string bo, string xmlResponse)
    {
        _responses[bo] = xmlResponse;
    }

    public Task<string> ExecuteTransactionAsync(
        string bo, string paramsXml, string docXml)
    {
        if (_responses.TryGetValue(bo, out var response))
            return Task.FromResult(response);
        
        throw new SysproConnectionException($"No mock response for BO: {bo}");
    }
}

// Usage in tests:
[Fact]
public async Task CreateOrder_ValidRequest_ReturnsSalesOrderNumber()
{
    var mock = new MockSysproTransactionService();
    mock.SetResponse("SORTOI", @"
        <SalesOrders><Orders><OrderHeader>
            <SalesOrder>000123</SalesOrder>
            <OrderStatus>1</OrderStatus>
        </OrderHeader></Orders></SalesOrders>");

    var service = new SalesOrderService(mock, _logger);
    var result = await service.CreateSalesOrderAsync(validRequest);

    Assert.Equal("000123", result.SalesOrderNumber);
}
```

---

## 9.6 Migration Strategies (Legacy to New Integration)

```
SCENARIO: Migrating from old VB.NET integration to new .NET 8 middleware

PHASE 1: Shadow Mode (2 weeks)
  • Deploy new API alongside old system
  • Old system handles all production traffic
  • New system processes same orders in parallel (write to staging DB only)
  • Compare results: does new system produce same XML as old?

PHASE 2: Read-Only Cutover (1 week)
  • New API handles all READ operations
  • Old system still handles all WRITE operations
  • Frontend updated to call new API for reads

PHASE 3: Write Cutover — Canary (1 week)
  • Route 10% of write traffic to new API
  • Monitor for errors/discrepancies
  • Gradually increase: 25% → 50% → 75% → 100%

PHASE 4: Decommission Old System (1 week)
  • 100% traffic on new API for 1 week without issues
  • Old system remains available but receives no traffic
  • After 1 week: decommission old system

⚠ TOTAL MIGRATION TIMELINE: 5-6 weeks minimum
⚠ NEVER do a "big bang" cutover with ERP integrations
```

---

[← Back to Main Guide](../README.md) | [Previous: Deployment](./08-DEPLOYMENT.md) | [Next: Mastery Roadmap →](./10-MASTERY-ROADMAP.md)
