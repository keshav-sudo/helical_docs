# 🚀 Quick Start Guide — From Zero to First SYSPRO API Call

> **Read Time**: 15 minutes | **Hands-on Time**: 2 hours  
> **Prerequisites**: Basic knowledge of C#, REST APIs, SQL Server

---

## What You'll Build Today

By the end of this guide, you'll have:
1. ✅ A working .NET 8 Web API project
2. ✅ Connection to SYSPRO e.net Solutions
3. ✅ Your first API endpoint that queries SYSPRO inventory
4. ✅ Understanding of the XML request/response pattern

---

## 🎯 Step 1: Understand What You're Building (5 min)

```
YOUR SYSTEM                    SYSPRO
═══════════                    ══════
┌──────────────┐              ┌──────────────┐
│ Your .NET    │  ──XML───►   │ e.net        │
│ Web API      │              │ Solutions    │
│              │  ◄──XML───   │              │
│ (Middleware) │              │ (Gateway)    │
└──────┬───────┘              └──────┬───────┘
       │                             │
       │ REST/JSON                   │
       ▼                             ▼
┌──────────────┐              ┌──────────────┐
│ Your         │              │ SYSPRO       │
│ Frontend     │              │ Database     │
│ (React/Vue)  │              │ (SQL Server) │
└──────────────┘              └──────────────┘
```

**Key Concept**: Your API is a "translator" between:
- **Modern world** (REST, JSON, React)  
- **ERP world** (XML, Business Objects, Legacy)

---

## 🎯 Step 2: Get These Ready (10 min)

### Required Information (Ask your IT/SYSPRO admin)

| Item | Example | Where to Get |
|------|---------|--------------|
| SYSPRO e.net URL | `http://syspro-server:8080/` | IT Team |
| Company ID | `S` or `001` | SYSPRO Admin |
| Operator Code | `ADMIN` | SYSPRO Admin |
| Operator Password | `****` | SYSPRO Admin |
| Test Warehouse | `WH01` | SYSPRO Admin |
| SYSPRO SQL Server | `syspro-sql-server` | IT Team |
| SYSPRO Database Name | `SysproCompanyS` | IT Team |

### Required Software

```bash
# Check you have these installed
dotnet --version    # Need 8.0+
code --version      # VS Code (or Visual Studio)
```

---

## 🎯 Step 3: Create Your Project (15 min)

```bash
# Create project
mkdir syspro-integration && cd syspro-integration
dotnet new webapi -n SysproAPI -o ./src/SysproAPI

# Add required packages
cd src/SysproAPI
dotnet add package Polly                    # Retry logic
dotnet add package Serilog.AspNetCore       # Logging
dotnet add package Microsoft.Data.SqlClient # SQL connectivity

# Open in VS Code
code .
```

---

## 🎯 Step 4: Configure appsettings.json (5 min)

Replace the contents of `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Syspro": {
    "BaseUrl": "http://YOUR-SYSPRO-SERVER:8080/",
    "CompanyId": "S",
    "DefaultOperator": "ADMIN",
    "DefaultPassword": "your-password",
    "DefaultWarehouse": "WH01",
    "TimeoutSeconds": 30
  },
  "ConnectionStrings": {
    "SysproDb": "Server=YOUR-SQL-SERVER;Database=SysproCompanyS;User Id=readonly_user;Password=***;TrustServerCertificate=true;"
  }
}
```

---

## 🎯 Step 5: Create the e.net Client (20 min)

Create file `Services/SysproEnetClient.cs`:

```csharp
using System.Net.Http;
using System.Xml.Linq;

namespace SysproAPI.Services;

public class SysproEnetClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<SysproEnetClient> _logger;
    
    public SysproEnetClient(HttpClient httpClient, IConfiguration config, ILogger<SysproEnetClient> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }
    
    // Step 1: Login to SYSPRO (get session ID)
    public async Task<string> LogonAsync()
    {
        var url = $"{_config["Syspro:BaseUrl"]}saborw/Logon";
        
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Operator"] = _config["Syspro:DefaultOperator"]!,
            ["Password"] = _config["Syspro:DefaultPassword"]!,
            ["CompanyId"] = _config["Syspro:CompanyId"]!,
            ["OperatorPassword"] = _config["Syspro:DefaultPassword"]!
        });
        
        var response = await _httpClient.PostAsync(url, content);
        var sessionId = await response.Content.ReadAsStringAsync();
        
        _logger.LogInformation("Logged into SYSPRO. SessionId starts with: {SessionStart}", 
            sessionId.Substring(0, Math.Min(8, sessionId.Length)));
        
        return sessionId;
    }
    
    // Step 2: Query a Business Object
    public async Task<string> QueryAsync(string sessionId, string businessObject, string xmlParameters)
    {
        var url = $"{_config["Syspro:BaseUrl"]}saborw/Query";
        
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = sessionId,
            ["BusinessObject"] = businessObject,
            ["XmlIn"] = xmlParameters
        });
        
        var response = await _httpClient.PostAsync(url, content);
        return await response.Content.ReadAsStringAsync();
    }
    
    // Step 3: Logout (release session)
    public async Task LogoffAsync(string sessionId)
    {
        var url = $"{_config["Syspro:BaseUrl"]}saborw/Logoff";
        
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = sessionId
        });
        
        await _httpClient.PostAsync(url, content);
        _logger.LogInformation("Logged off from SYSPRO");
    }
}
```

---

## 🎯 Step 6: Create Inventory Service (15 min)

Create file `Services/InventoryService.cs`:

```csharp
using System.Xml.Linq;

namespace SysproAPI.Services;

public class InventoryService
{
    private readonly SysproEnetClient _enetClient;
    
    public InventoryService(SysproEnetClient enetClient)
    {
        _enetClient = enetClient;
    }
    
    public async Task<List<InventoryItem>> GetInventoryAsync(string? stockCodeFilter = null)
    {
        // 1. Login
        var sessionId = await _enetClient.LogonAsync();
        
        try
        {
            // 2. Build XML query
            var xmlParams = $@"<?xml version=""1.0""?>
<Query>
    <TableName>InvMaster</TableName>
    <ReturnRows>100</ReturnRows>
    <Columns>
        <Column>StockCode</Column>
        <Column>Description</Column>
        <Column>Warehouse</Column>
        <Column>QtyOnHand</Column>
        <Column>QtyAllocated</Column>
    </Columns>
    {(stockCodeFilter != null ? $"<Filter>StockCode LIKE '{stockCodeFilter}%'</Filter>" : "")}
</Query>";
            
            // 3. Call SYSPRO
            var xmlResponse = await _enetClient.QueryAsync(sessionId, "INVQRY", xmlParams);
            
            // 4. Parse response
            var doc = XDocument.Parse(xmlResponse);
            var items = doc.Descendants("Row").Select(row => new InventoryItem
            {
                StockCode = row.Element("StockCode")?.Value ?? "",
                Description = row.Element("Description")?.Value ?? "",
                Warehouse = row.Element("Warehouse")?.Value ?? "",
                QtyOnHand = decimal.TryParse(row.Element("QtyOnHand")?.Value, out var qty) ? qty : 0,
                QtyAllocated = decimal.TryParse(row.Element("QtyAllocated")?.Value, out var alloc) ? alloc : 0
            }).ToList();
            
            return items;
        }
        finally
        {
            // 5. Always logout
            await _enetClient.LogoffAsync(sessionId);
        }
    }
}

public class InventoryItem
{
    public string StockCode { get; set; } = "";
    public string Description { get; set; } = "";
    public string Warehouse { get; set; } = "";
    public decimal QtyOnHand { get; set; }
    public decimal QtyAllocated { get; set; }
    public decimal Available => QtyOnHand - QtyAllocated;
}
```

---

## 🎯 Step 7: Create API Controller (10 min)

Create file `Controllers/InventoryController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using SysproAPI.Services;

namespace SysproAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly InventoryService _inventoryService;
    
    public InventoryController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetInventory([FromQuery] string? stockCode = null)
    {
        try
        {
            var items = await _inventoryService.GetInventoryAsync(stockCode);
            return Ok(new 
            { 
                success = true, 
                count = items.Count,
                data = items 
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new 
            { 
                success = false, 
                error = ex.Message 
            });
        }
    }
}
```

---

## 🎯 Step 8: Register Services (5 min)

Update `Program.cs`:

```csharp
using SysproAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register our services
builder.Services.AddHttpClient<SysproEnetClient>();
builder.Services.AddScoped<InventoryService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

---

## 🎯 Step 9: Run & Test (5 min)

```bash
dotnet run
```

Open browser: `https://localhost:5001/swagger`

Try the `/api/inventory` endpoint!

---

## ✅ Success Checkpoint

If you see inventory data, congratulations! You've:
1. Connected to SYSPRO e.net
2. Sent an XML query
3. Parsed the XML response
4. Returned JSON via REST API

---

## 🔜 Next Steps

| When Ready For | Read This |
|---------------|-----------|
| Understanding SYSPRO modules | [01-SYSTEM-UNDERSTANDING.md](./docs/01-SYSTEM-UNDERSTANDING.md) |
| Creating Sales Orders | [04-DOTNET-IMPLEMENTATION.md](./docs/04-DOTNET-IMPLEMENTATION.md) |
| Building a complete project | [05-REAL-PROJECT.md](./docs/05-REAL-PROJECT.md) |
| Production deployment | [08-DEPLOYMENT.md](./docs/08-DEPLOYMENT.md) |

---

## 🆘 Common Errors & Fixes

| Error | Cause | Fix |
|-------|-------|-----|
| "Connection refused" | SYSPRO server not reachable | Check firewall, VPN, URL |
| "Invalid operator" | Wrong credentials | Verify with SYSPRO admin |
| "License exceeded" | Too many sessions | Implement session pooling (Part 4.4) |
| "Company not found" | Wrong company ID | Check company code with admin |
| XML parsing error | Invalid response | Log raw response, check SYSPRO logs |
