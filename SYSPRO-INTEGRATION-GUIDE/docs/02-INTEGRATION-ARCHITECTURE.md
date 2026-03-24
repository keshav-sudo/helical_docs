# Part 2: Integration Architecture (Very Deep)

[← Back to Main Guide](../README.md) | [Previous: System Understanding](./01-SYSTEM-UNDERSTANDING.md) | [Next: e.net Solutions →](./03-ENET-SOLUTIONS.md)

---

## 2.1 How Real Companies Integrate with SYSPRO

There are exactly **4 core integration patterns** and 1 **hybrid pattern** (which 90% of production systems actually use). Understanding when to use which pattern is what separates a junior developer from a Tech Lead.

### Integration Pattern Decision Framework

```
┌────────────────────────────────────────────────────────────────────────┐
│                INTEGRATION PATTERN DECISION FRAMEWORK                   │
├────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Question 1: Are you WRITING data to SYSPRO or READING data?          │
│  ┌────────────────────────────────────────────────────────────────┐    │
│  │ WRITING (Create/Update/Delete)                                 │    │
│  │ ► MUST use e.net (Pattern A or C or D)                        │    │
│  │ ► NEVER write directly to SQL                                 │    │
│  │ ► Business rules enforced by e.net, not your code             │    │
│  ├────────────────────────────────────────────────────────────────┤    │
│  │ READING (Query/List/Report)                                   │    │
│  │ ► CAN use Direct SQL (Pattern B) — fastest                   │    │
│  │ ► CAN use e.net Query/Browse — slower but portable           │    │
│  │ ► Choose based on speed requirements                          │    │
│  └────────────────────────────────────────────────────────────────┘    │
│                                                                         │
│  Question 2: Does the user need an IMMEDIATE response?                 │
│  ┌────────────────────────────────────────────────────────────────┐    │
│  │ YES — User is waiting on screen                               │    │
│  │ ► Synchronous API (Pattern A)                                 │    │
│  │ ► Response time target: < 3 seconds                           │    │
│  │ ► Examples: "Create this order NOW", "Show me stock levels"    │    │
│  ├────────────────────────────────────────────────────────────────┤    │
│  │ NO — Can process in background                                │    │
│  │ ► How many transactions per hour?                             │    │
│  │   ├── < 100/hour → Batch Sync (Pattern C)                    │    │
│  │   ├── 100-1000/hour → Event-Driven (Pattern D)               │    │
│  │   └── > 1000/hour → Event-Driven with parallelism            │    │
│  └────────────────────────────────────────────────────────────────┘    │
│                                                                         │
│  Question 3: What happens if SYSPRO is DOWN?                           │
│  ┌────────────────────────────────────────────────────────────────┐    │
│  │ System must STOP → Use Pattern A (synchronous)                │    │
│  │ System must CONTINUE → Use Pattern C or D (queued)            │    │
│  └────────────────────────────────────────────────────────────────┘    │
│                                                                         │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2.2 Pattern A: Synchronous API via e.net (Detailed)

This is the **most common starting point**. Your API calls SYSPRO's e.net, waits for a response, and returns it to the caller. Simple, direct, predictable.

### Complete Request Lifecycle

```
┌───────────┐                                                        Time
│  Browser  │                                                         │
│  (User)   │                                                         │
└─────┬─────┘                                                         │
      │                                                               │
      │ 1. POST /api/orders (JSON body)                              │ 0ms
      │    {customer: "0000100", lines: [...]}                       │
      ▼                                                               │
┌───────────┐                                                         │
│ Your .NET │ 2. Validate request locally                            │ 5ms
│ API       │    • Check required fields                              │
│           │    • Validate data types                                │
│           │    • Check customer exists (local cache or SQL read)    │ 50ms
│           │                                                         │
│           │ 3. Check inventory availability (Direct SQL read)      │ 70ms
│           │    SELECT QtyOnHand-QtyAllocated FROM InvWarehouse     │
│           │                                                         │
│           │ 4. Build XML request for SYSPRO                        │ 72ms
│           │    • Convert DTO → XML parameters + document            │
│           │    • Apply mapping (your codes → SYSPRO codes)         │
│           │                                                         │
│           │ 5. Acquire session from pool                           │ 73ms
│           │    • If pool has available session → reuse (0ms)       │
│           │    • If pool empty → new Logon() call (500-2000ms!)    │
│           │                                                         │
│           │ 6. Call e.net Transaction("SORTOI", xmlParams, xmlDoc) │
│           ├──────────────────────────────────┐                     │
│           │                                  ▼                     │
│           │                          ┌───────────────┐             │
│           │                          │  SYSPRO e.net │             │
│           │                          │               │             │
│           │    7. e.net validates:   │ • Parse XML   │             │ 100ms
│           │       • Customer exists? │ • Load rules  │             │
│           │       • Credit limit OK? │ • Check stock │             │ 200ms
│           │       • Stock available? │ • Calculate   │             │
│           │       • Price valid?     │   tax, disc   │             │ 300ms
│           │       • Tax correct?     │ • Create SO   │             │
│           │                          │ • Allocate inv│             │ 500ms
│           │    8. SYSPRO writes       │ • Post to SQL │             │
│           │       to SQL database:   │ • Return XML  │             │ 800ms
│           │       • SorMaster (INSERT)│              │             │
│           │       • SorDetail (INSERT)│              │             │
│           │       • InvWarehouse      │              │             │
│           │         (UPDATE QtyAlloc) │              │             │
│           │       • Audit trail       └───────────────┘             │
│           │                                  │                     │
│           │◄─────────────────────────────────┘                     │ 900ms
│           │ 9. Parse XML response                                  │
│           │    • Extract SalesOrder number                          │
│           │    • Check for errors                                  │ 910ms
│           │                                                         │
│           │ 10. Return session to pool                             │ 911ms
│           │                                                         │
│           │ 11. Save to local DB (audit, sync log)                 │ 930ms
│           │                                                         │
│           │ 12. Build JSON response                                │ 932ms
└─────┬─────┘                                                         │
      │                                                               │
      │ 13. HTTP 201 Created                                         │ 935ms
      │     {salesOrder: "000123", status: "confirmed"}              │
      ▼                                                               │
┌───────────┐                                                         │
│  Browser  │ 14. Show success message to user                      │ 950ms
│  (User)   │     "Order #000123 created successfully!"              │
└───────────┘                                                         ▼
                                                              Total: ~1 second
```

### When Pattern A Works Well

| ✅ Good For | ❌ Bad For |
|------------|-----------|
| User creating a single order on web portal | Processing 500 Shopify orders at midnight |
| Checking a customer's credit status | Nightly full inventory sync |
| Getting real-time price for a product | Bulk data imports/exports |
| User-facing operations needing immediate feedback | Scenarios where SYSPRO downtime is common |
| Low-volume (< 50 orders/hour) | High-concurrency scenarios |

### Typical Latency Breakdown

| Operation | Time | Optimizable? |
|-----------|------|-------------|
| HTTP request overhead | 5-20ms | ❌ Network physics |
| Local validation | 1-5ms | ❌ Already fast |
| SQL inventory check | 10-50ms | ✅ Add Redis cache |
| Session acquisition (pool hit) | 0-1ms | ✅ Warm pool |
| Session acquisition (pool miss) | 500-2000ms | ⚠ Pool sizing |
| e.net XML processing | 200-1500ms | ❌ SYSPRO internal |
| Response parsing | 1-5ms | ❌ Already fast |
| Local DB write | 5-20ms | ❌ Already fast |
| **Total (pool hit)** | **300-1600ms** | |
| **Total (pool miss/first call)** | **800-3600ms** | |

---

## 2.3 Pattern B: Direct SQL Read (Detailed)

For **read-only** operations, skip e.net entirely and query SYSPRO's SQL database directly. This is 10-50x faster than e.net queries.

### Architecture

```
┌────────────┐    REST/JSON      ┌────────────────┐    SQL Query      ┌─────────────┐
│  Frontend  │──────────────────►│  .NET API      │──────────────────►│  SYSPRO     │
│            │◄──────────────────│                │◄──────────────────│  SQL Server │
│            │    JSON           │  Uses:         │    Result Set    │             │
└────────────┘                   │  • ADO.NET      │                   │  READ-ONLY  │
                                 │  • Dapper       │                   │  connection │
                                 │  • EF Core      │                   │             │
                                 │    (read-only)  │                   │  ⚠ User:    │
                                 │                 │                   │  readonly_  │
                                 │  + Redis cache  │                   │  syspro     │
                                 │    for hot data │                   │             │
                                 └────────────────┘                   └─────────────┘
```

### The Golden Rule: NEVER Write to SYSPRO's SQL

This is so important it needs a full explanation:

```
WHY DIRECT SQL WRITES ARE DANGEROUS — CONCRETE EXAMPLE
═══════════════════════════════════════════════════════

Suppose you INSERT a Sales Order directly into SorMaster/SorDetail:

What YOU do:                           What SYSPRO e.net does (that you missed):
────────────                           ────────────────────────────────────────
INSERT INTO SorMaster (...)            1. Validates customer credit limit
INSERT INTO SorDetail (...)            2. Checks customer is not on hold
                                       3. Validates stock code exists
                                       4. Checks stock availability
                                       5. Calculates tax based on tax tables
                                       6. Applies contract pricing
                                       7. Applies volume discounts
                                       8. Allocates inventory (QtyAllocated)
                                       9. Sets correct order status
                                       10. Generates audit trail entries
                                       11. Triggers workflow notifications
                                       12. Updates sales analysis tables
                                       13. Creates CusSorMaster cross-ref
                                       14. Handles multi-currency conversion
                                       15. Validates required custom fields
                                       16. Checks duplicate PO numbers
                                       17. Applies branch/warehouse rules
                                       18. Updates next-number sequence
                                       19. Posts to GL (if configured)
                                       20. Sends email notifications

Result of direct SQL:
• Inventory NOT allocated → overselling possible
• Tax NOT calculated → financial reporting wrong
• Audit trail missing → compliance violation
• GL entries missing → books don't balance
• Workflow not triggered → manager not notified
• Next-number not updated → duplicate SO numbers
• Custom validations skipped → bad data in system

CONSEQUENCE: The client's financial reports are WRONG.
             Their auditor flags this. They lose trust in the system.
             YOUR contract is terminated. ❌
```

### SQL Connection Best Practices

```csharp
// appsettings.json — TWO separate connection strings
{
  "ConnectionStrings": {
    // READ-ONLY access to SYSPRO database
    // Uses a SQL user with only SELECT permissions
    "SysproReadOnly": "Server=syspro-sql;Database=SysproCompanyA;User Id=readonly_syspro;Password={FROM_KEYVAULT};Encrypt=true;TrustServerCertificate=false;Connection Timeout=10;Max Pool Size=20;",
    
    // YOUR local database — full access
    "LocalDb": "Server=local-sql;Database=OrderManagement;Trusted_Connection=true;Encrypt=true;Max Pool Size=50;"
  }
}
```

**SQL user permissions (set up by DBA):**
```sql
-- Create a read-only user for your integration
CREATE LOGIN readonly_syspro WITH PASSWORD = 'StrongPassword!';
USE SysproCompanyA;
CREATE USER readonly_syspro FOR LOGIN readonly_syspro;
ALTER ROLE db_datareader ADD MEMBER readonly_syspro;

-- Do NOT grant:
-- db_datawriter ❌
-- db_owner ❌
-- Any INSERT/UPDATE/DELETE permissions ❌
```

### When to Use e.net Query vs Direct SQL

| Scenario | Use e.net Query | Use Direct SQL |
|----------|----------------|---------------|
| Need business-rule-validated data | ✅ | ❌ |
| Speed critical (< 100ms) | ❌ | ✅ |
| Complex joins across 5+ tables | ❌ | ✅ |
| Aggregations (SUM, COUNT, GROUP BY) | ❌ | ✅ |
| Pagination (TOP, OFFSET) | ❌ | ✅ |
| Customer portal queries | ❌ | ✅ |
| Dashboard data | ❌ | ✅ |
| Simple single-record lookup | ✅ (acceptable) | ✅ (faster) |
| Data with SYSPRO-specific formatting | ✅ | ❌ |

---

## 2.4 Pattern C: Batch Sync (Detailed)

For scenarios where data flows in bulk and a delay of minutes to hours is acceptable. This is the **workhorse pattern** for eCommerce integrations.

### Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                       BATCH SYNC ARCHITECTURE                         │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  EXTERNAL SYSTEM                    YOUR MIDDLEWARE                    │
│  (Shopify, WooCommerce)            (.NET Worker Service)              │
│                                                                       │
│  ┌──────────────┐                  ┌──────────────────────┐          │
│  │  New orders   │    Webhook /    │  1. RECEIVE           │          │
│  │  placed by    │────Schedule ───►│     • Validate JSON   │          │
│  │  customers    │                 │     • Map fields      │          │
│  │  24/7         │                 │     • Assign status:  │          │
│  └──────────────┘                  │       "Pending"       │          │
│                                    └──────────┬───────────┘          │
│                                               │                      │
│                                               ▼                      │
│                                    ┌──────────────────────┐          │
│                                    │  2. STAGING TABLE     │          │
│                                    │                      │          │
│                                    │  OrderQueue           │          │
│                                    │  ├── Id              │          │
│                                    │  ├── ExternalOrderId │          │
│                                    │  ├── JsonPayload     │          │
│                                    │  ├── Status          │          │
│                                    │  │   (Pending,       │          │
│                                    │  │    Processing,    │          │
│                                    │  │    Success,       │          │
│                                    │  │    Failed,        │          │
│                                    │  │    DeadLetter)    │          │
│                                    │  ├── SysproSO        │          │
│                                    │  ├── Attempts        │          │
│                                    │  ├── ErrorMessage    │          │
│                                    │  └── CreatedAt       │          │
│                                    └──────────┬───────────┘          │
│                                               │                      │
│             Runs every N seconds              │                      │
│             (configurable)                    ▼                      │
│                                    ┌──────────────────────┐          │
│                                    │  3. SYNC ENGINE       │          │
│                          ┌────────►│     (Background       │          │
│                          │        │      Worker)           │          │
│                          │        │                      │          │
│                          │        │  FOR EACH pending:   │          │
│                          │        │  a. Lock record      │          │
│                          │        │  b. Build XML        │          │
│                          │        │  c. Call e.net       │          │
│                          │        │  d. Parse response   │          │
│                          │        │  e. Update status    │          │
│  If Failed &             │        │  f. Log result       │          │
│  Attempts < MaxRetry ────┘        │                      │          │
│                                    └──────────┬───────────┘          │
│                                               │                      │
│                                               ▼                      │
│                                    ┌──────────────────────┐          │
│                                    │  4. RESULT            │          │
│                                    │                      │          │
│                                    │  Success: Update     │          │
│                                    │  external system      │          │
│                                    │  (mark as fulfilled)  │          │
│                                    │                      │          │
│                                    │  Dead Letter: Alert   │          │
│                                    │  operations team      │          │
│                                    └──────────────────────┘          │
│                                                                       │
└──────────────────────────────────────────────────────────────────────┘
```

### Real-World Batch Sync SQL Schema

```sql
CREATE TABLE OrderQueue (
    Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
    ExternalSystem  NVARCHAR(50) NOT NULL,      -- 'Shopify', 'WooCommerce', 'EDI'
    ExternalOrderId NVARCHAR(100) NOT NULL,     -- Shopify order ID
    Payload         NVARCHAR(MAX) NOT NULL,     -- Full JSON from external system
    MappedXml       NVARCHAR(MAX) NULL,         -- SYSPRO XML (built during processing)
    SysproSO        NVARCHAR(20) NULL,          -- SYSPRO SO number (after success)
    
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    -- Pending → Processing → Success
    --                      → Failed → Retrying → Success
    --                                           → DeadLetter
    
    Attempts        INT NOT NULL DEFAULT 0,
    MaxAttempts     INT NOT NULL DEFAULT 3,
    NextRetryAt     DATETIME2 NULL,
    LastError       NVARCHAR(MAX) NULL,
    ProcessingNode  NVARCHAR(50) NULL,          -- Which server is processing (HA)
    LockedUntil     DATETIME2 NULL,             -- Pessimistic lock for HA
    
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ProcessedAt     DATETIME2 NULL,
    
    UNIQUE (ExternalSystem, ExternalOrderId),   -- Prevent duplicates
    INDEX IX_Status_NextRetry (Status, NextRetryAt),
    INDEX IX_CreatedAt (CreatedAt)
);
```

---

## 2.5 Pattern D: Event-Driven (Queue-Based) — Detailed

For high-throughput scenarios or when you need **guaranteed delivery** even during SYSPRO downtime.

### Architecture with RabbitMQ

```
┌──────────────────────────────────────────────────────────────────────┐
│                    EVENT-DRIVEN ARCHITECTURE                          │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  PRODUCERS (publish events)          BROKER          CONSUMERS        │
│                                                                       │
│  ┌─────────────┐                  ┌──────────┐  ┌───────────────┐   │
│  │ Web API     │──OrderCreated──►│          │  │ Order         │   │
│  │ (user       │                 │          │──►│ Processor     │   │
│  │  creates    │                 │          │  │ (calls e.net  │   │
│  │  order)     │                 │ RabbitMQ │  │  SORTOI)      │   │
│  └─────────────┘                 │ / Azure  │  └───────────────┘   │
│                                  │ Service  │                       │
│  ┌─────────────┐                 │ Bus      │  ┌───────────────┐   │
│  │ Shopify     │──NewOrder──────►│          │  │ Inventory     │   │
│  │ Webhook     │                 │          │──►│ Updater       │   │
│  │ Handler     │                 │          │  │ (syncs stock  │   │
│  └─────────────┘                 │          │  │  levels back) │   │
│                                  │          │  └───────────────┘   │
│  ┌─────────────┐                 │          │                       │
│  │ IoT Sensor  │──ProductionQty─►│          │  ┌───────────────┐   │
│  │ (factory    │                 │          │  │ Notifications │   │
│  │  floor)     │                 │          │──►│ (email/SMS/   │   │
│  └─────────────┘                 │          │  │  webhook)     │   │
│                                  └──────────┘  └───────────────┘   │
│                                       │                             │
│                                       ▼                             │
│                                  ┌──────────┐                       │
│                                  │ Dead      │  After N retries,   │
│                                  │ Letter    │  move to DLQ for    │
│                                  │ Queue     │  manual review      │
│                                  └──────────┘                       │
│                                                                       │
│  KEY PROPERTIES:                                                      │
│  • Messages are PERSISTENT (survive broker restart)                  │
│  • Each message processed EXACTLY ONCE (with ack/nack)              │
│  • Failed messages get RETRIED with exponential backoff             │
│  • After max retries → Dead Letter Queue for human review           │
│  • Producers don't care if consumers are down                       │
│  • Consumers can be SCALED horizontally                             │
│                                                                       │
└──────────────────────────────────────────────────────────────────────┘
```

### Message Schemas

```csharp
// Message published when a new order is created
public class OrderCreatedEvent
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = "WebAPI";
    public int RetryCount { get; set; } = 0;
    
    // Order data
    public string OrderId { get; set; }       // Your internal ID
    public string CustomerId { get; set; }     // SYSPRO customer code
    public string CustomerPo { get; set; }     // Idempotency key
    public DateTime OrderDate { get; set; }
    public string Warehouse { get; set; }
    public List<OrderLineMessage> Lines { get; set; }
}

public class OrderLineMessage
{
    public string StockCode { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Uom { get; set; } = "EA";
}

// Message published when SYSPRO processing completes
public class OrderSyncedEvent
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string OrderId { get; set; }        // Your internal ID
    public string SysproSalesOrder { get; set; } // SYSPRO SO number
    public bool Success { get; set; }
    public string? Error { get; set; }
}
```

---

## 2.6 Pattern E: The Hybrid (What 90% of Production Systems Actually Use)

No real system uses just one pattern. Here's what a **typical production SYSPRO integration** looks like:

```
┌──────────────────────────────────────────────────────────────────────────┐
│                  HYBRID ARCHITECTURE (PRODUCTION REALITY)                  │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                           │
│  ┌──────────────┐                    ┌──────────────────────────┐        │
│  │ Web Portal   │──Pattern A────────►│                          │        │
│  │ (user creates│  (sync e.net)      │    .NET 8 Web API        │        │
│  │  order)      │                    │                          │        │
│  └──────────────┘                    │  Routes to correct       │        │
│                                      │  pattern based on        │        │
│  ┌──────────────┐                    │  request type:           │        │
│  │ Dashboard    │──Pattern B────────►│                          │        │
│  │ (real-time   │  (direct SQL)      │  POST /orders     → A   │        │
│  │  metrics)    │                    │  GET /inventory   → B   │        │
│  └──────────────┘                    │  POST /batch/     → C   │        │
│                                      │  Webhooks         → D   │        │
│  ┌──────────────┐                    │                          │        │
│  │ Shopify      │──Pattern C────────►│                          │        │
│  │ (nightly     │  (batch sync)      │  READS:                 │        │
│  │  order sync) │                    │  Direct SQL → SYSPRO DB │───┐    │
│  └──────────────┘                    │  (InvWarehouse,         │   │    │
│                                      │   ArCustomer, etc.)     │   │    │
│  ┌──────────────┐                    │                          │   │    │
│  │ IoT Sensors  │──Pattern D────────►│  WRITES:                │   │    │
│  │ (production  │  (event queue)     │  e.net → SYSPRO App Svr │───┤    │
│  │  quantities) │                    │  (SORTOI, INVTMR, etc.) │   │    │
│  └──────────────┘                    │                          │   │    │
│                                      └──────────────────────────┘   │    │
│                                                                      │    │
│  ┌───────────────────────────────────────────────────────────────┐   │    │
│  │                                                               │   │    │
│  │  LOCAL DATABASE (Your DB — staging, audit, cache)             │   │    │
│  │  ├── Orders table (local staging before SYSPRO)              │   │    │
│  │  ├── SyncLog table (every e.net call logged)                 │   │    │
│  │  ├── MappingTables (your codes → SYSPRO codes)               │   │    │
│  │  ├── Cache tables (inventory snapshots refreshed every 5min) │   │    │
│  │  └── AuditTrail (who did what when)                          │   │    │
│  │                                                               │   │    │
│  └───────────────────────────────────────────────────────────────┘   │    │
│                                                                      │    │
│  ┌──────────────────┐          ┌──────────────────┐                 │    │
│  │ SYSPRO App       │          │ SYSPRO SQL       │◄────────────────┘    │
│  │ Server           │◄─────────│ Server           │                      │
│  │ (e.net host)     │  writes  │ (data store)     │    reads             │
│  └──────────────────┘          └──────────────────┘                      │
│                                                                           │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 2.7 Real Company Case Studies (Anonymized)

### Case Study 1: Manufacturing Company — 500 Orders/Day from Shopify

```
COMPANY PROFILE:
• Industry: Consumer electronics accessories
• SYSPRO Version: 8
• Volume: 300-500 Shopify orders/day
• 3 warehouses in different cities

ARCHITECTURE CHOSEN: Batch + Direct SQL (Hybrid)

┌──────────┐    Webhook    ┌────────────┐    Every     ┌──────────┐
│ Shopify  │──────────────►│ .NET API   │──5 minutes──►│ SYSPRO   │
│ Store    │               │ (receive   │   batch      │ e.net    │
│          │               │  & stage)  │              │ SORTOI   │
└──────────┘               └──────┬─────┘              └──────────┘
                                  │
                           ┌──────┴─────┐
                           │ Staging DB  │
                           │ (SQL Azure) │
                           └────────────┘

KEY DECISIONS:
• Orders received via Shopify webhook → staged in local DB immediately
• Background worker processes every 5 minutes (not real-time)
• 5 parallel SYSPRO sessions for throughput
• Inventory synced back to Shopify every 15 minutes (Direct SQL read)
• Failed orders → Dead letter → ops team email alert
• Customer creation handled separately: nightly batch for new customers

PERFORMANCE:
• Average sync time: 1.2 seconds per order
• 5 parallel sessions = ~250 orders/hour throughput
• Daily batch of 500 orders completes in ~2 hours
• Inventory sync: 30,000 SKUs in 45 seconds (direct SQL)
```

### Case Study 2: Distribution Company — EDI Integration

```
COMPANY PROFILE:
• Industry: Industrial supplies distribution
• SYSPRO Version: 7 (legacy, no REST API)
• 200 B2B customers sending EDI documents
• 100-200 EDI 850 (Purchase Orders) per day

ARCHITECTURE CHOSEN: Batch Sync + Direct SQL

┌──────────┐   AS2/SFTP    ┌──────────┐    Scheduled   ┌──────────┐
│ Customer │──────────────►│ EDI      │───────────────►│ SYSPRO   │
│ EDI      │               │ Transl.  │    (every      │ e.net    │
│ Platform │               │ (BizTalk │     15 min)    │          │
│          │◄──────────────│  or      │◄───────────────│          │
│ Receives │   EDI 855     │  custom) │    PO → SO     │          │
│ 855/810  │   EDI 810     │          │    confirm     │          │
└──────────┘               └──────────┘                └──────────┘

FLOW:
1. Customer sends EDI 850 (Purchase Order) via AS2/SFTP
2. EDI translator parses X12 → JSON/XML
3. Mapper converts to SYSPRO codes (UPC → StockCode, etc.)
4. Batch worker creates SO in SYSPRO via e.net
5. SYSPRO confirms → generates EDI 855 (PO Acknowledgment)
6. When shipped → generates EDI 856 (Advance Ship Notice)
7. When invoiced → generates EDI 810 (Invoice)
```

### Case Study 3: Field Service — Mobile Delivery App

```
COMPANY PROFILE:
• Industry: Building materials
• SYSPRO Version: 8
• 50 delivery trucks with tablets
• Same-day delivery service

ARCHITECTURE CHOSEN: Sync API + Event-Driven (Hybrid)

┌──────────┐    REST API    ┌──────────┐    e.net     ┌──────────┐
│ Mobile   │───────────────►│ .NET API │─────────────►│ SYSPRO   │
│ App      │◄───────────────│ (Azure)  │◄─────────────│          │
│ (Tablet) │    JSON        │          │    XML       │          │
└──────────┘                │          │              └──────────┘
     │                      │  VPN to  │
     │ Offline mode:        │  on-prem │
     │ stores locally,      └──────────┘
     │ syncs when online

KEY FEATURES:
• Driver loads truck → app shows delivery list (SQL read via API)
• Driver arrives → scans barcode → confirms delivery (sync e.net)
• Captures signature (stored in blob storage)
• If no internet → queues locally → syncs when back in range
• Real-time GPS tracking (event-driven, not SYSPRO-related)
• Delivery note posted to SYSPRO immediately on confirmation

OFFLINE STRATEGY:
• SQLite on tablet for local queue
• App shows "pending sync" badge
• Auto-syncs when WiFi/4G available
• Conflict resolution: last-write-wins (acceptable for delivery)
```

---

## 2.8 Integration Middleware Comparison

| Feature | Custom .NET | MuleSoft | BizTalk | SYSPRO Harmony |
|---------|------------|----------|---------|----------------|
| **Cost** | Dev time only | $$$$$ (license) | $$$$ (license) | $$ (included in SYSPRO 8) |
| **SYSPRO connector** | Build yourself | Available | Build yourself | Built-in |
| **Learning curve** | Low (if .NET dev) | High (new platform) | High (legacy) | Medium |
| **Flexibility** | Maximum | High | Medium | Low-Medium |
| **Maintenance** | Your team | Vendor + you | Your team | SYSPRO |
| **Best for** | Custom workflows, startups | Enterprise iPaaS | Legacy EDI | Simple SYSPRO-only flows |
| **Scalability** | You control | Excellent | Moderate | Moderate |
| **Vendor lock-in** | None | High | Medium | High (SYSPRO only) |

**Recommendation:** Start with Custom .NET (this guide). Move to MuleSoft/Harmony only if you have 10+ integration points and a dedicated middleware team.

---

## 2.9 Data Flow Direction Map (Complete)

```
┌──────────────────────────────────────────────────────────────────────┐
│                     COMPLETE DATA FLOW DIRECTION MAP                  │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  EXTERNAL → SYSPRO (WRITE via e.net)                                 │
│  ═══════════════════════════════════                                  │
│  • New Sales Orders (SORTOI)                                         │
│  • New Customers (ARSTOP)                                            │
│  • New Purchase Orders (PORTOI)                                      │
│  • Inventory Adjustments (INVTMA)                                    │
│  • Inventory Receipts (INVTMR)                                       │
│  • Inventory Transfers (INVTMT)                                      │
│  • Goods Received Notes (PORTGR)                                     │
│  • Payment Receipts (ARSTCR)                                         │
│  • Production Job Bookings (WIPTJB)                                  │
│  • Credit Notes (SORTCI)                                             │
│  • Stock Code Setup (INVSTS)                                         │
│                                                                       │
│  SYSPRO → EXTERNAL (READ via SQL or e.net Query)                     │
│  ════════════════════════════════════════════════                     │
│  • Order Status & Tracking (SorMaster)                               │
│  • Invoice PDFs & Data (ArInvoice)                                   │
│  • Inventory Levels (InvWarehouse)                                   │
│  • Price Lists (InvPrice)                                            │
│  • Shipment Details (SorDetail.MShipQty)                             │
│  • GL Balances (GlJournal)                                           │
│  • Customer Statements (ArInvoice aging)                             │
│  • Production Status (WipMaster)                                     │
│  • Supplier Data (ApSupplier)                                        │
│  • BOM Structures (BomStructure)                                     │
│                                                                       │
│  BIDIRECTIONAL (Sync Required — Changes on Both Sides)               │
│  ═════════════════════════════════════════════════════                │
│  • Customer Master Data (name, address, contact changes)             │
│  • Product Catalog / Pricing (new items, price changes)              │
│  • Warehouse / Bin Locations (new warehouses, reorganization)        │
│  ⚠ Bidirectional sync is HARDEST — requires conflict resolution     │
│  ⚠ Best practice: designate ONE system as master per field           │
│                                                                       │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 2.10 Integration Decision Matrix (Complete)

| Criteria | Pattern A (Sync API) | Pattern B (Direct SQL) | Pattern C (Batch) | Pattern D (Event) | Pattern E (Hybrid) |
|----------|---------------------|----------------------|-------------------|-------------------|-------------------|
| **Writes to SYSPRO** | ✅ | ❌ NEVER | ✅ (queued) | ✅ (queued) | ✅ |
| **Read performance** | ⚠ 200ms+ | ✅ 10ms | N/A | N/A | ✅ Best of both |
| **Business rules** | ✅ Full | ❌ None | ✅ Full | ✅ Full | ✅ Full |
| **Latency** | 200ms–2s | 10–100ms | Minutes–Hours | Seconds–Minutes | Varies by route |
| **Complexity** | Medium | Low | Medium | High | High |
| **SYSPRO downtime** | ❌ Blocks | ❌ Blocks reads | ✅ Queues work | ✅ Queues work | ✅ Mixed |
| **Volume** | < 100/hr | Unlimited reads | 100–10K/batch | 100–50K/hr | Unlimited |
| **Team skill needed** | .NET + XML | SQL | .NET + SQL | .NET + messaging | All of above |
| **Best for** | User-facing ops | Dashboards/reports | eCommerce sync | High-volume/IoT | Real production |

---

[← Back to Main Guide](../README.md) | [Previous: System Understanding](./01-SYSTEM-UNDERSTANDING.md) | [Next: e.net Solutions →](./03-ENET-SOLUTIONS.md)
