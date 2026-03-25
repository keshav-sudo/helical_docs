# ❓ SYSPRO Integration FAQ — 50 Most Asked Questions

> Quick answers to the questions every developer asks

---

## 📑 Table of Contents

1. [Basic Concepts (Q1-10)](#-basic-concepts)
2. [Authentication & Sessions (Q11-20)](#-authentication--sessions)
3. [Licensing (Q21-25)](#-licensing)
4. [Connectivity & Setup (Q26-35)](#-connectivity--setup)
5. [API & Code (Q36-45)](#-api--code)
6. [Production & Troubleshooting (Q46-50)](#-production--troubleshooting)

---

## 🎯 Basic Concepts

### Q1: SYSPRO kya hai?
**A:** SYSPRO ek ERP (Enterprise Resource Planning) software hai jo manufacturing aur distribution companies use karti hain. Inventory, sales orders, purchase orders, finance — sab manage karta hai.

---

### Q2: e.net kya hai?
**A:** e.net Solutions SYSPRO ka **API gateway** hai. Tumhara code SYSPRO se XML messages ke through baat karta hai via e.net. Ye SYSPRO ka official programmatic interface hai.

---

### Q3: Kya SYSPRO ka REST API hai?
**A:** SYSPRO 8+ mein limited REST API hai, but **e.net (XML over HTTP)** abhi bhi primary integration method hai with full Business Object support. Most integrations e.net use karti hain.

---

### Q4: Business Object kya hai?
**A:** Business Object (BO) ek SYSPRO component hai jo ek entity handle karta hai:
- `SORTOI` = Sales Order create karna
- `INVQRY` = Inventory query karna
- `ARSTOP` = Customer create karna

Each BO ka apna XML format hai.

---

### Q5: Kya main directly SQL mein write kar sakta hoon?
**A:** ❌ **KABHI NAHI!** SYSPRO ki business logic database mein nahi hai — e.net application server mein hai. Direct SQL write karoge to:
- Business rules skip ho jayenge
- Inventory allocations galat hongi
- Audit trail nahi banega
- Data corrupt ho sakta hai

**READ ke liye SQL OK hai. WRITE sirf e.net se!**

---

### Q6: SYSPRO Avanti kya hai?
**A:** Avanti SYSPRO ka official web UI hai (SYSPRO 8+). Browser se access kar sakte ho. But ye end-users ke liye hai, API integration ke liye nahi.

---

### Q7: Integration kyu chahiye jab Avanti hai?
**A:** Avanti se:
- eCommerce orders auto-sync nahi ho sakte
- External systems (Shopify, Salesforce) connect nahi ho sakte  
- Custom portals nahi bana sakte
- Automation/workflows limited hain

Integration in sab ke liye chahiye.

---

### Q8: Company kya hai SYSPRO mein?
**A:** SYSPRO mein "Company" = **separate database**. Agar client ke paas 3 companies hain (Company A, B, C), to 3 alag databases honge:
- `SysproCompanyA`
- `SysproCompanyB`  
- `SysproCompanyC`

Each company ka data isolated hai.

---

### Q9: Operator kya hai?
**A:** Operator = SYSPRO user account. Tumhari integration ek dedicated operator use karegi (e.g., `API_USER`). Is operator ko e.net access enabled hona chahiye.

---

### Q10: SYSPRO versions mein kya fark hai?
**A:** 
| Version | Key Difference |
|---------|---------------|
| SYSPRO 7 | e.net COM/WCF only, no REST |
| SYSPRO 8 | Avanti web UI, REST API added |
| SYSPRO 8 (2023+) | SYSPRO Harmony (cloud) |

Integration code mostly same hai, but SYSPRO 8+ has more options.

---

## 🔐 Authentication & Sessions

### Q11: SessionId kya hai?
**A:** Login karne ke baad SYSPRO ek GUID return karta hai (e.g., `A3B8D1B6-7F2E-...`). Ye **SessionId** har subsequent call mein use hoti hai. Session = logged in user.

---

### Q12: Session kab expire hoti hai?
**A:** Default 20 minutes inactivity ke baad. Each API call timer reset karta hai. Expired session use karoge to "Invalid session" error milega.

---

### Q13: Logoff karna zaroori hai?
**A:** **HAAN!** Session Logoff nahi karoge to:
- License seat block rahega
- 20 min tak koi aur use nahi kar sakta
- Limited licenses waste honge

Always `Logoff` call karo in `finally` block!

---

### Q14: Har request pe login karna chahiye?
**A:** ❌ **NAHI!** Login 500-2000ms leta hai. Har request pe login = bahut slow API.

✅ **Session Pooling use karo** — ek pool of sessions maintain karo, reuse karo.

---

### Q15: Session pool kya hai?
**A:** Multiple sessions pehle se login karke ready rakhna. Jab request aaye, pool se session lo, use karo, wapas pool mein daal do. 

File dekho: `code-samples/session-pool/SysproSessionPool.cs`

---

### Q16: Ek session multiple threads use kar sakte hain?
**A:** ❌ **NAHI!** Session thread-safe nahi hai. Concurrent use se data corruption ho sakta hai. 1 session = 1 thread at a time.

---

### Q17: Session company-specific hai?
**A:** **HAAN!** Ek session ek company se bound hai. Company A ke data ke liye Company A ki session chahiye. Multiple companies = multiple sessions.

---

### Q18: Password kahan store karoon?
**A:** 
- ❌ NEVER in `appsettings.json` (committed to Git)
- ❌ NEVER in code  
- ✅ Azure Key Vault
- ✅ AWS Secrets Manager
- ✅ Environment variables (production)
- ✅ `dotnet user-secrets` (development)

---

### Q19: Company password kya hai?
**A:** SYSPRO mein optional company-level password hota hai. Usually blank hota hai. Agar set hai, to login mein include karna padta hai.

---

### Q20: Login fail ho raha hai - kaise debug karoon?
**A:** Check karo:
1. Operator code correct hai? (Case sensitive ho sakta hai)
2. Password correct hai?
3. Company ID correct hai?
4. Operator ko e.net access enabled hai?
5. SYSPRO server reachable hai?
6. License available hai?

---

## 💰 Licensing

### Q21: SYSPRO license kaise kaam karta hai?
**A:** SYSPRO per-seat license hai:
- **Named User**: Specific person ke liye
- **Concurrent User**: Shared, anyone can use
- **e.net License**: API access ke liye

Tumhari integration e.net licenses use karti hai.

---

### Q22: Kitni licenses chahiye?
**A:**
| Scenario | Recommended |
|----------|-------------|
| Simple batch sync | 2-3 |
| Low volume real-time | 5-10 |
| High volume | 10-20+ |

Formula: Max concurrent sessions = License count

---

### Q23: "License exceeded" error kyu aa raha hai?
**A:** Maximum concurrent sessions ho gayi. Solutions:
1. Session pooling implement karo (pool size = license count)
2. Logoff calls ensure karo (sessions release ho rahe hain?)
3. Client se more licenses request karo

---

### Q24: Kya mujhe (consultant) SYSPRO license chahiye?
**A:** ❌ **NAHI!** Tum client ke SYSPRO server se connect karte ho, unki licenses use hoti hain. Tumhe SYSPRO install nahi karna.

---

### Q25: Multi-company ke liye extra licenses?
**A:** License pool shared hai across companies. But multiple companies access karne ke liye multiple sessions chahiye (1 per company), so effectively more licenses helpful hain.

---

## 🔌 Connectivity & Setup

### Q26: SYSPRO server URL kya hoga?
**A:** Format: `http://{server}:{port}/`
- Server: Client ke SYSPRO server ka hostname/IP
- Port: Usually 8080, but can be 80 or custom
- Example: `http://syspro.client.com:8080/`

IT team se poochho.

---

### Q27: Kya VPN chahiye?
**A:** Agar SYSPRO on-premise hai (client ki office mein), haan. Cloud-hosted ho to maybe not. IT team se confirm karo.

---

### Q28: Firewall rules kya chahiye?
**A:** 
- Port 8080 (or e.net port) open for HTTP
- Port 1433 open for SQL Server (if using direct SQL for reads)
- Tumhara server → SYSPRO server communication

---

### Q29: Connection test kaise karoon?
**A:** Postman/curl se:
```bash
curl -X POST http://syspro-server:8080/saborw/Logon \
  -d "Operator=TEST&Password=test&CompanyId=S&OperatorPassword=test"
```
Success = GUID return. Fail = error message.

---

### Q30: SQL Server se directly connect kar sakte hain?
**A:** **READ ke liye: HAAN** (with read-only user)
**WRITE ke liye: NAHI** (always use e.net)

Connection string IT se lo.

---

### Q31: Kaunse tools chahiye development ke liye?
**A:**
- .NET 8 SDK
- VS Code / Visual Studio
- SQL Server Management Studio (SSMS)
- Postman / Insomnia
- Git

---

### Q32: Project structure kaisa hona chahiye?
**A:**
```
src/
├── YourAPI.Api/           # Controllers
├── YourAPI.Core/          # Interfaces, DTOs
├── YourAPI.Infrastructure/  # SYSPRO client, services
```
See: `docs/04-DOTNET-IMPLEMENTATION.md`

---

### Q33: SYSPRO kaunsa .NET version support karta hai?
**A:** e.net HTTP calls kisi bhi language se kar sakte ho. .NET 6/7/8 sab work karta hai. Node.js, Python bhi work karega (XML over HTTP).

---

### Q34: Test environment kaise setup karoon?
**A:** Client se:
1. Test SYSPRO server URL
2. Test operator credentials
3. Test company ID
4. Test data (sample customers, stock codes)

---

### Q35: SSL/HTTPS chahiye?
**A:** Production mein **HAAN absolutely!** 
Development mein HTTP OK hai.

---

## 💻 API & Code

### Q36: XML kaise build karoon?
**A:**
```csharp
var xml = $@"<?xml version=""1.0""?>
<Query>
  <TableName>InvMaster</TableName>
  <Columns><Column>StockCode</Column></Columns>
</Query>";
```
Or use `XDocument` for complex XML.

---

### Q37: Response parse kaise karoon?
**A:**
```csharp
var doc = XDocument.Parse(response);
var items = doc.Descendants("Row").Select(r => new {
    Code = r.Element("StockCode")?.Value
}).ToList();
```

---

### Q38: Error check kaise karoon?
**A:**
```csharp
if (response.Contains("<ErrorDescription>")) {
    var doc = XDocument.Parse(response);
    var error = doc.Descendants("ErrorDescription").FirstOrDefault()?.Value;
    throw new SysproException(error);
}
```

---

### Q39: Retry logic kaise add karoon?
**A:** Polly package use karo:
```csharp
services.AddHttpClient<SysproClient>()
    .AddPolicyHandler(Policy
        .Handle<HttpRequestException>()
        .WaitAndRetryAsync(3, attempt => 
            TimeSpan.FromSeconds(Math.Pow(2, attempt))));
```

---

### Q40: Timeout kitna rakhoon?
**A:** 
- Default: 30 seconds
- Complex transactions: 60 seconds
- Simple queries: 15 seconds

---

### Q41: Sales Order create karne ka flow?
**A:**
1. Validate input (customer exists? stock exists?)
2. Check inventory availability (SQL read)
3. Check customer credit (SQL read)
4. Build XML (parameters + document)
5. Call e.net Transaction("SORTOI", ...)
6. Parse response, get SO number
7. Save to local DB for tracking

---

### Q42: Inventory check kaise karoon?
**A:**
```sql
SELECT StockCode, (QtyOnHand - QtyAllocated) AS Available
FROM InvWarehouse
WHERE StockCode = 'ABC' AND Warehouse = 'WH01'
```

---

### Q43: Customer credit check kaise karoon?
**A:**
```sql
SELECT Customer, CreditLimit, Balance,
       (CreditLimit - Balance) AS Available
FROM ArCustomer
WHERE Customer = 'CUST001'
```

---

### Q44: Background job kab use karoon?
**A:** Jab:
- Bulk orders process karne hon
- SYSPRO down ho sakta hai (queue orders, process later)
- Long-running operations

Use: Hangfire, or BackgroundService in .NET

---

### Q45: Local database kyu chahiye?
**A:** 
- Orders stage karne ke liye before SYSPRO sync
- Failed syncs track karne ke liye
- Audit trail maintain karne ke liye
- Retry logic ke liye

---

## 🚀 Production & Troubleshooting

### Q46: Production deploy karne se pehle kya check karoon?
**A:** See: `checklists/PRODUCTION-CHECKLIST.md`
Key items:
- [ ] Session pooling working
- [ ] Retry logic implemented
- [ ] Error handling complete
- [ ] Logging enabled
- [ ] No hardcoded credentials

---

### Q47: SYSPRO slow response de raha hai - kya karoon?
**A:**
1. Session pooling use karo (login time save)
2. READ operations SQL se karo (faster than e.net)
3. Cache frequently used data (Redis)
4. Background processing for non-urgent ops

---

### Q48: "Connection refused" error kaise fix karoon?
**A:**
1. SYSPRO server chal raha hai?
2. URL correct hai? (http vs https, port)
3. Firewall block nahi kar raha?
4. VPN connected hai?

---

### Q49: XML parsing error aa raha hai - kaise debug karoon?
**A:**
1. Raw response log karo
2. XML valid hai check karo (online validator)
3. Special characters escaped hain? (`&` → `&amp;`)
4. Encoding UTF-8 hai?

---

### Q50: Production mein monitor kya karoon?
**A:**
- API response times
- SYSPRO call success/failure rate
- Session pool utilization
- Error rates per Business Object
- Queue sizes (if using async)

Tools: Application Insights, Prometheus, Grafana

---

## 🆘 Still Have Questions?

1. Check `docs/TROUBLESHOOTING.md`
2. Check specific doc files for deep dive
3. Log everything and debug systematically

---

*Ye FAQ regularly update hota hai based on common questions.*
