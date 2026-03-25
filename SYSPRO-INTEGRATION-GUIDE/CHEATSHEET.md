# 🚀 SYSPRO Integration Cheatsheet

> **Print this page!** Everything you need on ONE page.

---

## 🔗 e.net Endpoints

```
BASE: http://{SERVER}:{PORT}/saborw/

POST /saborw/Logon        → Get SessionId
POST /saborw/Logoff       → Release session  
POST /saborw/Query        → READ data (SELECT)
POST /saborw/Transaction  → WRITE data (INSERT/UPDATE)
POST /saborw/Browse       → LIST data (paginated)
```

---

## 🔐 Authentication Flow

```
1. POST /saborw/Logon
   Body: Operator=USER&Password=PASS&CompanyId=S&OperatorPassword=PASS
   
2. Response: A3B8D1B6-7F2E-4A9C-B5D3-1E8F2A7B9C4D  ← SessionId

3. Use SessionId in all subsequent calls:
   UserId=A3B8D1B6-7F2E-4A9C-B5D3-1E8F2A7B9C4D

4. Always call Logoff when done (releases license!)
```

---

## 📦 Common Business Objects

### READ (Query/Browse)
| Code | Purpose | Example |
|------|---------|---------|
| `INVQRY` | Get stock item | Stock levels, description |
| `SORQRY` | Get sales order | Order status, lines |
| `ARSQRY` | Get customer | Credit limit, balance |
| `PORQRY` | Get purchase order | PO details |

### WRITE (Transaction)
| Code | Purpose | Example |
|------|---------|---------|
| `SORTOI` | Create/update SO | New sales order |
| `ARSTOP` | Create/update customer | New B2B customer |
| `PORTOI` | Create/update PO | New purchase order |
| `INVTMR` | Inventory receipt | GRN posting |
| `INVTMT` | Inventory transfer | Warehouse transfer |

---

## 🗄️ Key SQL Tables

```sql
-- Inventory
InvMaster     -- Item master (StockCode, Description)
InvWarehouse  -- Stock by warehouse (QtyOnHand, QtyAllocated)

-- Sales Orders  
SorMaster     -- SO header (SalesOrder, Customer, OrderDate)
SorDetail     -- SO lines (StockCode, OrderQty, Price)

-- Customers
ArCustomer    -- Customer master (Customer, Name, CreditLimit)

-- Purchase Orders
PorMasterHdr  -- PO header
PorMasterDet  -- PO lines
```

---

## 📝 XML Templates

### Login
```
POST /saborw/Logon
Content-Type: application/x-www-form-urlencoded

Operator=ADMIN&Password=secret&CompanyId=S&OperatorPassword=secret
```

### Query Inventory
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

### Create Sales Order
```xml
<!-- Parameters -->
<PostSorMaster>
  <Parameters>
    <ValidateOnly>N</ValidateOnly>
    <IgnoreWarnings>N</IgnoreWarnings>
  </Parameters>
</PostSorMaster>

<!-- Document -->
<PostSorMaster>
  <Orders>
    <OrderHeader>
      <Customer>CUST001</Customer>
      <OrderDate>2024-01-15</OrderDate>
      <Warehouse>WH01</Warehouse>
      <OrderDetails>
        <StockCode>ITEM001</StockCode>
        <OrderQty>10</OrderQty>
        <Price>25.50</Price>
      </OrderDetails>
    </OrderHeader>
  </Orders>
</PostSorMaster>
```

---

## 💻 C# Quick Code

### Login
```csharp
var content = new FormUrlEncodedContent(new Dictionary<string, string> {
    ["Operator"] = "ADMIN",
    ["Password"] = "secret",
    ["CompanyId"] = "S",
    ["OperatorPassword"] = "secret"
});
var response = await _http.PostAsync("/saborw/Logon", content);
var sessionId = await response.Content.ReadAsStringAsync();
```

### Query
```csharp
var content = new FormUrlEncodedContent(new Dictionary<string, string> {
    ["UserId"] = sessionId,
    ["BusinessObject"] = "INVQRY",
    ["XmlIn"] = xmlQuery
});
var response = await _http.PostAsync("/saborw/Query", content);
```

### Parse Response
```csharp
var doc = XDocument.Parse(xmlResponse);
var items = doc.Descendants("Row").Select(r => new {
    StockCode = r.Element("StockCode")?.Value,
    Description = r.Element("Description")?.Value
});
```

### Check Error
```csharp
if (response.Contains("<ErrorDescription>")) {
    var doc = XDocument.Parse(response);
    var error = doc.Descendants("ErrorDescription").FirstOrDefault()?.Value;
    throw new Exception(error);
}
```

---

## 🔴 Common Errors & Quick Fixes

| Error | Cause | Fix |
|-------|-------|-----|
| `Connection refused` | Server unreachable | Check URL, VPN, firewall |
| `Invalid operator` | Wrong credentials | Verify with SYSPRO admin |
| `License exceeded` | Too many sessions | Implement session pooling |
| `Session expired` | Timeout (20 min) | Re-login or use pool |
| `Customer not found` | Invalid code | Query ArCustomer first |
| `Stock not found` | Invalid code | Query InvMaster first |
| `Insufficient stock` | QtyOnHand < Order | Check InvWarehouse |
| `Credit limit exceeded` | Over limit | Check ArCustomer |

---

## 📊 Status Codes

### Sales Order Status
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

## 🔢 Ports

| Service | Port |
|---------|------|
| e.net HTTP | 8080 (or 80) |
| e.net WCF | 30661 |
| SQL Server | 1433 |

---

## 📁 Key Files in This Guide

| Need | File |
|------|------|
| First API call | `QUICK-START.md` |
| e.net connector | `code-samples/basic-enet-client/SysproEnetClient.cs` |
| Session pooling | `code-samples/session-pool/SysproSessionPool.cs` |
| Sales order service | `code-samples/order-service/SalesOrderService.cs` |
| All Business Objects | `docs/03-ENET-SOLUTIONS.md` |
| Error handling | `docs/06-ERROR-HANDLING.md` |
| Before starting | `checklists/PRE-INTEGRATION-CHECKLIST.md` |

---

## ⚡ Quick SQL Queries

```sql
-- Check stock availability
SELECT StockCode, Warehouse, QtyOnHand, QtyAllocated,
       (QtyOnHand - QtyAllocated) AS Available
FROM InvWarehouse WHERE StockCode = 'ITEM001';

-- Check customer credit
SELECT Customer, Name, CreditLimit, Balance,
       (CreditLimit - Balance) AS AvailableCredit
FROM ArCustomer WHERE Customer = 'CUST001';

-- Recent orders
SELECT TOP 10 SalesOrder, Customer, OrderDate, OrderStatus
FROM SorMaster ORDER BY OrderDate DESC;
```

---

## 🎯 Remember

1. **Session = License** - Each session uses 1 license seat
2. **Always Logoff** - Or sessions hang until timeout
3. **Use Session Pool** - Never login per request
4. **READ via SQL** - Faster than e.net Query
5. **WRITE via e.net ONLY** - Never write directly to SQL
6. **Validate First** - Check customer/stock before SYSPRO call

---

*Save this page. You'll need it every day!*
