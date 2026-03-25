using System.Xml.Linq;
using SysproIntegrationApi.Models;

namespace SysproIntegrationApi.Services;

public class CustomerService
{
    private readonly SysproSessionPool _pool;
    private readonly SysproEnetClient _client;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        SysproSessionPool pool,
        SysproEnetClient client,
        ILogger<CustomerService> logger)
    {
        _pool = pool;
        _client = client;
        _logger = logger;
    }

    public async Task<Customer?> GetCustomerAsync(
        string customerCode, 
        CancellationToken ct = default)
    {
        return await _pool.ExecuteAsync(async sessionId =>
        {
            var queryXml = $@"<Query>
                <Key>
                    <Customer>{customerCode}</Customer>
                </Key>
            </Query>";

            var response = await _client.QueryAsync(sessionId, "ARSQRY", queryXml, ct);
            return ParseCustomer(response);
        }, ct);
    }

    public async Task<List<Customer>> SearchCustomersAsync(
        string? name = null,
        int maxRecords = 100,
        CancellationToken ct = default)
    {
        return await _pool.ExecuteAsync(async sessionId =>
        {
            var nameFilter = string.IsNullOrEmpty(name) 
                ? "" 
                : $"<FilterByName>{name}</FilterByName>";

            var queryXml = $@"<Query>
                <Key>
                    <Customer FilterType=""A"" FilterValue=""""></Customer>
                </Key>
                <Option>
                    <MaxRecords>{maxRecords}</MaxRecords>
                    {nameFilter}
                </Option>
            </Query>";

            var response = await _client.QueryAsync(sessionId, "ARSQRY", queryXml, ct);
            return ParseCustomers(response);
        }, ct);
    }

    public async Task<CreateCustomerResult> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken ct = default)
    {
        return await _pool.ExecuteAsync(async sessionId =>
        {
            var xml = $@"<SetupArCustomer>
                <Item>
                    <Key>
                        <Customer>{request.CustomerCode}</Customer>
                    </Key>
                    <Name>{request.Name}</Name>
                    <ShortName>{request.ShortName ?? request.Name[..Math.Min(15, request.Name.Length)]}</ShortName>
                    <Branch>{request.Branch}</Branch>
                    <SoldToAddr1>{request.Address1 ?? ""}</SoldToAddr1>
                    <SoldToAddr2>{request.Address2 ?? ""}</SoldToAddr2>
                    <SoldToAddr3>{request.City ?? ""}</SoldToAddr3>
                    <SoldToAddr4>{request.State ?? ""}</SoldToAddr4>
                    <SoldToAddr5>{request.PostalCode ?? ""}</SoldToAddr5>
                    <CustomerClass>{request.CustomerClass}</CustomerClass>
                    <CreditLimit>{request.CreditLimit}</CreditLimit>
                    <Currency>{request.Currency ?? "USD"}</Currency>
                    <TermsCode>{request.TermsCode ?? "30"}</TermsCode>
                    <Telephone>{request.Phone ?? ""}</Telephone>
                    <Contact>{request.ContactName ?? ""}</Contact>
                    <Email>{request.Email ?? ""}</Email>
                </Item>
            </SetupArCustomer>";

            var response = await _client.TransactionAsync(sessionId, "ARSTOP", xml, ct);
            
            _logger.LogInformation("Created customer: {CustomerCode}", request.CustomerCode);

            return new CreateCustomerResult 
            { 
                Success = true, 
                CustomerCode = request.CustomerCode 
            };
        }, ct);
    }

    private Customer? ParseCustomer(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var cust = doc.Descendants("ArCustomer").FirstOrDefault();
            
            if (cust == null) return null;

            return new Customer
            {
                CustomerCode = cust.Element("Customer")?.Value ?? "",
                Name = cust.Element("Name")?.Value ?? "",
                Address1 = cust.Element("SoldToAddr1")?.Value,
                Address2 = cust.Element("SoldToAddr2")?.Value,
                City = cust.Element("SoldToAddr3")?.Value,
                State = cust.Element("SoldToAddr4")?.Value,
                PostalCode = cust.Element("SoldToAddr5")?.Value,
                Phone = cust.Element("Telephone")?.Value,
                Email = cust.Element("Email")?.Value,
                CreditLimit = decimal.TryParse(cust.Element("CreditLimit")?.Value, out var cl) ? cl : 0,
                Balance = decimal.TryParse(cust.Element("CurrentBalance")?.Value, out var b) ? b : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse customer response");
            return null;
        }
    }

    private List<Customer> ParseCustomers(string xml)
    {
        var customers = new List<Customer>();
        try
        {
            var doc = XDocument.Parse(xml);
            foreach (var cust in doc.Descendants("ArCustomer"))
            {
                customers.Add(new Customer
                {
                    CustomerCode = cust.Element("Customer")?.Value ?? "",
                    Name = cust.Element("Name")?.Value ?? "",
                    Phone = cust.Element("Telephone")?.Value,
                    Email = cust.Element("Email")?.Value
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse customer list response");
        }
        return customers;
    }
}
