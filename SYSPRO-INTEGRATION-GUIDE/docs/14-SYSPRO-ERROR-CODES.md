# 🔴 SYSPRO Error Codes & Messages — Complete Reference

> Every error you'll encounter with cause and fix

---

## 📑 Quick Navigation

1. [Authentication Errors](#-authentication-errors)
2. [Session Errors](#-session-errors)
3. [License Errors](#-license-errors)
4. [Connection Errors](#-connection-errors)
5. [Customer Errors](#-customer-errors)
6. [Inventory/Stock Errors](#-inventorystock-errors)
7. [Sales Order Errors](#-sales-order-errors)
8. [Purchase Order Errors](#-purchase-order-errors)
9. [XML/Format Errors](#-xmlformat-errors)
10. [General Business Errors](#-general-business-errors)

---

## 🔐 Authentication Errors

### `Invalid operator`

```xml
<ErrorDescription>Invalid operator</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Operator code doesn't exist | Check operator code spelling |
| Operator code case mismatch | Try exact case from SYSPRO |
| Operator disabled | Ask SYSPRO admin to enable |

---

### `Invalid password`

```xml
<ErrorDescription>Invalid password</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Wrong password | Verify with SYSPRO admin |
| Password expired | Reset password in SYSPRO |
| Copy-paste whitespace | Trim password string |

---

### `Operator does not have e.net access`

```xml
<ErrorDescription>Operator does not have e.net access</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| e.net not enabled for operator | SYSPRO Admin → Operator Maintenance → Enable "e.net Solutions" |

---

### `Invalid company`

```xml
<ErrorDescription>Invalid company</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Company ID doesn't exist | Check company code (usually "S", "A", "001") |
| Operator has no access to company | SYSPRO Admin → add company access to operator |

---

### `Company password required`

```xml
<ErrorDescription>Company password required</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Company has password protection | Include `CompanyPassword` in login request |

---

## 🔑 Session Errors

### `Invalid Logon ID` / `Invalid session`

```xml
<ErrorDescription>Invalid Logon ID</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Session expired (20 min timeout) | Re-login, get new session |
| Session already logged off | Re-login |
| Session never existed | Check SessionId value |

**Code Fix:**
```csharp
catch (SysproException ex) when (ex.Message.Contains("Invalid Logon") || 
                                   ex.Message.Contains("Invalid session"))
{
    // Invalidate session in pool, get new one
    await _sessionPool.InvalidateSession(sessionId);
    var newSession = await _sessionPool.AcquireSessionAsync();
    // Retry operation
}
```

---

### `Session timeout`

```xml
<ErrorDescription>Session timeout</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| No activity for 20+ minutes | Use session pooling with keep-alive |
| Long-running operation | Break into smaller operations |

---

### `Session in use`

```xml
<ErrorDescription>Session in use by another process</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Same session used by multiple threads | 1 session = 1 thread only |
| Concurrent requests on same session | Use session pooling with proper locking |

---

## 💰 License Errors

### `Maximum users exceeded` / `No licenses available`

```xml
<ErrorDescription>Maximum users exceeded</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| All e.net licenses in use | Implement session pooling |
| Sessions not being released | Ensure Logoff in finally blocks |
| Need more licenses | Request additional licenses from client |

**Prevention:**
```csharp
public class SysproSessionPool
{
    private readonly int _maxPoolSize; // = license count
    
    public SysproSessionPool(int licenseCount)
    {
        _maxPoolSize = licenseCount; // Never exceed this
    }
}
```

---

### `License not valid for this module`

```xml
<ErrorDescription>License not valid for this module</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Client doesn't have license for module | Check which modules are licensed |
| Trying to use unlicensed feature | Remove that feature or get license |

---

## 🔌 Connection Errors

### `Connection refused`

```
System.Net.Sockets.SocketException: Connection refused
```

| Cause | Fix |
|-------|-----|
| SYSPRO server not running | Ask IT to start SYSPRO services |
| Wrong URL/port | Verify URL with IT (usually port 8080) |
| Firewall blocking | Open firewall for e.net port |
| VPN not connected | Connect to VPN first |

**Debug Steps:**
```bash
# 1. Ping server
ping syspro-server

# 2. Test port
telnet syspro-server 8080

# 3. Test in browser
curl http://syspro-server:8080/saborw/Logon
```

---

### `Connection timeout`

```
System.TimeoutException: The operation has timed out
```

| Cause | Fix |
|-------|-----|
| Network slow | Increase timeout |
| SYSPRO overloaded | Reduce concurrent requests |
| Large response | Paginate results |

**Code Fix:**
```csharp
var client = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(60) // Increase from default 30
};
```

---

### `SSL/TLS error`

```
System.Net.Http.HttpRequestException: The SSL connection could not be established
```

| Cause | Fix |
|-------|-----|
| Invalid SSL certificate | Use valid certificate (production) |
| Self-signed cert in dev | Add certificate bypass (dev only!) |

**Dev Workaround (NOT for production!):**
```csharp
var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = 
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};
```

---

## 👤 Customer Errors

### `Customer not on file` / `Invalid customer`

```xml
<ErrorDescription>Customer 0000100 not on file</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Customer code doesn't exist | Query ArCustomer first to validate |
| Wrong customer code format | Check padding (0000100 vs 100) |

**Prevention:**
```csharp
public async Task<bool> CustomerExistsAsync(string customerId)
{
    var sql = "SELECT 1 FROM ArCustomer WHERE Customer = @id";
    // Execute and check
}

// Always validate before creating order
if (!await CustomerExistsAsync(request.CustomerId))
    throw new ValidationException($"Customer {request.CustomerId} not found");
```

---

### `Customer on hold`

```xml
<ErrorDescription>Customer 0000100 is on credit hold</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Customer has credit issues | Check with AR department |
| Manual hold placed | Remove hold in SYSPRO |

**Check Before Order:**
```sql
SELECT Customer, CreditStatus, CustomerOnHold
FROM ArCustomer
WHERE Customer = 'CUST001'
-- CreditStatus = 'H' means hold
-- CustomerOnHold = 'Y' means hold
```

---

### `Credit limit exceeded`

```xml
<ErrorDescription>Order value exceeds available credit. Available: 1000.00, Order: 5000.00</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Order > available credit | Reduce order or get credit approval |

**Pre-Check:**
```sql
SELECT 
    Customer,
    CreditLimit,
    Balance,
    (CreditLimit - Balance) AS AvailableCredit
FROM ArCustomer
WHERE Customer = 'CUST001'
```

---

## 📦 Inventory/Stock Errors

### `Stock code not on file`

```xml
<ErrorDescription>Stock code INVALID not on file</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Stock code doesn't exist | Query InvMaster first |
| Wrong stock code | Check exact code from SYSPRO |

---

### `Insufficient stock` / `Stock not available`

```xml
<ErrorDescription>Insufficient stock for A100 in WH01. Available: 5, Requested: 10</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Not enough inventory | Reduce quantity or backorder |
| Wrong warehouse | Check correct warehouse |

**Pre-Check:**
```sql
SELECT 
    StockCode,
    Warehouse,
    QtyOnHand,
    QtyAllocated,
    (QtyOnHand - QtyAllocated) AS Available
FROM InvWarehouse
WHERE StockCode = 'A100' AND Warehouse = 'WH01'
```

---

### `Stock code on hold`

```xml
<ErrorDescription>Stock code A100 is on hold</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Item marked as held | Check with inventory team |

```sql
SELECT StockCode, StockOnHold FROM InvMaster WHERE StockCode = 'A100'
-- StockOnHold = 'Y' means on hold
```

---

### `Invalid warehouse`

```xml
<ErrorDescription>Warehouse XX not on file</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Warehouse code doesn't exist | Check valid warehouse codes |

---

## 📋 Sales Order Errors

### `Customer PO number already exists`

```xml
<ErrorDescription>Customer PO number PO-001 already exists on Sales Order 000123</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Duplicate PO number | Use unique PO number or skip validation |

**Parameter to allow duplicates:**
```xml
<Parameters>
    <AllowPoNumberDuplicates>Y</AllowPoNumberDuplicates>
</Parameters>
```

---

### `Order line quantity must be greater than zero`

```xml
<ErrorDescription>Order line quantity must be greater than zero</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Zero or negative quantity | Validate quantity > 0 before sending |

---

### `Price is zero not allowed`

```xml
<ErrorDescription>Zero price not allowed for stock code A100</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Price = 0 and SYSPRO configured to reject | Set price > 0 or allow zero price |

**Parameter to allow zero price:**
```xml
<Parameters>
    <AllowZeroPrice>Y</AllowZeroPrice>
</Parameters>
```

---

### `Warehouse is required`

```xml
<ErrorDescription>Warehouse is a required field</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Missing warehouse in XML | Include `<Warehouse>WH01</Warehouse>` |

---

### `Sales order not found`

```xml
<ErrorDescription>Sales order 000123 not found</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Order doesn't exist | Check order number |
| Order in different company | Login to correct company |

---

## 📦 Purchase Order Errors

### `Supplier not on file`

```xml
<ErrorDescription>Supplier SUP001 not on file</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Invalid supplier code | Query ApSupplier first |

---

### `Supplier on hold`

```xml
<ErrorDescription>Supplier SUP001 is on hold</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Supplier marked as held | Check with AP department |

---

## 📝 XML/Format Errors

### `XML parsing error` / `Malformed XML`

```xml
<ErrorDescription>XML parsing error at line X</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Invalid XML syntax | Validate XML before sending |
| Unescaped special characters | Escape `&`, `<`, `>`, `"` |
| Wrong encoding | Use UTF-8 |

**Escape Characters:**
```csharp
var escaped = SecurityElement.Escape(userInput);
// & → &amp;
// < → &lt;
// > → &gt;
// " → &quot;
```

---

### `Required field missing`

```xml
<ErrorDescription>Required field 'Customer' not supplied</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Missing required XML element | Check BO documentation for required fields |

**Common Required Fields for SORTOI:**
- `Customer`
- At least one `OrderDetails` with `StockCode` and `OrderQty`

---

### `Invalid date format`

```xml
<ErrorDescription>Invalid date format for OrderDate</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Wrong date format | Use `YYYY-MM-DD` format |

```csharp
var date = DateTime.Now.ToString("yyyy-MM-dd"); // 2024-03-15
```

---

### `Invalid numeric value`

```xml
<ErrorDescription>Invalid numeric value for Quantity</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Non-numeric text in number field | Validate data types |
| Comma instead of period | Use period for decimals |

```csharp
var price = 25.50m.ToString(CultureInfo.InvariantCulture); // "25.50" not "25,50"
```

---

## ⚠️ General Business Errors

### `Transaction rolled back`

```xml
<ErrorDescription>Transaction rolled back due to previous errors</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Earlier error in multi-line transaction | Check all line errors |
| ApplyIfEntireDocumentValid = Y | Fix all errors and retry |

---

### `Business object not found`

```xml
<ErrorDescription>Business object INVALID not found</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Wrong BO code | Use correct code (SORTOI, INVQRY, etc.) |

---

### `Access denied` / `Permission denied`

```xml
<ErrorDescription>Operator does not have permission for SORTOI</ErrorDescription>
```

| Cause | Fix |
|-------|-----|
| Operator lacks BO permission | SYSPRO Admin → add BO permission to operator group |

---

## 🔧 Error Handling Pattern

```csharp
public async Task<Result> CallSysproAsync(...)
{
    try
    {
        var response = await _client.TransactionAsync(...);
        
        // Check for errors in response
        if (response.Contains("<ErrorDescription>"))
        {
            var error = ExtractError(response);
            _logger.LogWarning("SYSPRO error: {Error}", error);
            
            // Categorize and handle
            return error switch
            {
                var e when e.Contains("Invalid Logon") => 
                    Result.Fail(ErrorType.SessionExpired, e),
                var e when e.Contains("License") => 
                    Result.Fail(ErrorType.LicenseExceeded, e),
                var e when e.Contains("Customer") && e.Contains("not on file") => 
                    Result.Fail(ErrorType.InvalidCustomer, e),
                var e when e.Contains("Stock") && e.Contains("not on file") => 
                    Result.Fail(ErrorType.InvalidStock, e),
                var e when e.Contains("Insufficient stock") => 
                    Result.Fail(ErrorType.InsufficientStock, e),
                var e when e.Contains("Credit") => 
                    Result.Fail(ErrorType.CreditExceeded, e),
                _ => Result.Fail(ErrorType.BusinessError, e)
            };
        }
        
        return Result.Success(response);
    }
    catch (HttpRequestException ex)
    {
        return Result.Fail(ErrorType.ConnectionError, ex.Message);
    }
    catch (TimeoutException ex)
    {
        return Result.Fail(ErrorType.Timeout, ex.Message);
    }
}
```

---

## 📊 Error Code Categories

| Category | HTTP Status | Retry? |
|----------|-------------|--------|
| Authentication | 401 | No (re-login) |
| Session | 401 | Yes (new session) |
| License | 503 | Yes (with backoff) |
| Connection | 503 | Yes (with backoff) |
| Validation | 400 | No (fix input) |
| Business Logic | 422 | No (fix data) |
| Server Error | 500 | Maybe |

---

*This reference is based on common SYSPRO 7/8 error messages. Your version may have slight variations.*
