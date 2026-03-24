# Part 5: Build a Real Project — Order Management System

[← Back to Main Guide](../README.md) | [Previous: .NET Implementation](./04-DOTNET-IMPLEMENTATION.md) | [Next: Error Handling →](./06-ERROR-HANDLING.md)

---

## 5.1 System Design

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│              ORDER MANAGEMENT SYSTEM (OMS) ARCHITECTURE                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌──────────────┐       ┌────────────────────────┐                      │
│  │   Frontend    │       │     .NET 8 Web API      │                      │
│  │   (React)     │──────►│     (OMS Middleware)     │                      │
│  │              │  REST  │                        │                      │
│  │  Pages:      │       │  ┌──────────────────┐  │     ┌──────────────┐ │
│  │  • Dashboard │       │  │ OrderController   │  │     │   SYSPRO     │ │
│  │  • New Order │       │  │ CustomerController│  │────►│   e.net      │ │
│  │  • Inventory │       │  │ InventoryController│ │     │   Solutions  │ │
│  │  • Customers │       │  │ DashboardController│ │     └──────┬───────┘ │
│  └──────────────┘       │  └──────────────────┘  │            │         │
│                          │                        │            ▼         │
│                          │  ┌──────────────────┐  │     ┌──────────────┐ │
│                          │  │ SalesOrderService │  │     │  SYSPRO      │ │
│                          │  │ InventoryService  │  │     │  SQL Server  │ │
│                          │  │ CustomerService   │  │     │  (read-only) │ │
│                          │  │ SyncService       │  │     └──────────────┘ │
│                          │  └──────────────────┘  │                      │
│                          │                        │                      │
│                          │  ┌──────────────────┐  │                      │
│                          │  │ Background Jobs   │  │                      │
│                          │  │ • OrderSyncWorker │  │                      │
│                          │  │ • StaleOrderRetry │  │                      │
│                          │  └──────────────────┘  │                      │
│                          │           │            │                      │
│                          └───────────┼────────────┘                      │
│                                      ▼                                   │
│                          ┌────────────────────────┐                      │
│                          │    Local SQL Database    │                      │
│                          │                        │                      │
│                          │  Tables:               │                      │
│                          │  • Orders              │                      │
│                          │  • OrderLines           │                      │
│                          │  • SyncLog              │                      │
│                          │  • ErrorLog             │                      │
│                          │  • AuditTrail           │                      │
│                          └────────────────────────┘                      │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 5.2 Complete API Endpoints

| Method | Endpoint | Purpose | Source |
|--------|----------|---------|--------|
| `POST` | `/api/orders` | Create new order (stage locally + send to SYSPRO) | Local DB + e.net |
| `GET` | `/api/orders` | List all orders with pagination | Local DB |
| `GET` | `/api/orders/{id}` | Get order details | Local DB |
| `GET` | `/api/orders/{id}/syspro-status` | Get SYSPRO sync status | e.net Query |
| `PUT` | `/api/orders/{id}` | Update order (before SYSPRO sync) | Local DB |
| `POST` | `/api/orders/{id}/retry` | Retry failed SYSPRO sync | e.net |
| `DELETE` | `/api/orders/{id}` | Cancel order | Local DB + e.net |
| `GET` | `/api/inventory` | Search inventory | SYSPRO SQL (read) |
| `GET` | `/api/inventory/{stockCode}` | Get item availability | SYSPRO SQL (read) |
| `GET` | `/api/customers` | List customers | SYSPRO SQL (read) |
| `POST` | `/api/customers` | Create customer in SYSPRO | e.net |
| `GET` | `/api/dashboard/summary` | Dashboard metrics | Local DB + SQL |

---

## 5.3 Local Database Schema

```sql
-- ========================================================
-- OMS LOCAL DATABASE SCHEMA
-- This is YOUR database, not SYSPRO's
-- ========================================================

CREATE TABLE Orders (
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    OrderNumber         NVARCHAR(20) NOT NULL,       -- Your internal order number
    SysproSalesOrder    NVARCHAR(20) NULL,           -- SYSPRO SO number (after sync)
    CustomerId          NVARCHAR(15) NOT NULL,        -- SYSPRO Customer code
    CustomerName        NVARCHAR(100) NOT NULL,
    CustomerPoNumber    NVARCHAR(30) NULL,
    OrderDate           DATE NOT NULL DEFAULT GETDATE(),
    RequiredDate        DATE NULL,
    Warehouse           NVARCHAR(10) NOT NULL DEFAULT 'WH01',
    OrderStatus         NVARCHAR(20) NOT NULL DEFAULT 'Draft',
        -- Draft | Validated | SentToSyspro | Confirmed | Failed | Cancelled
    SyncStatus          NVARCHAR(20) NOT NULL DEFAULT 'Pending',
        -- Pending | InProgress | Success | Failed | Retrying
    SyncAttempts        INT NOT NULL DEFAULT 0,
    LastSyncAttempt     DATETIME2 NULL,
    SyncErrorMessage    NVARCHAR(MAX) NULL,
    SysproXmlRequest    NVARCHAR(MAX) NULL,          -- Store for debugging
    SysproXmlResponse   NVARCHAR(MAX) NULL,          -- Store for debugging
    TotalValue          DECIMAL(18,2) NOT NULL DEFAULT 0,
    CreatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy           NVARCHAR(50) NOT NULL,
    UpdatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Orders_SyncStatus (SyncStatus),
    INDEX IX_Orders_OrderDate (OrderDate),
    INDEX IX_Orders_CustomerId (CustomerId)
);

CREATE TABLE OrderLines (
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    OrderId             UNIQUEIDENTIFIER NOT NULL REFERENCES Orders(Id),
    LineNumber          INT NOT NULL,
    StockCode           NVARCHAR(30) NOT NULL,
    Description         NVARCHAR(100) NOT NULL,
    Quantity            DECIMAL(18,4) NOT NULL,
    UnitPrice           DECIMAL(18,4) NOT NULL,
    LineTotal           AS (Quantity * UnitPrice) PERSISTED,
    UnitOfMeasure       NVARCHAR(10) NOT NULL DEFAULT 'EA',
    Warehouse           NVARCHAR(10) NOT NULL DEFAULT 'WH01',
    
    INDEX IX_OrderLines_OrderId (OrderId),
    UNIQUE (OrderId, LineNumber)
);

CREATE TABLE SyncLog (
    Id                  BIGINT IDENTITY(1,1) PRIMARY KEY,
    OrderId             UNIQUEIDENTIFIER NOT NULL REFERENCES Orders(Id),
    Attempt             INT NOT NULL,
    Direction           NVARCHAR(10) NOT NULL,       -- Outbound | Inbound
    BusinessObject      NVARCHAR(20) NOT NULL,       -- SORTOI, SORQRY, etc.
    XmlSent             NVARCHAR(MAX) NULL,
    XmlReceived         NVARCHAR(MAX) NULL,
    StatusCode          NVARCHAR(20) NOT NULL,       -- Success | Error | Timeout
    ErrorMessage        NVARCHAR(MAX) NULL,
    DurationMs          INT NOT NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_SyncLog_OrderId (OrderId),
    INDEX IX_SyncLog_CreatedAt (CreatedAt)
);

CREATE TABLE AuditTrail (
    Id                  BIGINT IDENTITY(1,1) PRIMARY KEY,
    EntityType          NVARCHAR(50) NOT NULL,       -- Order, Customer, etc.
    EntityId            NVARCHAR(50) NOT NULL,
    Action              NVARCHAR(20) NOT NULL,       -- Create, Update, Delete, Sync
    OldValues           NVARCHAR(MAX) NULL,          -- JSON
    NewValues           NVARCHAR(MAX) NULL,          -- JSON
    UserId              NVARCHAR(50) NOT NULL,
    IpAddress           NVARCHAR(45) NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Audit_Entity (EntityType, EntityId),
    INDEX IX_Audit_CreatedAt (CreatedAt)
);
```

---

## 5.4 Complete Order Flow

```
┌──────────────────────────────────────────────────────────────────┐
│                    COMPLETE ORDER FLOW                            │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Step 1: CREATE ORDER (Frontend → API → Local DB)                │
│  ─────────────────────────────────────────────────                │
│  POST /api/orders                                                 │
│  │                                                                │
│  ├── Validate request (customer exists, items exist)             │
│  ├── Check inventory (direct SQL read from SYSPRO)               │
│  ├── Insert into local Orders + OrderLines tables                │
│  ├── Set OrderStatus = 'Validated', SyncStatus = 'Pending'      │
│  └── Return 201 with local order ID                              │
│                                                                   │
│  Step 2: SYNC TO SYSPRO (Background Worker)                      │
│  ─────────────────────────────────────────────                    │
│  OrderSyncWorker runs every 10 seconds:                          │
│  │                                                                │
│  ├── SELECT * FROM Orders WHERE SyncStatus = 'Pending'           │
│  ├── For each order:                                              │
│  │   ├── Set SyncStatus = 'InProgress'                           │
│  │   ├── Build XML from order + lines                            │
│  │   ├── Call SYSPRO e.net SORTOI Transaction                    │
│  │   ├── Parse response                                          │
│  │   │   ├── SUCCESS:                                            │
│  │   │   │   ├── Extract SYSPRO SO number                        │
│  │   │   │   ├── Update Orders.SysproSalesOrder                  │
│  │   │   │   ├── Set SyncStatus = 'Success'                     │
│  │   │   │   ├── Set OrderStatus = 'Confirmed'                  │
│  │   │   │   └── Log to SyncLog (StatusCode = 'Success')        │
│  │   │   │                                                       │
│  │   │   └── FAILURE:                                            │
│  │   │       ├── Increment SyncAttempts                          │
│  │   │       ├── Store error in SyncErrorMessage                 │
│  │   │       ├── Set SyncStatus based on attempt count:          │
│  │   │       │   ├── Attempts < 3 → 'Retrying'                  │
│  │   │       │   └── Attempts >= 3 → 'Failed'                   │
│  │   │       └── Log to SyncLog (StatusCode = 'Error')           │
│  │   └── Log to AuditTrail                                       │
│  └── Sleep(10 seconds)                                            │
│                                                                   │
│  Step 3: CONFIRM & NOTIFY (After Successful Sync)                │
│  ───────────────────────────────────────────────                  │
│  │                                                                │
│  ├── Update local order with SYSPRO SO number                    │
│  ├── Send confirmation email/webhook                              │
│  └── Frontend polls GET /api/orders/{id} for status update       │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

---

## 5.5 Background Worker — OrderSyncWorker

```csharp
using Microsoft.Data.SqlClient;
using SysproIntegration.Core.Interfaces;
using SysproIntegration.Infrastructure.Syspro.XmlBuilders;
using System.Diagnostics;

namespace SysproIntegration.Api.Workers;

public class OrderSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderSyncWorker> _logger;

    public OrderSyncWorker(
        IServiceProvider serviceProvider,
        ILogger<OrderSyncWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("OrderSyncWorker started");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingOrders(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OrderSyncWorker error");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        }
    }

    private async Task ProcessPendingOrders(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var salesOrderService = scope.ServiceProvider
            .GetRequiredService<ISalesOrderService>();

        // Get pending orders from local DB
        const string sql = @"
            SELECT Id, CustomerId, CustomerPoNumber, OrderDate, Warehouse
            FROM Orders 
            WHERE SyncStatus IN ('Pending', 'Retrying')
              AND SyncAttempts < 3
            ORDER BY CreatedAt";

        var connectionString = scope.ServiceProvider
            .GetRequiredService<IConfiguration>()
            .GetConnectionString("LocalDb")!;

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, conn);
        var orders = new List<PendingOrder>();

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            orders.Add(new PendingOrder
            {
                Id = reader.GetGuid(0),
                CustomerId = reader.GetString(1),
                CustomerPoNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                OrderDate = reader.GetDateTime(3),
                Warehouse = reader.GetString(4)
            });
        }
        await reader.CloseAsync();

        foreach (var order in orders)
        {
            if (ct.IsCancellationRequested) break;

            var sw = Stopwatch.StartNew();
            try
            {
                // Mark as in progress
                await UpdateSyncStatus(conn, order.Id, "InProgress", null);

                // Build request from local data
                var request = await BuildRequestFromLocalDb(conn, order);

                // Send to SYSPRO
                var result = await salesOrderService.CreateSalesOrderAsync(request);
                sw.Stop();

                // Success — update local DB
                await UpdateOrderSuccess(conn, order.Id, 
                    result.SalesOrderNumber, sw.ElapsedMilliseconds);

                _logger.LogInformation(
                    "Order {OrderId} synced to SYSPRO as {SysproSO} in {Ms}ms",
                    order.Id, result.SalesOrderNumber, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                await UpdateOrderFailure(conn, order.Id, 
                    ex.Message, sw.ElapsedMilliseconds);

                _logger.LogWarning(ex,
                    "Order {OrderId} sync failed (will retry)", order.Id);
            }
        }
    }

    private static async Task UpdateSyncStatus(
        SqlConnection conn, Guid orderId, string status, string? error)
    {
        const string sql = @"
            UPDATE Orders 
            SET SyncStatus = @Status, 
                SyncErrorMessage = @Error,
                LastSyncAttempt = GETUTCDATE(),
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", orderId);
        cmd.Parameters.AddWithValue("@Status", status);
        cmd.Parameters.AddWithValue("@Error", (object?)error ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task UpdateOrderSuccess(
        SqlConnection conn, Guid orderId, string sysproSo, long durationMs)
    {
        const string sql = @"
            UPDATE Orders 
            SET SysproSalesOrder = @SysproSO,
                OrderStatus = 'Confirmed',
                SyncStatus = 'Success',
                SyncAttempts = SyncAttempts + 1,
                LastSyncAttempt = GETUTCDATE(),
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id;

            INSERT INTO SyncLog (OrderId, Attempt, Direction, BusinessObject,
                StatusCode, DurationMs)
            SELECT Id, SyncAttempts, 'Outbound', 'SORTOI', 'Success', @Duration
            FROM Orders WHERE Id = @Id;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", orderId);
        cmd.Parameters.AddWithValue("@SysproSO", sysproSo);
        cmd.Parameters.AddWithValue("@Duration", durationMs);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task UpdateOrderFailure(
        SqlConnection conn, Guid orderId, string error, long durationMs)
    {
        const string sql = @"
            UPDATE Orders 
            SET SyncStatus = CASE 
                    WHEN SyncAttempts + 1 >= 3 THEN 'Failed' 
                    ELSE 'Retrying' END,
                OrderStatus = CASE 
                    WHEN SyncAttempts + 1 >= 3 THEN 'Failed' 
                    ELSE OrderStatus END,
                SyncAttempts = SyncAttempts + 1,
                SyncErrorMessage = @Error,
                LastSyncAttempt = GETUTCDATE(),
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id;

            INSERT INTO SyncLog (OrderId, Attempt, Direction, BusinessObject,
                StatusCode, ErrorMessage, DurationMs)
            SELECT Id, SyncAttempts, 'Outbound', 'SORTOI', 'Error', @Error, @Duration
            FROM Orders WHERE Id = @Id;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", orderId);
        cmd.Parameters.AddWithValue("@Error", error);
        cmd.Parameters.AddWithValue("@Duration", durationMs);
        await cmd.ExecuteNonQueryAsync();
    }

    // Helper to build request from local DB (simplified)
    private static async Task<CreateSalesOrderRequest> BuildRequestFromLocalDb(
        SqlConnection conn, PendingOrder order)
    {
        var lines = new List<CreateSalesOrderLineRequest>();
        const string sql = "SELECT StockCode, Quantity, UnitPrice, UnitOfMeasure, Warehouse FROM OrderLines WHERE OrderId = @OrderId ORDER BY LineNumber";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", order.Id);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lines.Add(new CreateSalesOrderLineRequest
            {
                StockCode = reader.GetString(0),
                Quantity = reader.GetDecimal(1),
                UnitPrice = reader.GetDecimal(2),
                UnitOfMeasure = reader.GetString(3),
                Warehouse = reader.GetString(4)
            });
        }

        return new CreateSalesOrderRequest
        {
            CustomerId = order.CustomerId,
            CustomerPoNumber = order.CustomerPoNumber,
            OrderDate = order.OrderDate,
            Warehouse = order.Warehouse,
            Lines = lines
        };
    }

    private class PendingOrder
    {
        public Guid Id { get; set; }
        public string CustomerId { get; set; } = "";
        public string? CustomerPoNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public string Warehouse { get; set; } = "WH01";
    }
}
```

---

[← Back to Main Guide](../README.md) | [Previous: .NET Implementation](./04-DOTNET-IMPLEMENTATION.md) | [Next: Error Handling →](./06-ERROR-HANDLING.md)
