# 🔄 SYSPRO Version Compatibility Guide

> Differences between SYSPRO 7 and SYSPRO 8 for integration developers

---

## 📊 Quick Comparison Matrix

| Feature | SYSPRO 7 | SYSPRO 8 | SYSPRO 8 (2023+) |
|---------|----------|----------|------------------|
| **Release Period** | 2014-2018 | 2018-2023 | 2023+ |
| **e.net Solutions** | ✅ Available | ✅ Available | ✅ Available |
| **REST API** | ❌ No | ⚠️ Limited | ✅ Expanded |
| **Web UI** | ❌ No | ✅ Avanti | ✅ Avanti |
| **Cloud Option** | ❌ No | ⚠️ Limited | ✅ SYSPRO Harmony |
| **OAuth Support** | ❌ No | ⚠️ REST only | ✅ Yes |
| **XML/SOAP** | ✅ Primary | ✅ Primary | ✅ Primary |
| **COM+ Required** | ✅ Yes | ✅ Yes | ⚠️ Optional |

---

## 🔌 Integration Method Availability

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    INTEGRATION METHODS BY VERSION                        │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  METHOD              │ SYSPRO 7  │ SYSPRO 8  │ SYSPRO 8+              │
│  ────────────────────┼───────────┼───────────┼────────────────────────│
│  e.net COM/DCOM      │ ✅ Primary │ ✅ Yes     │ ✅ Yes (legacy)        │
│  e.net WCF (HTTP)    │ ✅ Primary │ ✅ Yes     │ ✅ Yes                 │
│  e.net WCF (TCP)     │ ✅ Yes     │ ✅ Yes     │ ✅ Yes                 │
│  REST API            │ ❌ No      │ ⚠️ Limited │ ✅ Expanded            │
│  SYSPRO Harmony      │ ❌ No      │ ❌ No      │ ✅ Yes (iPaaS)         │
│  OData               │ ❌ No      │ ⚠️ Limited │ ✅ Yes                 │
│                                                                          │
│  RECOMMENDATION:                                                         │
│  ─────────────────                                                       │
│  • SYSPRO 7: Use e.net WCF (HTTP/XML) - only option                    │
│  • SYSPRO 8: Use e.net WCF (HTTP/XML) - most complete                  │
│  • SYSPRO 8+: Use e.net OR REST - depends on use case                  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 📝 e.net Endpoint Differences

### SYSPRO 7

```
Base URL: http://{server}:30661/SYSPROWCFService/
          OR
          http://{server}:8080/saborw/

Endpoints:
  /Logon        - Authentication
  /Logoff       - Release session
  /Query        - Read data
  /Transaction  - Write data
  /Browse       - List data
```

### SYSPRO 8

```
Base URL: http://{server}:8080/saborw/
          OR
          http://{server}:30661/SYSPROWCFService/
          OR (NEW)
          https://{server}/SYSPRORestApi/

Endpoints (e.net - same as 7):
  /saborw/Logon
  /saborw/Logoff
  /saborw/Query
  /saborw/Transaction
  
Endpoints (REST - new):
  /api/v1/session (POST) - Login
  /api/v1/inventory (GET) - Query inventory
  /api/v1/salesorders (POST) - Create order
```

### SYSPRO 8+ (2023)

```
Base URL: Same as SYSPRO 8
          PLUS
          https://{tenant}.sysprocloud.com/api/ (Harmony)

Additional:
  - OAuth 2.0 authentication option
  - Expanded REST coverage
  - Webhook support (limited)
```

---

## 🔐 Authentication Differences

### SYSPRO 7 - e.net Only

```csharp
// Only method: Username + Password
var content = new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["Operator"] = "API_USER",
    ["Password"] = "password",
    ["CompanyId"] = "A",
    ["OperatorPassword"] = "password"
});
var sessionId = await client.PostAsync("/saborw/Logon", content);
```

### SYSPRO 8 - e.net + Basic Auth (REST)

```csharp
// Method 1: e.net (same as SYSPRO 7)
// ... same code as above ...

// Method 2: REST API with Basic Auth (NEW)
client.DefaultRequestHeaders.Authorization = 
    new AuthenticationHeaderValue("Basic", 
        Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass")));
```

### SYSPRO 8+ - e.net + OAuth

```csharp
// Method 1 & 2: Same as SYSPRO 8

// Method 3: OAuth 2.0 (NEW)
var tokenResponse = await client.PostAsync("/oauth/token", 
    new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["grant_type"] = "client_credentials",
        ["client_id"] = "your-client-id",
        ["client_secret"] = "your-client-secret"
    }));
var token = JsonSerializer.Deserialize<TokenResponse>(await tokenResponse.Content.ReadAsStringAsync());

client.DefaultRequestHeaders.Authorization = 
    new AuthenticationHeaderValue("Bearer", token.AccessToken);
```

---

## 📦 Business Object Differences

### Core BOs (Same in All Versions)

| Business Object | SYSPRO 7 | SYSPRO 8 | Description |
|----------------|----------|----------|-------------|
| `SORTOI` | ✅ | ✅ | Sales Order Transaction |
| `SORQRY` | ✅ | ✅ | Sales Order Query |
| `INVQRY` | ✅ | ✅ | Inventory Query |
| `ARSTOP` | ✅ | ✅ | Customer Setup |
| `ARSQRY` | ✅ | ✅ | Customer Query |
| `PORTOI` | ✅ | ✅ | Purchase Order Transaction |
| `PORQRY` | ✅ | ✅ | Purchase Order Query |
| `INVTMR` | ✅ | ✅ | Inventory Receipt |
| `INVTMT` | ✅ | ✅ | Inventory Transfer |

### New BOs in SYSPRO 8

| Business Object | SYSPRO 8+ | Description |
|----------------|-----------|-------------|
| `COMSFQ` | ✅ | Quote Management |
| `WFETSK` | ✅ | Workflow Tasks |
| `ESPHLP` | ✅ | ESP Helper Objects |

---

## 🗄️ Database Schema Differences

### Core Tables (Same in All Versions)

```sql
-- These tables exist in all versions with same structure:
SorMaster       -- Sales Order Header
SorDetail       -- Sales Order Lines
InvMaster       -- Inventory Master
InvWarehouse    -- Stock by Warehouse
ArCustomer      -- Customer Master
ApSupplier      -- Supplier Master
PorMasterHdr    -- Purchase Order Header
PorMasterDet    -- Purchase Order Lines
```

### SYSPRO 8 Additional Tables

```sql
-- New in SYSPRO 8:
AdmFormControl      -- Avanti UI configuration
EspWorkflow         -- Workflow definitions
WfeTaskQueue        -- Workflow task queue
SrsReportCache      -- Report caching
```

### Column Differences

| Table | Column | SYSPRO 7 | SYSPRO 8 |
|-------|--------|----------|----------|
| SorMaster | WebOrderRef | ❌ | ✅ (new) |
| SorMaster | SourceSystem | ❌ | ✅ (new) |
| ArCustomer | CustomerType | VARCHAR(1) | VARCHAR(3) |
| InvMaster | EcommerceEnabled | ❌ | ✅ (new) |

---

## ⚙️ Configuration Differences

### Session Timeout

| Version | Default Timeout | Configurable |
|---------|----------------|--------------|
| SYSPRO 7 | 20 minutes | Yes (registry) |
| SYSPRO 8 | 20 minutes | Yes (web.config + UI) |

### Session Limits

| Version | Default Sessions | Maximum |
|---------|-----------------|---------|
| SYSPRO 7 | Per license | Hard limit |
| SYSPRO 8 | Per license | Hard limit + soft warnings |

---

## 🔄 Migration Considerations

### Migrating from SYSPRO 7 to 8

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    MIGRATION CHECKLIST: 7 → 8                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ✅ NO CODE CHANGES REQUIRED FOR:                                       │
│  ─────────────────────────────────                                       │
│  • e.net XML requests/responses (same format)                           │
│  • Business Object codes (same names)                                   │
│  • Core SQL table structures (same)                                     │
│  • Session management (same flow)                                       │
│                                                                          │
│  ⚠️ VERIFY/UPDATE:                                                      │
│  ────────────────                                                        │
│  • Server URL (might change during upgrade)                             │
│  • Port numbers (verify e.net port)                                     │
│  • SSL certificates (SYSPRO 8 prefers HTTPS)                            │
│  • Operator permissions (re-verify after upgrade)                       │
│                                                                          │
│  🆕 OPTIONAL ENHANCEMENTS:                                              │
│  ──────────────────────────                                              │
│  • Consider using REST API for simple queries                           │
│  • Add OAuth support if using Avanti integration                        │
│  • Leverage new Business Objects if needed                              │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 💻 Version Detection Code

```csharp
public class SysproVersionDetector
{
    public async Task<SysproVersion> DetectVersionAsync(string baseUrl)
    {
        // Method 1: Check for REST API availability
        try
        {
            var response = await _client.GetAsync($"{baseUrl}/SYSPRORestApi/api/v1/version");
            if (response.IsSuccessStatusCode)
            {
                var version = await response.Content.ReadAsStringAsync();
                if (version.Contains("8."))
                    return SysproVersion.Syspro8;
            }
        }
        catch { }

        // Method 2: Check e.net response headers
        try
        {
            var response = await _client.GetAsync($"{baseUrl}/saborw/version");
            var headers = response.Headers;
            // Parse version from response
        }
        catch { }

        // Default assumption
        return SysproVersion.Syspro7;
    }
}

public enum SysproVersion
{
    Syspro7,
    Syspro8,
    Syspro8Plus
}
```

---

## 🔧 Version-Aware Integration Pattern

```csharp
public class SysproClientFactory
{
    public ISysproClient CreateClient(SysproVersion version, SysproSettings settings)
    {
        return version switch
        {
            SysproVersion.Syspro7 => new SysproEnetClient(settings),
            SysproVersion.Syspro8 => settings.UseRestApi 
                ? new SysproRestClient(settings) 
                : new SysproEnetClient(settings),
            SysproVersion.Syspro8Plus => settings.UseOAuth
                ? new SysproOAuthClient(settings)
                : new SysproEnetClient(settings),
            _ => new SysproEnetClient(settings) // Safe default
        };
    }
}

public interface ISysproClient
{
    Task<string> QueryAsync(string businessObject, string xml);
    Task<string> TransactionAsync(string businessObject, string paramsXml, string docXml);
}
```

---

## 📋 Compatibility Checklist

### Before Starting Integration

- [ ] Confirm SYSPRO version with client
- [ ] Check which modules are licensed
- [ ] Verify e.net is enabled
- [ ] Get correct server URL and port
- [ ] Test connectivity to e.net endpoint
- [ ] Verify REST API availability (if SYSPRO 8+)

### Code Compatibility

| Your Code Uses | SYSPRO 7 | SYSPRO 8 | Notes |
|---------------|----------|----------|-------|
| e.net HTTP/XML | ✅ Works | ✅ Works | Universal |
| REST API | ❌ Fails | ⚠️ Limited | Check coverage |
| OAuth | ❌ Fails | ⚠️ Partial | 8+ only |
| Direct SQL Read | ✅ Works | ✅ Works | Universal |

---

## 🆘 Version-Specific Troubleshooting

### SYSPRO 7 Issues

| Issue | Cause | Fix |
|-------|-------|-----|
| No REST endpoint | Not supported | Use e.net only |
| COM errors | COM+ not configured | Register COM objects |
| WCF binding error | Wrong binding | Use basicHttpBinding |

### SYSPRO 8 Issues

| Issue | Cause | Fix |
|-------|-------|-----|
| REST returns 404 | API not enabled | Enable in SYSPRO Setup |
| Mixed mode auth | Conflicting settings | Choose one auth method |
| Avanti conflicts | Session sharing | Use dedicated API operator |

---

## 🎯 Recommendations by Version

### SYSPRO 7 Integration

```
✅ USE:
• e.net WCF (HTTP) for all API calls
• Direct SQL for read-heavy operations
• Session pooling (mandatory)

❌ AVOID:
• Expecting REST API
• Modern auth methods
```

### SYSPRO 8 Integration

```
✅ USE:
• e.net WCF (HTTP) for transactions
• REST API for simple queries (if enabled)
• Direct SQL for reporting
• Session pooling (mandatory)

⚠️ CONSIDER:
• REST API for new development
• OAuth if integrating with Avanti
```

### SYSPRO 8+ Integration

```
✅ USE:
• REST API for standard operations
• e.net for complex/bulk transactions
• OAuth for Avanti integration
• SYSPRO Harmony for cloud scenarios

🆕 EXPLORE:
• Webhooks for real-time events
• OData for data queries
```

---

*Version information based on SYSPRO documentation as of 2024. Always verify with your specific SYSPRO installation.*
