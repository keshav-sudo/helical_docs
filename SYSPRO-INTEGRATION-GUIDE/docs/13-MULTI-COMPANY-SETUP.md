# Part 13: Multi-Company Setup Guide

[← Back to Index](./00-INDEX.md) | [← Back to Main Guide](../README.md)

---

## 🎯 Quick Answer

> **Q: How do I handle multiple SYSPRO companies in one integration?**
> 
> **A:** Each SYSPRO "Company" is a separate database. You need:
> 1. Separate login sessions for each company
> 2. Session pool per company
> 3. Pass CompanyId during Logon
> 4. CANNOT share sessions across companies

---

## 📋 Table of Contents

1. [Understanding SYSPRO Companies](#1-understanding-syspro-companies)
2. [Company Structure in SYSPRO](#2-company-structure-in-syspro)
3. [Multi-Company Architecture](#3-multi-company-architecture)
4. [Implementation Guide](#4-implementation-guide)
5. [Session Management per Company](#5-session-management-per-company)
6. [Cross-Company Operations](#6-cross-company-operations)
7. [Configuration Patterns](#7-configuration-patterns)
8. [Common Issues & Solutions](#8-common-issues--solutions)

---

## 1. Understanding SYSPRO Companies

### What is a SYSPRO Company?

```
┌───────────────────────────────────────────────────────────────────┐
│                   SYSPRO COMPANY CONCEPT                          │
├───────────────────────────────────────────────────────────────────┤
│                                                                   │
│  SYSPRO Installation                                              │
│  ├── Company "A" (Main Production)                               │
│  │   ├── Database: SYSPRO_CompanyA                               │
│  │   ├── Own Customers, Inventory, Orders                        │
│  │   └── Own GL Setup & Chart of Accounts                        │
│  │                                                                │
│  ├── Company "B" (Different Entity/Region)                       │
│  │   ├── Database: SYSPRO_CompanyB                               │
│  │   ├── Own Customers, Inventory, Orders                        │
│  │   └── Own GL Setup & Chart of Accounts                        │
│  │                                                                │
│  └── Company "T" (Test/Training)                                 │
│      ├── Database: SYSPRO_CompanyT                               │
│      └── For testing, training, UAT                              │
│                                                                   │
│  KEY POINT: Each company is COMPLETELY ISOLATED                  │
│  - Cannot share data between companies in SYSPRO                 │
│  - Each needs its own session for API access                     │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
```

### Why Multiple Companies?

| Scenario | Example |
|----------|---------|
| **Multiple Legal Entities** | Parent company with subsidiaries |
| **Regional Separation** | US operations vs EU operations |
| **Business Divisions** | Manufacturing vs Retail |
| **Test Environment** | Production "A" vs Test "T" |
| **Acquired Company** | Maintaining separate books during merger |

---

## 2. Company Structure in SYSPRO

### Company ID Format

```
┌─────────────────────────────────────────────────────────────┐
│  COMPANY ID: Single character A-Z or 0-9                    │
│                                                              │
│  Common conventions:                                         │
│  • "A" - Primary/Production company                          │
│  • "B","C","D" - Additional business entities               │
│  • "T" - Test/Training company                               │
│  • "U" - UAT (User Acceptance Testing)                       │
│  • Numbers (1,2,3) - Some clients use numeric IDs           │
│                                                              │
│  Maximum: 62 companies per SYSPRO installation              │
│  (A-Z = 26, 0-9 = 10, plus lowercase if enabled)            │
└─────────────────────────────────────────────────────────────┘
```

### Database per Company

```sql
-- Each company has its own database
-- SQL Server naming convention (typical):

SYSPRO_CompanyA          -- Main production
SYSPRO_CompanyB          -- Secondary entity
SYSPRO_CompanyT          -- Test environment

-- Or client-specific naming:
ACME_Production          -- Company A
ACME_WestCoast          -- Company B
ACME_Testing            -- Company T
```

---

## 3. Multi-Company Architecture

### Integration Architecture

```
┌────────────────────────────────────────────────────────────────────────┐
│                    MULTI-COMPANY ARCHITECTURE                           │
├────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   Your Application                                                      │
│   ┌──────────────────────────────────────────────────────────────┐    │
│   │                                                               │    │
│   │  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────┐ │    │
│   │  │ Session Pool A  │  │ Session Pool B  │  │ Session Pool │ │    │
│   │  │ (Company A)     │  │ (Company B)     │  │ T (Test)     │ │    │
│   │  │                 │  │                 │  │              │ │    │
│   │  │ Sessions: 5     │  │ Sessions: 3     │  │ Sessions: 2  │ │    │
│   │  └────────┬────────┘  └────────┬────────┘  └──────┬───────┘ │    │
│   │           │                    │                   │         │    │
│   └───────────┼────────────────────┼───────────────────┼─────────┘    │
│               │                    │                   │               │
│               ▼                    ▼                   ▼               │
│   ┌───────────────────────────────────────────────────────────────┐   │
│   │                     SYSPRO e.net Service                       │   │
│   │                     (Single Installation)                      │   │
│   └───────────────────────────────────────────────────────────────┘   │
│               │                    │                   │               │
│               ▼                    ▼                   ▼               │
│   ┌───────────────┐   ┌───────────────┐   ┌───────────────────┐      │
│   │ Company A DB  │   │ Company B DB  │   │ Company T DB      │      │
│   │ SYSPRO_CompA  │   │ SYSPRO_CompB  │   │ SYSPRO_CompT      │      │
│   └───────────────┘   └───────────────┘   └───────────────────┘      │
│                                                                         │
└────────────────────────────────────────────────────────────────────────┘
```

### Key Architecture Rules

| Rule | Description |
|------|-------------|
| **1 Pool = 1 Company** | Each company needs its own session pool |
| **Sessions are isolated** | Session from Company A cannot access Company B |
| **Same credentials OK** | User "ADMIN" can exist in multiple companies |
| **License per session** | Each session counts against total licenses |

---

## 4. Implementation Guide

### 4.1 Company-Aware Session Pool

```csharp
/// <summary>
/// Multi-company session pool manager
/// </summary>
public class MultiCompanySessionManager
{
    // Dictionary: CompanyId -> Session Pool
    private readonly ConcurrentDictionary<string, SysproSessionPool> _companyPools;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    
    public MultiCompanySessionManager(IConfiguration configuration, ILogger<MultiCompanySessionManager> logger)
    {
        _companyPools = new ConcurrentDictionary<string, SysproSessionPool>();
        _configuration = configuration;
        _logger = logger;
    }
    
    /// <summary>
    /// Get or create a session pool for a specific company
    /// </summary>
    public SysproSessionPool GetPoolForCompany(string companyId)
    {
        // Normalize company ID (uppercase)
        companyId = companyId.ToUpperInvariant();
        
        return _companyPools.GetOrAdd(companyId, id =>
        {
            _logger.LogInformation("Creating session pool for company {CompanyId}", id);
            
            var config = GetCompanyConfig(id);
            return new SysproSessionPool(
                config.BaseUrl,
                config.Operator,
                config.Password,
                companyId,
                config.PoolSize
            );
        });
    }
    
    /// <summary>
    /// Execute operation in a specific company context
    /// </summary>
    public async Task<T> ExecuteInCompanyAsync<T>(string companyId, Func<string, Task<T>> operation)
    {
        var pool = GetPoolForCompany(companyId);
        return await pool.ExecuteAsync(operation);
    }
    
    private CompanyConfig GetCompanyConfig(string companyId)
    {
        // Read from configuration
        var section = _configuration.GetSection($"Syspro:Companies:{companyId}");
        
        if (!section.Exists())
        {
            throw new InvalidOperationException($"No configuration found for company '{companyId}'");
        }
        
        return section.Get<CompanyConfig>()!;
    }
}

public class CompanyConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int PoolSize { get; set; } = 5;
}
```

### 4.2 Configuration (appsettings.json)

```json
{
  "Syspro": {
    "BaseUrl": "http://syspro-server:30000",
    
    "Companies": {
      "A": {
        "BaseUrl": "http://syspro-server:30000",
        "Operator": "ADMIN",
        "Password": "admin_password_A",
        "PoolSize": 10,
        "Description": "Main Production Company"
      },
      "B": {
        "BaseUrl": "http://syspro-server:30000",
        "Operator": "ADMIN",
        "Password": "admin_password_B",
        "PoolSize": 5,
        "Description": "West Coast Division"
      },
      "T": {
        "BaseUrl": "http://syspro-test:30000",
        "Operator": "TESTUSER",
        "Password": "test_password",
        "PoolSize": 3,
        "Description": "Test/Training Company"
      }
    }
  }
}
```

### 4.3 Login XML with Company

```xml
<!-- Login to Company A -->
<Logon>
  <Operator>ADMIN</Operator>
  <OperatorPassword>password123</OperatorPassword>
  <CompanyId>A</CompanyId>
  <CompanyPassword></CompanyPassword>
</Logon>

<!-- Login to Company B -->
<Logon>
  <Operator>ADMIN</Operator>
  <OperatorPassword>password123</OperatorPassword>
  <CompanyId>B</CompanyId>
  <CompanyPassword></CompanyPassword>
</Logon>
```

---

## 5. Session Management per Company

### 5.1 Requesting a Session

```csharp
public class OrderService
{
    private readonly MultiCompanySessionManager _sessionManager;
    
    public async Task<CreateOrderResult> CreateOrderAsync(string companyId, OrderRequest order)
    {
        // Session pool automatically handles company-specific sessions
        return await _sessionManager.ExecuteInCompanyAsync(companyId, async sessionId =>
        {
            var orderXml = BuildOrderXml(order);
            
            var result = await _client.TransactionAsync(
                sessionId, 
                "SORTOI", 
                orderXml
            );
            
            return ParseOrderResult(result);
        });
    }
}
```

### 5.2 API Design for Multi-Company

```csharp
// Option 1: Company in URL path
[Route("api/{companyId}/orders")]
public class OrdersController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        [FromRoute] string companyId, 
        [FromBody] OrderRequest order)
    {
        var result = await _orderService.CreateOrderAsync(companyId, order);
        return Ok(result);
    }
}

// Option 2: Company in header
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] OrderRequest order)
    {
        var companyId = Request.Headers["X-Company-Id"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(companyId))
        {
            return BadRequest("X-Company-Id header is required");
        }
        
        var result = await _orderService.CreateOrderAsync(companyId, order);
        return Ok(result);
    }
}

// Option 3: Company from authenticated user claims
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] OrderRequest order)
    {
        // Get company from JWT claims (set during login)
        var companyId = User.FindFirst("company_id")?.Value;
        
        if (string.IsNullOrEmpty(companyId))
        {
            return Unauthorized("User not associated with a company");
        }
        
        var result = await _orderService.CreateOrderAsync(companyId, order);
        return Ok(result);
    }
}
```

### 5.3 License Allocation per Company

```
┌─────────────────────────────────────────────────────────────────────┐
│               LICENSE ALLOCATION STRATEGY                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  Total Licenses Available: 20                                        │
│                                                                      │
│  Allocation:                                                         │
│  ├── Company A (Production):     12 licenses → Pool of 12 sessions │
│  ├── Company B (Regional):        5 licenses → Pool of 5 sessions  │
│  └── Company T (Test):            3 licenses → Pool of 3 sessions  │
│      ───────────────────                                            │
│      Total:                      20 licenses                        │
│                                                                      │
│  IMPORTANT:                                                          │
│  • Licenses are shared across all companies in installation          │
│  • Pool sizes should not exceed total available licenses            │
│  • Plan for peak usage across all companies                          │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 6. Cross-Company Operations

### When You Need Cross-Company Data

| Scenario | Solution |
|----------|----------|
| **Consolidated reporting** | Query each company separately, aggregate in your app |
| **Transfer between companies** | Create matching transactions in both (SO in A, PO in B) |
| **Shared customer lookup** | Query each company, deduplicate by customer code |
| **Cross-company inventory** | Sum inventory from all companies |

### Example: Cross-Company Inventory Check

```csharp
public async Task<AggregatedInventory> GetInventoryAcrossCompanies(string stockCode)
{
    var companies = new[] { "A", "B", "C" };
    var tasks = companies.Select(async companyId =>
    {
        var inventory = await GetCompanyInventory(companyId, stockCode);
        return new CompanyInventory
        {
            CompanyId = companyId,
            StockCode = stockCode,
            QtyOnHand = inventory.QtyOnHand,
            QtyAvailable = inventory.QtyAvailable
        };
    });
    
    var results = await Task.WhenAll(tasks);
    
    return new AggregatedInventory
    {
        StockCode = stockCode,
        TotalOnHand = results.Sum(r => r.QtyOnHand),
        TotalAvailable = results.Sum(r => r.QtyAvailable),
        ByCompany = results.ToList()
    };
}
```

### Cross-Company Transfer (Inter-Company Sales)

```
┌────────────────────────────────────────────────────────────────────┐
│             INTER-COMPANY TRANSFER FLOW                             │
├────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Company A (Selling)              Company B (Buying)               │
│  ──────────────────────           ─────────────────────            │
│                                                                     │
│  1. Create Sales Order            2. Create Purchase Order          │
│     Customer: COMPANY_B              Supplier: COMPANY_A            │
│     ──────────────────               ──────────────────             │
│     │                                │                              │
│     ▼                                ▼                              │
│  3. Ship Order                    4. Receive Goods                  │
│     (Reduces A inventory)            (Increases B inventory)        │
│     ──────────────────               ────────────────────           │
│     │                                │                              │
│     ▼                                ▼                              │
│  5. Invoice Customer B           6. Process Supplier Invoice        │
│     (A/R posted)                    (A/P posted)                    │
│                                                                     │
│  YOUR CODE MUST:                                                    │
│  • Create matching documents in BOTH companies                      │
│  • Use consistent references (link SO# to PO#)                     │
│  • Handle as a distributed transaction (both succeed or rollback) │
│                                                                     │
└────────────────────────────────────────────────────────────────────┘
```

---

## 7. Configuration Patterns

### Pattern 1: Environment-Based Company Selection

```json
// appsettings.Development.json
{
  "Syspro": {
    "DefaultCompany": "T",
    "Companies": {
      "T": { "BaseUrl": "http://syspro-test:30000", ... }
    }
  }
}

// appsettings.Production.json
{
  "Syspro": {
    "DefaultCompany": "A",
    "Companies": {
      "A": { "BaseUrl": "http://syspro-prod:30000", ... },
      "B": { "BaseUrl": "http://syspro-prod:30000", ... }
    }
  }
}
```

### Pattern 2: Tenant-Based Company Mapping

```csharp
// For multi-tenant SaaS applications
public class TenantCompanyMapping
{
    private readonly Dictionary<string, string> _tenantToCompany = new()
    {
        ["tenant-acme-corp"] = "A",
        ["tenant-beta-inc"] = "B",
        ["tenant-gamma-llc"] = "C"
    };
    
    public string GetCompanyForTenant(string tenantId)
    {
        if (_tenantToCompany.TryGetValue(tenantId, out var companyId))
        {
            return companyId;
        }
        
        throw new InvalidOperationException($"No SYSPRO company mapped for tenant '{tenantId}'");
    }
}
```

### Pattern 3: User-Based Company Access

```csharp
// Users can access multiple companies
public class UserCompanyAccess
{
    public async Task<List<string>> GetAllowedCompanies(string userId)
    {
        // From database or identity provider
        return await _userRepository.GetCompanyAccessList(userId);
    }
    
    public async Task ValidateAccess(string userId, string requestedCompany)
    {
        var allowed = await GetAllowedCompanies(userId);
        
        if (!allowed.Contains(requestedCompany))
        {
            throw new UnauthorizedAccessException(
                $"User {userId} does not have access to company {requestedCompany}");
        }
    }
}
```

---

## 8. Common Issues & Solutions

### Issue 1: "Session belongs to different company"

```
┌─────────────────────────────────────────────────────────────────┐
│  ERROR: "Session was created for company A, cannot use for B"  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  CAUSE: Using session from wrong pool                           │
│                                                                  │
│  WRONG:                                                          │
│    var session = poolA.GetSession();                            │
│    client.Query(session, "INVQRY", ...);  // Queries Company A  │
│    client.Query(session, "ARSQRY", ...);  // Still Company A!   │
│                                                                  │
│  RIGHT:                                                          │
│    var sessionA = poolA.GetSession();                           │
│    var sessionB = poolB.GetSession();                           │
│    client.Query(sessionA, "INVQRY", ...); // Queries Company A  │
│    client.Query(sessionB, "ARSQRY", ...); // Queries Company B  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Issue 2: License Exhaustion

```csharp
// Problem: Too many pools exhaust licenses
// Solution: Dynamic pool sizing based on load

public class DynamicPoolManager
{
    private readonly SemaphoreSlim _totalLicenseLimit;
    
    public DynamicPoolManager(int totalLicenses)
    {
        // Global semaphore across all companies
        _totalLicenseLimit = new SemaphoreSlim(totalLicenses, totalLicenses);
    }
    
    public async Task<IDisposable> AcquireSession(string companyId)
    {
        // Wait for available license
        await _totalLicenseLimit.WaitAsync();
        
        try
        {
            var pool = GetPoolForCompany(companyId);
            return await pool.AcquireSessionAsync();
        }
        catch
        {
            _totalLicenseLimit.Release();
            throw;
        }
    }
}
```

### Issue 3: Configuration Drift

```csharp
// Solution: Validate company configuration on startup

public class CompanyConfigValidator
{
    public async Task ValidateAllCompanies(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var companies = config.GetSection("Syspro:Companies").GetChildren();
        
        foreach (var company in companies)
        {
            var companyId = company.Key;
            var baseUrl = company["BaseUrl"];
            
            // Test connectivity to each company
            var client = new SysproEnetClient(baseUrl);
            
            try
            {
                var sessionId = await client.LogonAsync(
                    company["Operator"],
                    company["Password"],
                    companyId
                );
                
                await client.LogoffAsync(sessionId);
                
                Console.WriteLine($"✓ Company {companyId}: Connection successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Company {companyId}: {ex.Message}");
                throw;
            }
        }
    }
}
```

---

## 📋 Multi-Company Checklist

Before going live with multi-company setup:

### Configuration
- [ ] Each company has configuration entry
- [ ] Credentials for each company are correct
- [ ] Pool sizes sum to available licenses
- [ ] Test connectivity to each company on startup

### Security
- [ ] Users are authorized for specific companies
- [ ] Company ID is validated before use
- [ ] Audit logs include company ID

### Architecture
- [ ] Separate session pool per company
- [ ] API clearly indicates target company
- [ ] Cross-company operations are explicit

### Testing
- [ ] Test operations in each company
- [ ] Test cross-company scenarios
- [ ] Test license exhaustion handling
- [ ] Verify company isolation (no data leakage)

---

## 🔗 Related Documentation

- [03-ENET-SOLUTIONS.md](./03-ENET-SOLUTIONS.md) - e.net API details
- [07-SECURITY-AUTH.md](./07-SECURITY-AUTH.md) - Authentication patterns
- [../code-samples/session-pool/](../code-samples/session-pool/) - Session pool implementation

---

[← Back to Index](./00-INDEX.md) | [← Back to Main Guide](../README.md)
