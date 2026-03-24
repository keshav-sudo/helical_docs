# Part 7: Security & Authentication (Production)

[← Back to Main Guide](../README.md) | [Previous: Error Handling](./06-ERROR-HANDLING.md) | [Next: Deployment →](./08-DEPLOYMENT.md)

---

## 7.1 Security Architecture — Complete Picture

```
┌──────────────────────────────────────────────────────────────────────────┐
│                    SECURITY ARCHITECTURE                                  │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                           │
│  LAYER 1: API SECURITY (Your .NET API)                                   │
│  ──────────────────────────────────────                                   │
│  ┌────────────┐    JWT + HTTPS     ┌──────────────────────────────┐     │
│  │ Frontend / │───────────────────►│ .NET 8 Web API               │     │
│  │ External   │                    │                              │     │
│  │ Systems    │◄───────────────────│ Middleware stack:            │     │
│  └────────────┘                    │ 1. HTTPS enforcement         │     │
│                                    │ 2. CORS policy               │     │
│                                    │ 3. Rate limiting             │     │
│                                    │ 4. JWT validation            │     │
│                                    │ 5. RBAC authorization        │     │
│                                    │ 6. Request logging           │     │
│                                    │ 7. Input validation          │     │
│                                    └──────────────────────────────┘     │
│                                                                           │
│  LAYER 2: SYSPRO CREDENTIAL MANAGEMENT                                   │
│  ──────────────────────────────────────                                   │
│  Your API authenticates to SYSPRO using:                                 │
│  • Operator + Password (stored in Key Vault or Secret Manager)          │
│  • Company ID + Company Password                                         │
│  • NEVER in appsettings.json in production                              │
│  • NEVER in source code or Git                                           │
│                                                                           │
│  LAYER 3: SQL DATABASE SECURITY                                          │
│  ─────────────────────────────                                            │
│  • SYSPRO DB: Read-only SQL user (db_datareader only)                   │
│  • Local DB: Application-specific user with limited permissions         │
│  • Connection strings in Key Vault                                       │
│  • TDE (Transparent Data Encryption) enabled                            │
│                                                                           │
│  LAYER 4: NETWORK SECURITY                                               │
│  ─────────────────────────                                                │
│  • VPN / Azure ExpressRoute between cloud and SYSPRO on-prem            │
│  • Firewall rules: only API server can reach SYSPRO                     │
│  • No direct internet access to SYSPRO                                   │
│                                                                           │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 7.2 JWT Authentication — Complete Implementation

### JWT Service

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SysproIntegration.Infrastructure.Security;

public class JwtService
{
    private readonly JwtSettings _settings;

    public JwtService(IOptions<JwtSettings> settings) => _settings = settings.Value;

    public string GenerateToken(string userId, string email, string[] roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        // Add role claims
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;   // 256-bit min
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}
```

### Auth Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(JwtService jwtService, ILogger<AuthController> logger)
    {
        _jwtService = jwtService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // In production: validate against your user store (AD, DB, Identity Server)
        // This is simplified for demonstration
        var (valid, roles) = ValidateUser(request.Username, request.Password);
        
        if (!valid)
        {
            _logger.LogWarning("Failed login attempt for user {User}", request.Username);
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var token = _jwtService.GenerateToken(
            request.Username, 
            $"{request.Username}@company.com",
            roles);

        _logger.LogInformation("User {User} logged in successfully", request.Username);
        
        return Ok(new { token, expiresIn = 3600, roles });
    }

    private (bool valid, string[] roles) ValidateUser(string username, string password)
    {
        // Production: use ASP.NET Identity, AD, or external IdP
        // Never hardcode credentials
        return username switch
        {
            "admin" => (true, new[] { "Admin", "OrderManager" }),
            "warehouse" => (true, new[] { "InventoryViewer" }),
            "sales" => (true, new[] { "OrderCreator", "CustomerViewer" }),
            _ => (false, Array.Empty<string>())
        };
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
```

---

## 7.3 Role-Based Access Control (RBAC)

### Role Definitions

```
┌──────────────────────────────────────────────────────────────────┐
│                    RBAC ROLE MATRIX                               │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Role              │ Permissions                                  │
│  ─────────────────────────────────────────────────────           │
│  Admin             │ All operations + user management            │
│  OrderManager      │ Create, view, retry, cancel orders          │
│  OrderCreator      │ Create, view orders only                    │
│  InventoryViewer   │ View inventory only (no writes)             │
│  CustomerViewer    │ View customers only (no writes)             │
│  CustomerManager   │ Create, view, update customers              │
│  DashboardViewer   │ View dashboard metrics only                 │
│  ApiIntegration    │ Full API access (for system-to-system)      │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

### Applying RBAC to Controllers

```csharp
[ApiController]
[Route("api/orders")]
[Authorize] // All endpoints require authentication
public class SalesOrderController : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin,OrderManager,OrderCreator,ApiIntegration")]
    public async Task<IActionResult> CreateSalesOrder(
        [FromBody] CreateSalesOrderRequest request) { /* ... */ }

    [HttpGet]
    [Authorize(Roles = "Admin,OrderManager,OrderCreator,DashboardViewer")]
    public async Task<IActionResult> ListOrders() { /* ... */ }

    [HttpPost("{id}/retry")]
    [Authorize(Roles = "Admin,OrderManager")] // Only managers can retry
    public async Task<IActionResult> RetrySync(Guid id) { /* ... */ }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,OrderManager")] // Only managers can cancel
    public async Task<IActionResult> CancelOrder(Guid id) { /* ... */ }
}
```

---

## 7.4 Credential Management — Azure Key Vault

```csharp
// Program.cs — Load secrets from Azure Key Vault
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{builder.Configuration["KeyVault:Name"]}.vault.azure.net/"),
    new DefaultAzureCredential());

// Key Vault secrets structure:
// Syspro--OperatorPassword → SYSPRO operator password
// Syspro--CompanyPassword → SYSPRO company password
// ConnectionStrings--SysproDb → SYSPRO SQL connection string
// ConnectionStrings--LocalDb → Local DB connection string
// Jwt--Secret → JWT signing key
```

### Environment-Specific Secret Management

```
DEVELOPMENT:
  dotnet user-secrets set "Syspro:OperatorPassword" "dev-password"
  dotnet user-secrets set "ConnectionStrings:SysproDb" "Server=localhost;..."

STAGING / PRODUCTION:
  Azure Key Vault (recommended)
  OR
  Kubernetes Secrets (if running in K8s)
  OR
  AWS Secrets Manager
  OR
  HashiCorp Vault

NEVER NEVER NEVER:
  ❌ In appsettings.json committed to Git
  ❌ In environment variables in plain text
  ❌ In code comments or documentation
  ❌ In Docker Compose files committed to Git
  ❌ Shared via email or chat
```

---

## 7.5 Input Validation & Sanitization

### Server-Side Validation (FluentValidation)

```csharp
using FluentValidation;

public class CreateSalesOrderValidator : AbstractValidator<CreateSalesOrderRequest>
{
    public CreateSalesOrderValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer ID is required")
            .MaximumLength(15).WithMessage("Customer ID max 15 chars")
            .Matches(@"^[A-Za-z0-9]+$").WithMessage("Customer ID: alphanumeric only");

        RuleFor(x => x.CustomerPoNumber)
            .MaximumLength(30).When(x => x.CustomerPoNumber != null)
            .Matches(@"^[A-Za-z0-9\-_]+$").When(x => !string.IsNullOrEmpty(x.CustomerPoNumber))
            .WithMessage("PO Number: alphanumeric, hyphens, underscores only");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("At least one order line required");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.StockCode)
                .NotEmpty().WithMessage("Stock code is required")
                .MaximumLength(30)
                .Matches(@"^[A-Za-z0-9\-]+$").WithMessage("Stock code: invalid characters");

            line.RuleFor(l => l.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be > 0")
                .LessThanOrEqualTo(99999).WithMessage("Quantity exceeds maximum");

            line.RuleFor(l => l.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Price must be >= 0");
        });
    }
}
```

### XML Injection Prevention

```csharp
/// <summary>
/// CRITICAL: Always escape user input before embedding in XML for SYSPRO.
/// SYSPRO processes XML — a malicious string could break the XML structure.
/// </summary>
public static string SanitizeForXml(string? input)
{
    if (string.IsNullOrWhiteSpace(input)) return string.Empty;
    
    // Remove control characters (except whitespace)
    var cleaned = new string(input.Where(c => 
        !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t').ToArray());
    
    // XML-escape special characters
    return System.Security.SecurityElement.Escape(cleaned);
}

// Usage in XML builder:
new XElement("CustomerPoNumber", SanitizeForXml(request.CustomerPoNumber))
```

---

## 7.6 API Security Headers

```csharp
// SecurityHeadersMiddleware.cs
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        
        // Remove server identification
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");

        await _next(context);
    }
}

// Register in Program.cs
app.UseMiddleware<SecurityHeadersMiddleware>();
```

---

## 7.7 Audit Logging — Compliance

```csharp
/// <summary>
/// Logs every significant action for compliance and debugging.
/// Required for: SOX compliance, ISO 27001, client audits.
/// </summary>
public class AuditService
{
    private readonly string _connStr;

    public AuditService(IConfiguration config)
    {
        _connStr = config.GetConnectionString("LocalDb")!;
    }

    public async Task LogAsync(string entityType, string entityId, string action,
        object? oldValues, object? newValues, string userId, string? ipAddress)
    {
        const string sql = @"
            INSERT INTO AuditTrail 
                (EntityType, EntityId, Action, OldValues, NewValues, UserId, IpAddress)
            VALUES 
                (@Type, @Id, @Action, @Old, @New, @User, @Ip)";

        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Type", entityType);
        cmd.Parameters.AddWithValue("@Id", entityId);
        cmd.Parameters.AddWithValue("@Action", action);
        cmd.Parameters.AddWithValue("@Old", 
            oldValues != null ? JsonSerializer.Serialize(oldValues) : DBNull.Value);
        cmd.Parameters.AddWithValue("@New", 
            newValues != null ? JsonSerializer.Serialize(newValues) : DBNull.Value);
        cmd.Parameters.AddWithValue("@User", userId);
        cmd.Parameters.AddWithValue("@Ip", (object?)ipAddress ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }
}

// Usage in controller:
await _auditService.LogAsync(
    "SalesOrder", orderId.ToString(), "Create",
    oldValues: null,
    newValues: request,
    userId: User.Identity?.Name ?? "anonymous",
    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
```

---

## 7.8 Security Checklist

| # | Item | Status | Notes |
|---|------|--------|-------|
| 1 | HTTPS enforced everywhere | ☐ | `app.UseHttpsRedirection()` |
| 2 | JWT tokens with short expiry (1hr) | ☐ | Refresh tokens for long sessions |
| 3 | CORS restricted to known origins | ☐ | No `AllowAnyOrigin()` in production |
| 4 | Rate limiting on all endpoints | ☐ | 100 req/min per client |
| 5 | All input validated server-side | ☐ | FluentValidation + DataAnnotations |
| 6 | XML injection prevented | ☐ | SecurityElement.Escape() |
| 7 | SQL injection prevented | ☐ | Parameterized queries only |
| 8 | SYSPRO creds in Key Vault | ☐ | Never in appsettings.json |
| 9 | Conn strings in Key Vault | ☐ | Never in source control |
| 10 | SYSPRO SQL user is read-only | ☐ | db_datareader only |
| 11 | Security headers on all responses | ☐ | HSTS, X-Frame-Options, etc. |
| 12 | Audit trail for all mutations | ☐ | AuditTrail table |
| 13 | Error messages don't leak internals | ☐ | Generic messages in production |
| 14 | Dependency scanning enabled | ☐ | `dotnet list package --vulnerable` |
| 15 | Secrets rotated quarterly | ☐ | Key Vault auto-rotation |

---

[← Back to Main Guide](../README.md) | [Previous: Error Handling](./06-ERROR-HANDLING.md) | [Next: Deployment →](./08-DEPLOYMENT.md)
