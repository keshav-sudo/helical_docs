namespace SysproIntegrationApi.Models;

public class Customer
{
    public string CustomerCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal Balance { get; set; }
}

public class CreateCustomerRequest
{
    public string CustomerCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? ShortName { get; set; }
    public string Branch { get; set; } = "01";
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string CustomerClass { get; set; } = "A";
    public decimal CreditLimit { get; set; } = 10000;
    public string? Currency { get; set; } = "USD";
    public string? TermsCode { get; set; } = "30";
    public string? Phone { get; set; }
    public string? ContactName { get; set; }
    public string? Email { get; set; }
}

public class CreateCustomerResult
{
    public bool Success { get; set; }
    public string CustomerCode { get; set; } = "";
    public string? Error { get; set; }
}
