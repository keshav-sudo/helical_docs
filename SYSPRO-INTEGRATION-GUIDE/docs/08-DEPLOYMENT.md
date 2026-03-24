# Part 8: Deployment & Infrastructure (Production)

[← Back to Main Guide](../README.md) | [Previous: Security](./07-SECURITY-AUTH.md) | [Next: Best Practices →](./09-BEST-PRACTICES.md)

---

## 8.1 Deployment Architecture — Complete Options

```
┌──────────────────────────────────────────────────────────────────────┐
│              DEPLOYMENT ARCHITECTURE OPTIONS                          │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  OPTION A: ALL ON-PREMISE (Simplest)                                 │
│  ────────────────────────────────────                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │ Your API     │  │ SYSPRO       │  │ SQL Server   │              │
│  │ (IIS on      │  │ App Server   │  │              │              │
│  │  Windows)    │──│ (same/near   │──│ (same/near   │              │
│  │              │  │  server)     │  │  server)     │              │
│  └──────────────┘  └──────────────┘  └──────────────┘              │
│                                                                       │
│  ✅ No VPN needed   ✅ Lowest latency   ❌ No cloud scale          │
│                                                                       │
│  OPTION B: HYBRID — API IN CLOUD (Most Common)                      │
│  ──────────────────────────────────────────────                      │
│  ┌─────────────────┐        VPN/Express     ┌────────────────┐     │
│  │ Azure / AWS     │──────────Route ────────│ On-Premise     │     │
│  │                 │                         │                │     │
│  │ ┌─────────┐    │                         │ ┌──────────┐  │     │
│  │ │ App     │    │                         │ │ SYSPRO   │  │     │
│  │ │ Service │    │         Encrypted       │ │ App Svr  │  │     │
│  │ │ (.NET)  │────│─────────Tunnel ─────────│─│          │  │     │
│  │ └─────────┘    │                         │ └──────────┘  │     │
│  │ ┌─────────┐    │                         │ ┌──────────┐  │     │
│  │ │ Azure   │    │                         │ │ SQL      │  │     │
│  │ │ SQL     │    │                         │ │ Server   │  │     │
│  │ │ (local  │    │                         │ │ (SYSPRO) │  │     │
│  │ │  DB)    │    │                         │ └──────────┘  │     │
│  │ └─────────┘    │                         │                │     │
│  └─────────────────┘                         └────────────────┘     │
│                                                                       │
│  ✅ Cloud scale   ✅ Global access   ⚠ VPN critical path           │
│                                                                       │
│  OPTION C: FULL CLOUD (SYSPRO 8+ Cloud)                              │
│  ──────────────────────────────────────                               │
│  Everything in Azure/AWS. SYSPRO hosted by SYSPRO Cloud.            │
│  ✅ Maximum scale  ✅ No VPN  ❌ Not available for SYSPRO 7        │
│                                                                       │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 8.2 IIS Deployment (On-Premise)

### Step-by-Step IIS Setup

```bash
# 1. Build the project
dotnet publish -c Release -o ./publish

# 2. create IIS site pointing to ./publish folder
# 3. Install .NET 8 Hosting Bundle on the server
# 4. Configure application pool:
#    - No Managed Code (for .NET 8)
#    - Identity: service account with network access to SYSPRO
```

### web.config for IIS

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" 
           modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet" 
                arguments=".\SysproIntegration.Api.dll"
                stdoutLogEnabled="true"
                stdoutLogFile=".\logs\stdout"
                hostingModel="InProcess">
      <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        <environmentVariable name="ASPNETCORE_URLS" value="http://*:5000" />
      </environmentVariables>
    </aspNetCore>
    <security>
      <requestFiltering>
        <requestLimits maxAllowedContentLength="52428800" />
      </requestFiltering>
    </security>
  </system.webServer>
</configuration>
```

---

## 8.3 Docker Deployment

### Dockerfile (Multi-Stage Build)

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first (layer caching)
COPY src/SysproIntegration.Api/*.csproj ./Api/
COPY src/SysproIntegration.Core/*.csproj ./Core/
COPY src/SysproIntegration.Infrastructure/*.csproj ./Infrastructure/
RUN dotnet restore ./Api/SysproIntegration.Api.csproj

# Copy source and build
COPY src/ .
RUN dotnet publish ./Api/SysproIntegration.Api.csproj \
    -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Security: run as non-root
RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s \
    CMD curl -f http://localhost:8080/health || exit 1

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SysproIntegration.Api.dll"]
```

### docker-compose.yml (Development)

```yaml
version: '3.8'
services:
  api:
    build: .
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__LocalDb=Server=localdb;Database=OMS;User=sa;Password=Dev@12345
      - Syspro__ServerUrl=http://syspro-server:30661
      - Syspro__Operator=API_USER
      - Syspro__OperatorPassword=dev-password
    depends_on:
      - localdb
    networks:
      - syspro-net

  localdb:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      SA_PASSWORD: "Dev@12345"
      ACCEPT_EULA: "Y"
    ports:
      - "1434:1433"
    volumes:
      - sqldata:/var/opt/mssql
    networks:
      - syspro-net

  seq:
    image: datalust/seq:latest
    ports:
      - "5341:80"
    environment:
      ACCEPT_EULA: Y
    networks:
      - syspro-net

volumes:
  sqldata:

networks:
  syspro-net:
    driver: bridge
```

---

## 8.4 CI/CD Pipeline — GitHub Actions

```yaml
# .github/workflows/deploy.yml
name: Build, Test & Deploy

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

env:
  DOTNET_VERSION: '8.0.x'
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Test
        run: dotnet test --no-build --configuration Release --verbosity normal
             --collect:"XPlat Code Coverage"

      - name: Upload coverage
        uses: codecov/codecov-action@v3
        with:
          files: '**/coverage.cobertura.xml'

  security-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Check vulnerable packages
        run: |
          dotnet restore
          dotnet list package --vulnerable --include-transitive 2>&1 | tee vuln-report.txt
          if grep -q "has the following vulnerable packages" vuln-report.txt; then
            echo "::warning::Vulnerable packages detected!"
          fi

  deploy-staging:
    needs: [build-and-test, security-scan]
    if: github.ref == 'refs/heads/develop'
    runs-on: ubuntu-latest
    environment: staging
    steps:
      - uses: actions/checkout@v4

      - name: Build Docker image
        run: docker build -t ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:staging .

      - name: Push to registry
        run: |
          echo "${{ secrets.GITHUB_TOKEN }}" | docker login ghcr.io -u $ --password-stdin
          docker push ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:staging

      - name: Deploy to Azure App Service (Staging)
        uses: azure/webapps-deploy@v2
        with:
          app-name: syspro-api-staging
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:staging

  deploy-production:
    needs: [build-and-test, security-scan]
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    environment: production
    steps:
      - uses: actions/checkout@v4

      - name: Build Docker image
        run: |
          docker build -t ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.sha }} .
          docker tag ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.sha }} \
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:latest

      - name: Push to registry
        run: |
          echo "${{ secrets.GITHUB_TOKEN }}" | docker login ghcr.io -u $ --password-stdin
          docker push ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.sha }}
          docker push ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:latest

      - name: Deploy to Azure App Service (Production)
        uses: azure/webapps-deploy@v2
        with:
          app-name: syspro-api-production
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.sha }}
```

---

## 8.5 Monitoring & Observability

### Application Insights / Prometheus Metrics

```csharp
// Custom metrics for SYSPRO integration monitoring
public class SysproMetrics
{
    // Track in your monitoring tool (App Insights, Prometheus, Datadog):
    
    // 1. e.net call duration (histogram)
    //    Labels: business_object, status
    //    Alert: p95 > 3000ms
    
    // 2. Session pool utilization (gauge)
    //    current_active / pool_size
    //    Alert: > 80% for 5 minutes
    
    // 3. Sync queue depth (gauge)
    //    COUNT(*) FROM Orders WHERE SyncStatus IN ('Pending','Retrying')
    //    Alert: > 100 orders pending
    
    // 4. Dead letter count (counter)
    //    Alert: ANY new dead letter → immediate alert
    
    // 5. Error rate (rate)
    //    failed_requests / total_requests per 5 min window
    //    Alert: > 5% error rate
    
    // 6. Circuit breaker state (gauge)
    //    0=closed, 1=half-open, 2=open
    //    Alert: state == 2 (open)
}
```

### Health Check Endpoint (Enhanced)

```csharp
// Custom health check for SYSPRO connectivity
public class SysproHealthCheck : IHealthCheck
{
    private readonly ISysproSessionManager _sessionManager;
    private readonly SysproEnetClient _client;

    public SysproHealthCheck(ISysproSessionManager sessionManager, SysproEnetClient client)
    {
        _sessionManager = sessionManager;
        _client = client;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct)
    {
        try
        {
            // Try to acquire and release a session
            var sessionId = await _sessionManager.AcquireSessionAsync(ct);
            _sessionManager.ReleaseSession(sessionId);
            
            return HealthCheckResult.Healthy("SYSPRO is reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "SYSPRO is unreachable", ex);
        }
    }
}

// Register:
builder.Services.AddHealthChecks()
    .AddCheck<SysproHealthCheck>("syspro", tags: new[] { "ready" });
```

---

## 8.6 Blue-Green / Canary Deployment

```
BLUE-GREEN DEPLOYMENT FOR SYSPRO API
═════════════════════════════════════

Step 1: BLUE (current production) is serving all traffic
  Load Balancer → [BLUE API v1.0] → SYSPRO

Step 2: Deploy GREEN (new version) alongside BLUE
  Load Balancer → [BLUE API v1.0] → SYSPRO
                  [GREEN API v1.1] → SYSPRO (not receiving traffic)

Step 3: Run smoke tests against GREEN
  • POST /api/orders (test order)
  • GET /api/inventory
  • GET /health
  All pass? ✅ Continue. Any fail? ❌ Rollback.

Step 4: Switch traffic to GREEN
  Load Balancer → [GREEN API v1.1] → SYSPRO
                  [BLUE API v1.0] (standby, ready for rollback)

Step 5: Monitor for 30 minutes
  • Error rate normal? ✅
  • Latency normal? ✅
  • Dead letters? ❌ None

Step 6: Decommission BLUE
  Load Balancer → [GREEN API v1.1] → SYSPRO
  (BLUE removed)

⚠ CRITICAL: Both BLUE and GREEN share the SAME SYSPRO session pool.
   Ensure pool size can handle both temporarily being active.
```

---

## 8.7 Disaster Recovery

```
┌──────────────────────────────────────────────────────────────────┐
│                  DISASTER RECOVERY PLAN                           │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  SCENARIO 1: Your API is down                                    │
│  ─────────────────────────────                                    │
│  Impact: Frontends can't create orders                           │
│  Recovery: Restart API / deploy from backup                      │
│  Data loss: None (orders queued in staging DB before API crash)  │
│  RTO: 15 minutes                                                 │
│                                                                   │
│  SCENARIO 2: SYSPRO is down (maintenance)                        │
│  ────────────────────────────────────────                         │
│  Impact: Orders queue up in staging DB, not synced               │
│  Recovery: When SYSPRO comes back, worker processes queued orders│
│  Data loss: None (all orders are in local DB)                    │
│  RTO: Automatic when SYSPRO returns                              │
│                                                                   │
│  SCENARIO 3: Local DB is down                                    │
│  ─────────────────────────────                                    │
│  Impact: Can't create or view orders                             │
│  Recovery: Restore from backup, replay transaction log           │
│  Data loss: Depends on backup frequency                          │
│  RTO: 30-60 minutes (SQL restore)                                │
│  Prevention: SQL Always On Availability Groups                   │
│                                                                   │
│  SCENARIO 4: VPN tunnel is down (cloud to on-prem)               │
│  ───────────────────────────────────────────────                  │
│  Impact: Same as SYSPRO down — orders queue up                   │
│  Recovery: Network team restores VPN                              │
│  Data loss: None                                                  │
│  RTO: Depends on network team                                    │
│  Prevention: Redundant VPN tunnels                               │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

---

[← Back to Main Guide](../README.md) | [Previous: Security](./07-SECURITY-AUTH.md) | [Next: Best Practices →](./09-BEST-PRACTICES.md)
