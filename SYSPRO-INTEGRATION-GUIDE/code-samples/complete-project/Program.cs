using Serilog;
using SysproIntegrationApi.Configuration;
using SysproIntegrationApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure SYSPRO settings
builder.Services.Configure<SysproSettings>(
    builder.Configuration.GetSection("Syspro"));

// Register SYSPRO services
builder.Services.AddSingleton<SysproEnetClient>();
builder.Services.AddSingleton<SysproSessionPool>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<OrderService>();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<SysproHealthCheck>("syspro");

var app = builder.Build();

// Initialize session pool on startup
var sessionPool = app.Services.GetRequiredService<SysproSessionPool>();
await sessionPool.InitializeAsync();

app.UseSerilogRequestLogging();
app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/api/health");

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Log.Information("Shutting down, releasing SYSPRO sessions...");
    sessionPool.DisposeAsync().AsTask().Wait();
});

Log.Information("SYSPRO Integration API starting...");
app.Run();
