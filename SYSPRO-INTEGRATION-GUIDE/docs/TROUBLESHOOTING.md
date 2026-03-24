# 🔧 Common Problems & Solutions

> Troubleshooting guide — when things don't work

---

## 🔴 Connection Issues

### Problem: "Connection refused" or "Unable to connect"

```
Symptoms:
- HttpClient throws exception
- Cannot reach SYSPRO server
```

**Solution Checklist:**
```
□ 1. Is SYSPRO server running?
     → Ask IT to check SYSPRO services

□ 2. Is URL correct?
     → Check: http vs https, port number, path
     → Test in browser: http://server:port/saborw/Logon

□ 3. Is firewall blocking?
     → Check Windows Firewall on SYSPRO server
     → Check network firewall rules

□ 4. Do you need VPN?
     → If SYSPRO is on-premise, you may need VPN
     → Connect to VPN first, then test

□ 5. Is port correct?
     → Default: 8080, but could be 80, 443, or custom
     → Ask SYSPRO admin for exact port
```

---

### Problem: "Certificate error" or "SSL/TLS error"

```
Symptoms:
- Works in browser but not in code
- Certificate validation failed
```

**Solution (Development only!):**
```csharp
// ONLY FOR DEVELOPMENT - never in production!
var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = 
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};
var client = new HttpClient(handler);
```

**Proper Solution (Production):**
```
□ 1. Get valid SSL certificate from IT
□ 2. Install certificate in Windows certificate store
□ 3. Use proper HTTPS with valid cert
```

---

## 🔴 Authentication Issues

### Problem: "Invalid operator" or "Login failed"

```
Symptoms:
- Logon returns error message instead of SessionId
- "Operator does not exist"
```

**Solution Checklist:**
```
□ 1. Check credentials with SYSPRO admin
     → Operator code is case-sensitive in some versions
     → Password may have expired

□ 2. Check company ID
     → Usually 'S' or '001' or similar
     → Must match exactly

□ 3. Check operator has e.net access
     → In SYSPRO: Setup → Operators → Edit
     → "Allow e.net access" must be enabled

□ 4. Check license
     → Operator must have valid license type
     → "Runtime" license allows API access
```

---

### Problem: "Session expired" or "Invalid session"

```
Symptoms:
- First call works, subsequent calls fail
- Works for a while, then stops
```

**Solution:**
```csharp
// Sessions expire after 20-30 minutes of inactivity
// Solution 1: Catch and re-login
try
{
    return await _client.QueryAsync(sessionId, "INVQRY", xml);
}
catch (SysproException ex) when (ex.Message.Contains("session"))
{
    // Re-login and retry
    sessionId = await _client.LogonAsync();
    return await _client.QueryAsync(sessionId, "INVQRY", xml);
}

// Solution 2: Use session pooling (better)
// See: code-samples/session-pool/
```

---

### Problem: "License limit exceeded"

```
Symptoms:
- Login works sometimes, fails other times
- "Maximum users exceeded"
```

**Solution:**
```
□ 1. Implement session pooling (REQUIRED for production)
     → See: code-samples/session-pool/SysproSessionPool.cs

□ 2. Always call Logoff when done
     → Don't leave sessions open

□ 3. Ask for more licenses
     → e.net calls consume SYSPRO licenses
     → You may need "Runtime" or "Web" licenses
```

---

## 🔴 Data Issues

### Problem: "Customer not found" or "Invalid customer"

```
Symptoms:
- Order creation fails
- Customer code rejected
```

**Solution:**
```sql
-- First, verify customer exists
SELECT Customer, Name, CustomerOnHold
FROM ArCustomer
WHERE Customer = 'YOUR-CUSTOMER-CODE'

-- Check if on hold
-- CustomerOnHold = 'Y' means orders blocked
```

```csharp
// In code: validate before sending to SYSPRO
public async Task<bool> CustomerExistsAsync(string customerId)
{
    var sql = "SELECT 1 FROM ArCustomer WHERE Customer = @id";
    // Execute query...
}
```

---

### Problem: "Stock not found" or "Invalid stock code"

```
Symptoms:
- Order line rejected
- Stock code not recognized
```

**Solution:**
```sql
-- Verify stock code exists
SELECT StockCode, Description, StockOnHold
FROM InvMaster
WHERE StockCode = 'YOUR-STOCK-CODE'

-- Check if discontinued or on hold
```

---

### Problem: "Insufficient stock" or "Stock not available"

```
Symptoms:
- Order partially accepted
- Allocation failed
```

**Solution:**
```sql
-- Check actual availability
SELECT 
    StockCode,
    Warehouse,
    QtyOnHand,
    QtyAllocated,
    (QtyOnHand - QtyAllocated) AS Available
FROM InvWarehouse
WHERE StockCode = 'YOUR-STOCK-CODE'
  AND Warehouse = 'YOUR-WAREHOUSE'

-- Available must be >= order quantity
```

---

### Problem: "Credit limit exceeded"

```
Symptoms:
- Order rejected
- Customer over limit
```

**Solution:**
```sql
-- Check customer credit situation
SELECT 
    Customer,
    CreditLimit,
    Balance,
    (CreditLimit - Balance) AS AvailableCredit
FROM ArCustomer
WHERE Customer = 'YOUR-CUSTOMER'

-- AvailableCredit must be >= order value
```

---

## 🔴 XML Issues

### Problem: "XML parsing error" or "Invalid XML"

```
Symptoms:
- SYSPRO rejects request
- "Malformed XML"
```

**Solution Checklist:**
```
□ 1. Check XML is well-formed
     → Use online XML validator
     → Check all tags are closed

□ 2. Check encoding
     → Use UTF-8
     → No special characters without encoding

□ 3. Check escaping
     → & becomes &amp;
     → < becomes &lt;
     → > becomes &gt;
     → " becomes &quot;

□ 4. Log the actual XML sent
     → Debug by seeing exact request
```

```csharp
// Always encode user input
var description = WebUtility.HtmlEncode(userInput);
var xml = $"<Description>{description}</Description>";
```

---

### Problem: "Required field missing"

```
Symptoms:
- "Field X is required"
- Transaction rejected
```

**Solution:**
```
□ 1. Check SYSPRO documentation for required fields
     → Different BOs have different requirements

□ 2. Common required fields for SORTOI:
     → Customer
     → At least one OrderDetails with StockCode + OrderQty

□ 3. Log and compare with working example
     → See docs/03-ENET-SOLUTIONS.md for XML examples
```

---

## 🔴 Performance Issues

### Problem: Slow response times (> 5 seconds)

```
Symptoms:
- API calls take too long
- Timeouts
```

**Solution Checklist:**
```
□ 1. Implement session pooling
     → Logon takes 500-2000ms each time
     → Reuse sessions instead

□ 2. Use direct SQL for reads
     → e.net Query is slower than direct SQL
     → For read-only: SELECT directly from SYSPRO DB

□ 3. Cache frequently accessed data
     → Customer list, Stock list
     → Use Redis or in-memory cache

□ 4. Use async processing
     → Queue orders, process in background
     → Don't make user wait for SYSPRO
```

---

### Problem: Timeouts during high load

```
Symptoms:
- Works with few users, fails with many
- TaskCanceledException
```

**Solution:**
```csharp
// 1. Increase timeout
var client = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(60)  // Increase from default 30
};

// 2. Implement retry with Polly
services.AddHttpClient<SysproClient>()
    .AddPolicyHandler(Policy
        .Handle<HttpRequestException>()
        .WaitAndRetryAsync(3, attempt => 
            TimeSpan.FromSeconds(Math.Pow(2, attempt))));

// 3. Implement circuit breaker
.AddPolicyHandler(Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1)));
```

---

## 🔴 Debugging Tips

### Enable Detailed Logging

```csharp
// In Program.cs
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// In your service
_logger.LogDebug("Sending to SYSPRO: {Xml}", xmlRequest);
_logger.LogDebug("Received from SYSPRO: {Xml}", xmlResponse);
```

### Test with Postman/Insomnia

```
1. Create new POST request
2. URL: http://your-syspro:8080/saborw/Logon
3. Body: x-www-form-urlencoded
   - Operator: ADMIN
   - Password: your-password
   - CompanyId: S
   - OperatorPassword: your-password
4. Send and check response
```

### Check SYSPRO Logs

```
Location: C:\SYSPRO7\Base\Logs\ (or similar)
Files: e.net*.log, error*.log

Ask SYSPRO admin to check these when debugging
```

---

## 🆘 Still Stuck?

1. **Check the detailed docs** — [docs/06-ERROR-HANDLING.md](./06-ERROR-HANDLING.md)
2. **Ask SYSPRO admin** — They can check server-side logs
3. **Log everything** — Request XML, Response XML, Timestamps
4. **Test in isolation** — Use Postman before code

---

[← Back to Index](./00-INDEX.md)
