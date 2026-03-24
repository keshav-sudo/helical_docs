# 🎯 Quick Reference Card — Most Used Things

> Print this or keep it open while coding

---

## e.net Endpoints

```
BASE URL: http://your-syspro-server:port/

Logon:       POST /saborw/Logon
Logoff:      POST /saborw/Logoff
Query:       POST /saborw/Query         (READ data)
Transaction: POST /saborw/Transaction   (WRITE data)
Browse:      POST /saborw/Browse        (LIST data)
```

---

## Common Business Objects

### For Reading (Query/Browse)

| Business Object | Purpose | Use Case |
|----------------|---------|----------|
| `INVQRY` | Inventory Query | Get stock item details |
| `INVBRW` | Inventory Browse | List/search inventory |
| `SORQRY` | Sales Order Query | Get SO details |
| `SORBRW` | Sales Order Browse | List sales orders |
| `ARSQRY` | Customer Query | Get customer details |
| `ARSBRW` | Customer Browse | List customers |
| `PORQRY` | Purchase Order Query | Get PO details |
| `PORBRW` | Purchase Order Browse | List purchase orders |

### For Writing (Transaction)

| Business Object | Purpose | Use Case |
|----------------|---------|----------|
| `SORTOI` | Sales Order Trans Input | Create/update SO |
| `ARSTOP` | Customer Setup | Create/update customer |
| `PORTOI` | Purchase Order Trans Input | Create/update PO |
| `INVTMR` | Inventory Receipt | GRN/stock receipt |
| `INVTTW` | Warehouse Transfer | Move stock |

---

## Common SQL Tables

### Inventory
```sql
InvMaster      -- Item master (StockCode, Description, etc.)
InvWarehouse   -- Stock by warehouse (QtyOnHand, QtyAllocated)
InvMovements   -- Stock transaction history
```

### Sales Orders
```sql
SorMaster      -- SO header (SalesOrder, Customer, OrderDate)
SorDetail      -- SO lines (StockCode, OrderQty, Price)
```

### Customers
```sql
ArCustomer     -- Customer master (Customer, Name, CreditLimit)
ArCustomer+    -- Extended customer data
```

### Purchase Orders
```sql
PorMasterHdr   -- PO header
PorMasterDet   -- PO lines
```

---

## Status Codes

### Sales Order Status (OrderStatus in SorMaster)
| Code | Meaning |
|------|---------|
| 1 | Open |
| 2 | In Progress |
| 3 | Partially Shipped |
| 4 | Complete |
| 8 | Cancelled |
| 9 | Suspended |

### Order Types
| Code | Meaning |
|------|---------|
| O | Order |
| Q | Quote |
| B | Blanket |
| C | Credit Note |

---

## Essential SQL Queries

### Check Stock Availability
```sql
SELECT 
    StockCode,
    Warehouse,
    QtyOnHand,
    QtyAllocated,
    (QtyOnHand - QtyAllocated) AS Available
FROM InvWarehouse
WHERE StockCode = 'YOUR-STOCK-CODE'
```

### Get Customer Details
```sql
SELECT 
    Customer,
    Name,
    CreditLimit,
    Balance,
    (CreditLimit - Balance) AS AvailableCredit
FROM ArCustomer
WHERE Customer = 'CUSTOMER-CODE'
```

### Get Sales Order with Lines
```sql
SELECT 
    h.SalesOrder,
    h.Customer,
    h.OrderDate,
    h.OrderStatus,
    d.MStockCode AS StockCode,
    d.MOrderQty AS Quantity,
    d.MPrice AS UnitPrice
FROM SorMaster h
JOIN SorDetail d ON h.SalesOrder = d.SalesOrder
WHERE h.SalesOrder = 'SO-NUMBER'
```

### Recent Orders for Customer
```sql
SELECT TOP 10
    SalesOrder,
    OrderDate,
    OrderStatus,
    MerchandiseValue
FROM SorMaster
WHERE Customer = 'CUSTOMER-CODE'
ORDER BY OrderDate DESC
```

---

## XML Templates

### Logon Request
```xml
Operator=ADMIN
Password=your-password
CompanyId=S
OperatorPassword=your-password
```

### Simple Inventory Query
```xml
<?xml version="1.0"?>
<Query>
    <TableName>InvMaster</TableName>
    <ReturnRows>100</ReturnRows>
    <Columns>
        <Column>StockCode</Column>
        <Column>Description</Column>
    </Columns>
    <Filter>StockCode LIKE 'ABC%'</Filter>
</Query>
```

### Create Sales Order (Minimal)
```xml
<!-- Parameters -->
<?xml version="1.0"?>
<PostSorMaster>
    <Parameters>
        <ValidateOnly>N</ValidateOnly>
        <IgnoreWarnings>N</IgnoreWarnings>
    </Parameters>
</PostSorMaster>

<!-- Document -->
<?xml version="1.0"?>
<PostSorMaster>
    <Orders>
        <OrderHeader>
            <Customer>CUST001</Customer>
            <OrderDate>2024-01-15</OrderDate>
            <OrderDetails>
                <StockCode>ITEM001</StockCode>
                <OrderQty>10</OrderQty>
            </OrderDetails>
        </OrderHeader>
    </Orders>
</PostSorMaster>
```

---

## C# Quick Snippets

### HttpClient Setup
```csharp
builder.Services.AddHttpClient<SysproEnetClient>(client =>
{
    client.BaseAddress = new Uri(config["Syspro:BaseUrl"]);
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

### Parse XML Response
```csharp
var doc = XDocument.Parse(xmlResponse);
var items = doc.Descendants("Row")
    .Select(row => new {
        StockCode = row.Element("StockCode")?.Value,
        Description = row.Element("Description")?.Value
    })
    .ToList();
```

### Check for Errors
```csharp
if (response.Contains("<ErrorDescription>"))
{
    var doc = XDocument.Parse(response);
    var error = doc.Descendants("ErrorDescription").FirstOrDefault()?.Value;
    throw new SysproException(error);
}
```

---

## Common Errors & Fixes

| Error | Cause | Fix |
|-------|-------|-----|
| "Connection refused" | Server unreachable | Check URL, firewall, VPN |
| "Invalid operator" | Bad credentials | Verify with SYSPRO admin |
| "License exceeded" | Too many sessions | Implement session pooling |
| "Session expired" | Timeout | Get new session, or pool |
| "Customer not found" | Invalid customer code | Query ArCustomer first |
| "Stock not found" | Invalid stock code | Query InvMaster first |
| "Insufficient stock" | QtyOnHand < OrderQty | Check InvWarehouse |
| "Credit limit exceeded" | Balance + Order > Limit | Check ArCustomer |

---

## Port Numbers

| Service | Default Port |
|---------|-------------|
| SYSPRO e.net | 8080 or 80 |
| SQL Server | 1433 |
| SYSPRO Client | 30200+ |

---

## Useful Links

- SYSPRO Help: `https://help.syspro.com`
- SYSPRO Community: `https://community.syspro.com`
- .NET Docs: `https://docs.microsoft.com/dotnet`

---

[← Back to Index](./00-INDEX.md)
