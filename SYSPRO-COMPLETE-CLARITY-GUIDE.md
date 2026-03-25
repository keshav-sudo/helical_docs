# 🔥 SYSPRO Integration — COMPLETE CLARITY GUIDE

> **FOR**: Consulting companies starting SYSPRO integration projects  
> **GOAL**: 100% clarity on connectors, APIs, authentication, licensing & prerequisites

---

## 📌 TABLE OF CONTENTS

1. [Quick Answer Summary](#-quick-answer-summary)
2. [Where Are The Connectors?](#-where-are-the-connectors)
3. [Where Are The APIs?](#-where-are-the-apis)
4. [How SYSPRO Authentication Works](#-how-syspro-authentication-works)
5. [Licensing - Per Company Requirements](#-licensing---per-company-requirements)
6. [What Consulting Companies Need](#-what-consulting-companies-need-to-start)
7. [Complete Architecture Overview](#-complete-architecture-overview)
8. [Step-by-Step: First API Call](#-step-by-step-first-api-call)

---

## 🎯 QUICK ANSWER SUMMARY

| Question | Answer |
|----------|--------|
| **Where is the connector?** | `code-samples/basic-enet-client/SysproEnetClient.cs` |
| **Where are the APIs?** | SYSPRO exposes `e.net Solutions` endpoints. Your .NET code calls them. |
| **How does auth work?** | Operator + Password → Login → Get SessionId (GUID) → Use in all calls → Logoff |
| **Do we need license per company?** | YES. Each SYSPRO company = separate database. Need credentials per company. |
| **What do consultants need?** | SYSPRO credentials, e.net URL, Company ID, test operator, read-only SQL access |

---

## 🔌 WHERE ARE THE CONNECTORS?

### The Main Connector File

```
📁 SYSPRO-INTEGRATION-GUIDE/
   └── 📁 code-samples/
       └── 📁 basic-enet-client/
           └── 📄 SysproEnetClient.cs    ← THIS IS YOUR CONNECTOR
```

### What The Connector Does

```csharp
// File: code-samples/basic-enet-client/SysproEnetClient.cs

public class SysproEnetClient
{
    // 1. LOGIN - Get Session ID
    public async Task<SysproSession> LogonAsync(...)
    
    // 2. QUERY - Read data (inventory, orders, customers)
    public async Task<string> QueryAsync(sessionId, businessObject, xmlParams)
    
    // 3. TRANSACTION - Create/Update data (sales orders, POs)
    public async Task<string> TransactionAsync(sessionId, businessObject, xmlParams, xmlDoc)
    
    // 4. LOGOFF - Release session (ALWAYS do this!)
    public async Task LogoffAsync(sessionId)
}
```

### Session Pool (For Production)

```
📁 code-samples/
   └── 📁 session-pool/
       └── 📄 SysproSessionPool.cs    ← MANAGES MULTIPLE SESSIONS
```

Why you need this:
- Each login takes 500-2000ms
- Each active session = 1 license seat consumed
- Pool reuses sessions efficiently

---

## 🔗 WHERE ARE THE APIs?

### SYSPRO's API = e.net Solutions

SYSPRO does NOT have a modern REST API in the traditional sense. It has **e.net Solutions** — an XML-over-HTTP gateway.

```
┌────────────────────────────────────────────────────────────────┐
│                    SYSPRO API ENDPOINTS                         │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Base URL: http://{SYSPRO-SERVER}:{PORT}/saborw/               │
│                                                                 │
│  ENDPOINT        │  PURPOSE                │  METHOD            │
│  ─────────────────────────────────────────────────────────────  │
│  /saborw/Logon   │  Authenticate, get      │  POST              │
│                  │  SessionId              │  Form data         │
│                  │                         │                    │
│  /saborw/Logoff  │  Release session        │  POST              │
│                  │  Free up license        │  UserId=SessionId  │
│                  │                         │                    │
│  /saborw/Query   │  READ data              │  POST              │
│                  │  (Inventory, Orders)    │  XML request       │
│                  │                         │                    │
│  /saborw/        │  WRITE data             │  POST              │
│  Transaction     │  (Create SO, PO)        │  XML request       │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
```

### Business Objects (API Operations)

| Code | What It Does | Example Use |
|------|--------------|-------------|
| `INVQRY` | Query Inventory | Check stock levels |
| `SORTOI` | Create Sales Order | New customer order |
| `SORQRY` | Query Sales Order | Get order status |
| `ARSTOP` | Create/Update Customer | New B2B customer |
| `ARSQRY` | Query Customer | Get customer details |
| `PORTOI` | Create Purchase Order | Order from supplier |
| `INVTMR` | Inventory Receipt | Goods received |
| `INVTMT` | Inventory Transfer | Move between warehouses |

### Your API Layer (What YOU Build)

```
┌─────────────────────────────────────────────────────────────────┐
│                 YOUR MIDDLEWARE ARCHITECTURE                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  External World              Your API                 SYSPRO    │
│  ─────────────              ────────                 ──────     │
│                                                                  │
│  React/Angular  ─────►  .NET 8 Web API  ─────►  e.net Solutions │
│  Mobile App              (Your Code)              (SYSPRO)       │
│  Shopify                                                         │
│  Salesforce              ┌─────────────┐         ┌───────────┐  │
│                          │ Controllers │         │ Business  │  │
│  REST/JSON               │ /api/orders │   XML   │ Objects   │  │
│  ◄─────────────         │ /api/stock  │ ──────► │ SORTOI    │  │
│                          │ /api/cust   │         │ INVQRY    │  │
│                          └─────────────┘         └───────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🔐 HOW SYSPRO AUTHENTICATION WORKS

### The Authentication Flow

```
┌────────────────────────────────────────────────────────────────┐
│               SYSPRO AUTHENTICATION FLOW                        │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│  STEP 1: LOGIN                                                  │
│  ─────────────                                                  │
│  Your Code sends:                                               │
│  ┌──────────────────────────────────────┐                      │
│  │ POST /saborw/Logon                   │                      │
│  │ Content-Type: x-www-form-urlencoded  │                      │
│  │                                       │                      │
│  │ Operator=API_USER                    │ ← SYSPRO user        │
│  │ Password=secret123                   │ ← User password      │
│  │ CompanyId=S                          │ ← Company code       │
│  │ OperatorPassword=secret123           │ ← Same as password   │
│  └──────────────────────────────────────┘                      │
│                                                                 │
│  SYSPRO returns:                                                │
│  ┌──────────────────────────────────────┐                      │
│  │ A3B8D1B6-7F2E-4A9C-B5D3-1E8F2A7B9C4D │ ← SessionId (GUID)  │
│  └──────────────────────────────────────┘                      │
│                                                                 │
│  ─────────────────────────────────────────────────────────────  │
│                                                                 │
│  STEP 2: USE SESSION ID IN ALL CALLS                           │
│  ────────────────────────────────────                           │
│  ┌──────────────────────────────────────┐                      │
│  │ POST /saborw/Query                    │                      │
│  │                                       │                      │
│  │ UserId=A3B8D1B6-7F2E-4A9C...        │ ← SessionId          │
│  │ BusinessObject=INVQRY                │                      │
│  │ XmlIn=<Query>...</Query>             │                      │
│  └──────────────────────────────────────┘                      │
│                                                                 │
│  ─────────────────────────────────────────────────────────────  │
│                                                                 │
│  STEP 3: LOGOFF (CRITICAL!)                                    │
│  ─────────────────────────                                      │
│  ┌──────────────────────────────────────┐                      │
│  │ POST /saborw/Logoff                   │                      │
│  │                                       │                      │
│  │ UserId=A3B8D1B6-7F2E-4A9C...        │ ← Release license    │
│  └──────────────────────────────────────┘                      │
│                                                                 │
│  If you DON'T logoff:                                          │
│  • Session stays active until timeout (20 min default)         │
│  • Each orphan session = wasted license ($$$)                  │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
```

### What SYSPRO Validates on Login

1. ✅ Operator exists in `AdmOperator` table
2. ✅ Password matches hash
3. ✅ Operator is enabled for e.net access
4. ✅ Company exists and operator has access
5. ✅ e.net license seat is available
6. ✅ Operator permissions for business objects

### Session Rules (CRITICAL)

| Rule | What Happens If Violated |
|------|--------------------------|
| **1 Session = 1 License Seat** | Extra logins fail with "No licenses available" |
| **Session Timeout: 20 min** | Session becomes invalid after inactivity |
| **ALWAYS LOGOFF** | Orphaned sessions waste licenses |
| **1 Session per Thread** | Concurrent use causes corruption |
| **Session = 1 Company** | Need separate session per company |

---

## 💰 LICENSING - PER COMPANY REQUIREMENTS

### How SYSPRO Licensing Works

```
┌────────────────────────────────────────────────────────────────┐
│               SYSPRO LICENSING MODEL                            │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│  LICENSE STRUCTURE:                                             │
│  ──────────────────                                             │
│                                                                 │
│  SYSPRO License Server                                          │
│  ├── Desktop Client Licenses (SYSPRO GUI users)                │
│  ├── e.net Solutions Licenses ← FOR YOUR INTEGRATION           │
│  ├── SYSPRO Avanti Licenses (Web UI users)                     │
│  └── Report Writer Licenses                                     │
│                                                                 │
│  EACH e.net LICENSE = 1 CONCURRENT SESSION                     │
│  ──────────────────────────────────────────                     │
│                                                                 │
│  If your client has 5 e.net licenses:                          │
│  • You can have MAX 5 active sessions at same time             │
│  • Session pool should be configured for max 5                 │
│  • Login #6 will fail with "No licenses available"             │
│                                                                 │
│  COMPANY STRUCTURE:                                             │
│  ──────────────────                                             │
│                                                                 │
│  Each "Company" in SYSPRO = SEPARATE DATABASE                  │
│                                                                 │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐       │
│  │ Company A    │   │ Company B    │   │ Company C    │       │
│  │ (Database A) │   │ (Database B) │   │ (Database C) │       │
│  │              │   │              │   │              │       │
│  │ Own customers│   │ Own customers│   │ Own customers│       │
│  │ Own inventory│   │ Own inventory│   │ Own inventory│       │
│  │ Own orders   │   │ Own orders   │   │ Own orders   │       │
│  └──────────────┘   └──────────────┘   └──────────────┘       │
│         │                  │                  │                │
│         └──────────────────┴──────────────────┘                │
│                           │                                     │
│                  Shared License Pool                            │
│                  (e.net seats)                                  │
│                                                                 │
│  TO ACCESS MULTIPLE COMPANIES:                                  │
│  • Need separate session for each company                      │
│  • Each session consumes 1 license                             │
│  • Operator must have permissions for each company             │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
```

### Licensing Questions & Answers

| Question | Answer |
|----------|--------|
| **Does each client company need their own SYSPRO?** | YES. SYSPRO is installed on the client's infrastructure |
| **Do we (consultant) need a SYSPRO license?** | NO. You connect to CLIENT'S SYSPRO using their licenses |
| **Can we share sessions?** | NO. Each concurrent request needs its own session |
| **What if licenses run out?** | Implement session pooling. Pool max = license count |
| **Multi-company integration?** | Need credentials for each company. 1 session per company |

### License Count Recommendations

| Integration Type | Minimum e.net Licenses |
|-----------------|------------------------|
| Simple sync (batch) | 2-3 |
| Real-time integration (low volume) | 5-10 |
| High-volume integration | 10-20+ |
| Multi-company | Multiply by number of companies |

---

## 📋 WHAT CONSULTING COMPANIES NEED TO START

### Pre-Requisites Checklist

```
┌────────────────────────────────────────────────────────────────┐
│         CHECKLIST: BEFORE STARTING SYSPRO INTEGRATION           │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│  FROM CLIENT'S IT TEAM:                                        │
│  ───────────────────────                                        │
│  [ ] SYSPRO Server URL (e.g., http://syspro.client.com:8080/)  │
│  [ ] SYSPRO Version (7.0, 8.0, etc.)                           │
│  [ ] Network access (VPN if needed)                            │
│  [ ] Firewall rules (port 8080 or custom e.net port)          │
│  [ ] SQL Server connection for read-only queries               │
│                                                                 │
│  FROM CLIENT'S SYSPRO ADMIN:                                   │
│  ────────────────────────────                                   │
│  [ ] Company ID (e.g., "S", "A", "001")                        │
│  [ ] Test Operator Code (e.g., "API_TEST")                     │
│  [ ] Test Operator Password                                     │
│  [ ] Operator must be e.net enabled                            │
│  [ ] Operator permissions for required Business Objects:       │
│      [ ] SORTOI - if creating sales orders                     │
│      [ ] INVQRY - if reading inventory                         │
│      [ ] ARSQRY/ARSTOP - if reading/creating customers         │
│      [ ] (other BOs as needed)                                  │
│  [ ] Test warehouse code (e.g., "WH01")                        │
│  [ ] Test customer code (for order testing)                    │
│  [ ] Test stock codes (for order testing)                      │
│                                                                 │
│  YOUR DEV ENVIRONMENT:                                          │
│  ─────────────────────                                          │
│  [ ] .NET 8 SDK installed                                       │
│  [ ] VS Code / Visual Studio                                    │
│  [ ] SQL Server Management Studio (for database exploration)   │
│  [ ] Postman / Insomnia (API testing)                          │
│  [ ] VPN client (if required)                                   │
│                                                                 │
│  DOCUMENTATION TO REQUEST:                                      │
│  ──────────────────────────                                     │
│  [ ] List of SYSPRO modules client uses                        │
│  [ ] Custom fields they've added                                │
│  [ ] Special business rules/workflows                           │
│  [ ] Existing integrations (if any)                             │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
```

### First Connection Test

```bash
# Test 1: Can you reach SYSPRO?
curl -X POST http://syspro-server:8080/saborw/Logon \
  -d "Operator=API_TEST" \
  -d "Password=testpassword" \
  -d "CompanyId=S" \
  -d "OperatorPassword=testpassword"

# If successful, returns SessionId (GUID):
# A3B8D1B6-7F2E-4A9C-B5D3-1E8F2A7B9C4D

# If error, returns error message:
# "Invalid Operator" / "Invalid Password" / "No licenses available"
```

---

## 🏗️ COMPLETE ARCHITECTURE OVERVIEW

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                        COMPLETE INTEGRATION ARCHITECTURE                       │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                               │
│   EXTERNAL SYSTEMS                YOUR MIDDLEWARE                 SYSPRO      │
│   ================                ===============                 ======      │
│                                                                               │
│   ┌───────────────┐                                                          │
│   │ React/Angular │                                                          │
│   │ Frontend      │─────┐                                                    │
│   └───────────────┘     │                                                    │
│                         │       ┌───────────────────────────────┐            │
│   ┌───────────────┐     │       │      .NET 8 WEB API           │            │
│   │ Mobile App    │─────┼──────►│                               │            │
│   └───────────────┘     │       │  ┌─────────────────────────┐  │            │
│                         │       │  │  Controllers             │  │            │
│   ┌───────────────┐     │       │  │  • /api/orders          │  │            │
│   │ Shopify       │─────┤       │  │  • /api/inventory       │  │            │
│   └───────────────┘     │       │  │  • /api/customers       │  │            │
│                         │       │  └───────────┬─────────────┘  │            │
│   ┌───────────────┐     │       │              │                │            │
│   │ Salesforce    │─────┘       │  ┌───────────▼─────────────┐  │            │
│   └───────────────┘             │  │  Services               │  │            │
│                                 │  │  • SalesOrderService    │  │            │
│      JSON/REST                  │  │  • InventoryService     │  │            │
│        ◄──────                  │  │  • CustomerService      │  │            │
│                                 │  └───────────┬─────────────┘  │            │
│                                 │              │                │            │
│                                 │  ┌───────────▼─────────────┐  │            │
│                                 │  │  SysproEnetClient       │  │   XML      │
│                                 │  │  (THE CONNECTOR)        │──┼──────────► │
│                                 │  │                         │  │            │
│                                 │  │  • LogonAsync()         │  │            │
│                                 │  │  • QueryAsync()         │  │            │
│                                 │  │  • TransactionAsync()   │  │            │
│                                 │  │  • LogoffAsync()        │  │   ┌──────┐ │
│                                 │  └───────────┬─────────────┘  │   │e.net │ │
│                                 │              │                │   │Solut.│ │
│                                 │  ┌───────────▼─────────────┐  │   └───┬──┘ │
│                                 │  │  SysproSessionPool      │  │       │    │
│                                 │  │  (License Management)   │  │       │    │
│                                 │  └─────────────────────────┘  │       │    │
│                                 │                               │       │    │
│                                 └───────────────────────────────┘       │    │
│                                                                         │    │
│                                                                         ▼    │
│                                                               ┌─────────────┐│
│                                                               │ SQL SERVER  ││
│                                                               │             ││
│                                                               │ • SorMaster ││
│                                                               │ • SorDetail ││
│                                                               │ • InvMaster ││
│                                                               │ • ArCustomer││
│                                                               └─────────────┘│
│                                                                               │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 🚀 STEP-BY-STEP: FIRST API CALL

### 1. Create Project

```bash
mkdir syspro-integration && cd syspro-integration
dotnet new webapi -n SysproAPI -o ./src/SysproAPI
cd src/SysproAPI
dotnet add package Polly
dotnet add package Serilog.AspNetCore
```

### 2. Configure appsettings.json

```json
{
  "Syspro": {
    "BaseUrl": "http://YOUR-SYSPRO-SERVER:8080/",
    "CompanyId": "S",
    "DefaultOperator": "API_USER",
    "DefaultPassword": "your-password",
    "DefaultWarehouse": "WH01"
  }
}
```

### 3. Copy The Connector

Copy `SysproEnetClient.cs` from:
```
SYSPRO-INTEGRATION-GUIDE/code-samples/basic-enet-client/SysproEnetClient.cs
```

### 4. Create Your First API Endpoint

```csharp
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly SysproEnetClient _client;

    public InventoryController(SysproEnetClient client) => _client = client;

    [HttpGet("{stockCode}")]
    public async Task<IActionResult> GetStock(string stockCode)
    {
        // 1. Login
        var session = await _client.LogonAsync();
        
        try
        {
            // 2. Query
            var xml = $@"<Query><StockCode>{stockCode}</StockCode></Query>";
            var result = await _client.QueryAsync(session.SessionId, "INVQRY", xml);
            
            return Ok(new { success = true, data = result });
        }
        finally
        {
            // 3. ALWAYS logoff
            await _client.LogoffAsync(session.SessionId);
        }
    }
}
```

### 5. Test

```bash
dotnet run
curl https://localhost:5001/api/inventory/A100
```

---

## 📚 FILE REFERENCE

| Need | File Location |
|------|---------------|
| **e.net Connector** | `code-samples/basic-enet-client/SysproEnetClient.cs` |
| **Session Pool** | `code-samples/session-pool/SysproSessionPool.cs` |
| **Sales Order Service** | `code-samples/order-service/SalesOrderService.cs` |
| **Auth Deep Dive** | `docs/07-SECURITY-AUTH.md` |
| **e.net Full Guide** | `docs/03-ENET-SOLUTIONS.md` |
| **Pre-Integration Checklist** | `checklists/PRE-INTEGRATION-CHECKLIST.md` |
| **Production Checklist** | `checklists/PRODUCTION-CHECKLIST.md` |
| **Glossary** | `GLOSSARY.md` |
| **Quick Start** | `QUICK-START.md` |

---

## ❓ COMMON QUESTIONS

### Q: Can I use REST API instead of XML?
**A:** SYSPRO 8+ has a REST API but it's limited. e.net XML is the primary integration method with full business object support.

### Q: Do I need SYSPRO installed on my machine?
**A:** NO. You connect to the client's SYSPRO server over HTTP.

### Q: Can I read directly from SQL?
**A:** YES for reads (with read-only user). NEVER write directly to SQL — always use e.net for writes.

### Q: What if the client has multiple companies?
**A:** Login separately for each company. Each session is company-specific.

### Q: How do I handle license exhaustion?
**A:** Implement session pooling (see `SysproSessionPool.cs`). Set pool max = license count.

---

## ✅ NEXT STEPS

1. ☐ Get credentials from client (Company ID, Operator, Password)
2. ☐ Get e.net URL from client IT
3. ☐ Test connectivity with curl/Postman
4. ☐ Copy `SysproEnetClient.cs` to your project
5. ☐ Create first API endpoint
6. ☐ Implement session pooling for production
7. ☐ Read `docs/03-ENET-SOLUTIONS.md` for deep dive

---

*This guide consolidates all information from the SYSPRO-INTEGRATION-GUIDE codebase. For detailed implementation, refer to the individual docs/ files.*
