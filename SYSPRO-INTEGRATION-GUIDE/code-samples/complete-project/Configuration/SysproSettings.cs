namespace SysproIntegrationApi.Configuration;

public class SysproSettings
{
    public string BaseUrl { get; set; } = "http://localhost:30000";
    public string Operator { get; set; } = "ADMIN";
    public string Password { get; set; } = "";
    public string CompanyId { get; set; } = "A";
    public string? CompanyPassword { get; set; }
    public int PoolSize { get; set; } = 5;
    public int SessionTimeoutSeconds { get; set; } = 1200;
    public int RequestTimeoutSeconds { get; set; } = 120;
}
