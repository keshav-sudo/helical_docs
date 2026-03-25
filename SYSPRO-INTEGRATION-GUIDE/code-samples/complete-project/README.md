# SYSPRO Integration API - Complete Working Example

A production-ready .NET 8 Web API for SYSPRO e.net integration.

## 🎯 What This Project Does

This is a **complete, runnable** .NET project that:
- Connects to SYSPRO via e.net Solutions
- Manages session pooling (saves licenses!)
- Provides REST API endpoints for common operations
- Includes proper error handling & logging
- Ready for Docker deployment

## 📁 Project Structure

```
complete-project/
├── Program.cs                    # Application entry point
├── SysproIntegrationApi.csproj   # Project file
├── appsettings.json              # Configuration
├── Dockerfile                    # Docker build
├── Controllers/
│   ├── InventoryController.cs    # Inventory endpoints
│   ├── CustomersController.cs    # Customer endpoints
│   └── OrdersController.cs       # Sales order endpoints
├── Services/
│   ├── SysproEnetClient.cs       # Core e.net client
│   ├── SysproSessionPool.cs      # Session management
│   ├── InventoryService.cs       # Inventory operations
│   ├── CustomerService.cs        # Customer operations
│   └── OrderService.cs           # Order operations
├── Models/
│   ├── InventoryItem.cs          # Inventory DTOs
│   ├── Customer.cs               # Customer DTOs
│   └── SalesOrder.cs             # Order DTOs
└── Configuration/
    └── SysproSettings.cs         # Settings classes
```

## 🚀 Quick Start

### Option 1: Run Locally

```bash
# 1. Clone or copy this project
cd complete-project

# 2. Restore packages
dotnet restore

# 3. Configure SYSPRO connection
# Edit appsettings.json with your SYSPRO server details

# 4. Run
dotnet run

# 5. Test
curl http://localhost:5000/api/health
curl http://localhost:5000/api/inventory/STOCKCODE
```

### Option 2: Run with Docker

```bash
# 1. Build image
docker build -t syspro-api .

# 2. Run container
docker run -d \
  -p 5000:8080 \
  -e Syspro__BaseUrl=http://syspro-server:30000 \
  -e Syspro__Operator=ADMIN \
  -e Syspro__Password=yourpassword \
  -e Syspro__CompanyId=A \
  syspro-api

# 3. Test
curl http://localhost:5000/api/health
```

## 📋 API Endpoints

### Health Check
```
GET /api/health
→ { "status": "healthy", "syspro": "connected" }
```

### Inventory
```
GET  /api/inventory/{stockCode}           # Get item
GET  /api/inventory?warehouse=01          # List items
POST /api/inventory/movement              # Post movement
```

### Customers
```
GET  /api/customers/{customerCode}        # Get customer
GET  /api/customers?name=ACME             # Search customers
POST /api/customers                       # Create customer
```

### Sales Orders
```
GET  /api/orders/{orderNumber}            # Get order
GET  /api/orders?customer=CUST001         # List orders
POST /api/orders                          # Create order
PUT  /api/orders/{orderNumber}            # Update order
```

## ⚙️ Configuration

### appsettings.json
```json
{
  "Syspro": {
    "BaseUrl": "http://syspro-server:30000",
    "Operator": "ADMIN",
    "Password": "your-password",
    "CompanyId": "A",
    "PoolSize": 5,
    "SessionTimeout": 1200
  }
}
```

### Environment Variables (override appsettings)
```bash
Syspro__BaseUrl=http://server:30000
Syspro__Operator=ADMIN
Syspro__Password=secret
Syspro__CompanyId=A
Syspro__PoolSize=10
```

## 🔑 Key Features

### 1. Session Pooling
```csharp
// Sessions are automatically managed
// Pool maintains 5 sessions (configurable)
// Reuses sessions across requests = saves licenses!
```

### 2. Error Handling
```csharp
// All SYSPRO errors mapped to proper HTTP responses
// 400 = validation error
// 404 = not found
// 500 = server error
// Full error details in response body
```

### 3. Logging
```csharp
// Structured logging with Serilog
// Logs session usage, errors, timings
// Configure in appsettings.json
```

## 📝 Example Usage

### Query Inventory
```bash
curl http://localhost:5000/api/inventory/WIDGET-001

# Response:
{
  "stockCode": "WIDGET-001",
  "description": "Standard Widget",
  "qtyOnHand": 150,
  "qtyAvailable": 120,
  "warehouses": [
    { "warehouse": "01", "qty": 100 },
    { "warehouse": "02", "qty": 50 }
  ]
}
```

### Create Sales Order
```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerCode": "CUST001",
    "customerPo": "PO-12345",
    "lines": [
      { "stockCode": "WIDGET-001", "qty": 10, "price": 99.99 },
      { "stockCode": "WIDGET-002", "qty": 5, "price": 149.99 }
    ]
  }'

# Response:
{
  "salesOrder": "SO001234",
  "status": "created",
  "total": 1749.85
}
```

## 🔗 Related Documentation

- [../../docs/03-ENET-SOLUTIONS.md](../../docs/03-ENET-SOLUTIONS.md) - e.net API reference
- [../../docs/04-DOTNET-IMPLEMENTATION.md](../../docs/04-DOTNET-IMPLEMENTATION.md) - .NET guide
- [../../docs/11-FAQ.md](../../docs/11-FAQ.md) - Common questions
- [../postman-collections/](../postman-collections/) - Postman testing
