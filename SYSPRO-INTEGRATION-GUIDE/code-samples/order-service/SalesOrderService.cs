using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace SysproIntegration.Services;

/// <summary>
/// Complete Sales Order service demonstrating:
/// 1. Building XML requests
/// 2. Calling SYSPRO e.net
/// 3. Parsing XML responses
/// 4. Error handling
/// </summary>
public class SalesOrderService
{
    private readonly SysproSessionPool _sessionPool;
    private readonly SysproEnetClient _client;
    private readonly ILogger<SalesOrderService> _logger;

    public SalesOrderService(
        SysproSessionPool sessionPool,
        SysproEnetClient client,
        ILogger<SalesOrderService> logger)
    {
        _sessionPool = sessionPool;
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Create a new sales order in SYSPRO
    /// </summary>
    public async Task<CreateOrderResult> CreateSalesOrderAsync(CreateOrderRequest request)
    {
        // Validate input first
        ValidateRequest(request);

        // Build XML for SYSPRO
        var xmlParams = BuildOrderParameters(request);
        var xmlDoc = BuildOrderDocument(request);

        _logger.LogInformation("Creating sales order for customer {Customer} with {LineCount} lines",
            request.CustomerId, request.Lines.Count);

        // Use pooled session
        using var pooledSession = await _sessionPool.AcquireSessionAsync();

        try
        {
            var response = await _client.TransactionAsync(
                pooledSession.SessionId,
                "SORTOI",  // Sales Order Transaction Input
                xmlParams,
                xmlDoc);

            // Parse response
            var result = ParseOrderResponse(response);

            _logger.LogInformation("Sales order created successfully. SYSPRO SO#: {SalesOrder}",
                result.SysproSalesOrderNumber);

            return result;
        }
        catch (SysproBusinessException ex)
        {
            _logger.LogWarning("SYSPRO rejected order: {Error}", ex.Message);
            return new CreateOrderResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Get sales order details from SYSPRO
    /// </summary>
    public async Task<SalesOrderDetails?> GetSalesOrderAsync(string salesOrderNumber)
    {
        using var pooledSession = await _sessionPool.AcquireSessionAsync();

        var xmlParams = $@"<?xml version=""1.0""?>
<Query>
    <TableName>SorMaster</TableName>
    <ReturnRows>1</ReturnRows>
    <Columns>
        <Column>SalesOrder</Column>
        <Column>Customer</Column>
        <Column>CustomerName</Column>
        <Column>OrderDate</Column>
        <Column>OrderStatus</Column>
        <Column>MerchandiseValue</Column>
        <Column>OrderTaxValue</Column>
    </Columns>
    <Filter>SalesOrder = '{salesOrderNumber}'</Filter>
</Query>";

        var response = await _client.QueryAsync(pooledSession.SessionId, "SORQRY", xmlParams);

        var doc = XDocument.Parse(response);
        var row = doc.Descendants("Row").FirstOrDefault();

        if (row == null) return null;

        return new SalesOrderDetails
        {
            SalesOrderNumber = row.Element("SalesOrder")?.Value ?? "",
            CustomerId = row.Element("Customer")?.Value ?? "",
            CustomerName = row.Element("CustomerName")?.Value ?? "",
            OrderDate = DateTime.Parse(row.Element("OrderDate")?.Value ?? DateTime.Now.ToString()),
            Status = row.Element("OrderStatus")?.Value ?? "",
            TotalValue = decimal.Parse(row.Element("MerchandiseValue")?.Value ?? "0"),
            TaxAmount = decimal.Parse(row.Element("OrderTaxValue")?.Value ?? "0")
        };
    }

    private void ValidateRequest(CreateOrderRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.CustomerId))
            errors.Add("Customer ID is required");

        if (!request.Lines.Any())
            errors.Add("At least one order line is required");

        foreach (var (line, index) in request.Lines.Select((l, i) => (l, i)))
        {
            if (string.IsNullOrWhiteSpace(line.StockCode))
                errors.Add($"Line {index + 1}: Stock code is required");

            if (line.Quantity <= 0)
                errors.Add($"Line {index + 1}: Quantity must be positive");
        }

        if (errors.Any())
            throw new ValidationException(string.Join("; ", errors));
    }

    private static string BuildOrderParameters(CreateOrderRequest request)
    {
        return $@"<?xml version=""1.0""?>
<PostSorMaster>
    <Parameters>
        <ValidateOnly>N</ValidateOnly>
        <IgnoreWarnings>N</IgnoreWarnings>
        <AllowNonStockItems>N</AllowNonStockItems>
        <AllowZeroPrice>N</AllowZeroPrice>
        <AllowPoNumberDuplicates>N</AllowPoNumberDuplicates>
        <DefaultMemoCode></DefaultMemoCode>
        <DefaultMemoNarrative></DefaultMemoNarrative>
        <DefaultSerialNumbers></DefaultSerialNumbers>
        <FixedExchangeRate></FixedExchangeRate>
        <FixedPrices></FixedPrices>
        <AllowApplyOrderDate>N</AllowApplyOrderDate>
    </Parameters>
</PostSorMaster>";
    }

    private static string BuildOrderDocument(CreateOrderRequest request)
    {
        var linesXml = string.Join("\n", request.Lines.Select(line => $@"
        <OrderDetails>
            <StockCode>{line.StockCode}</StockCode>
            <OrderQty>{line.Quantity}</OrderQty>
            <Price>{line.UnitPrice:F2}</Price>
            <Warehouse>{line.Warehouse ?? "WH01"}</Warehouse>
            <OrderUom></OrderUom>
        </OrderDetails>"));

        return $@"<?xml version=""1.0""?>
<PostSorMaster>
    <Orders>
        <OrderHeader>
            <Customer>{request.CustomerId}</Customer>
            <CustomerPoNumber>{request.CustomerPoNumber ?? ""}</CustomerPoNumber>
            <OrderDate>{request.OrderDate:yyyy-MM-dd}</OrderDate>
            <Warehouse>{request.Warehouse ?? "WH01"}</Warehouse>
            <OrderType>O</OrderType>
            <InvoiceTerms></InvoiceTerms>
            {linesXml}
        </OrderHeader>
    </Orders>
</PostSorMaster>";
    }

    private static CreateOrderResult ParseOrderResponse(string xmlResponse)
    {
        var doc = XDocument.Parse(xmlResponse);

        // Check for success
        var salesOrder = doc.Descendants("SalesOrder").FirstOrDefault()?.Value;

        if (!string.IsNullOrEmpty(salesOrder))
        {
            return new CreateOrderResult
            {
                Success = true,
                SysproSalesOrderNumber = salesOrder
            };
        }

        // Check for errors
        var error = doc.Descendants("ErrorDescription").FirstOrDefault()?.Value
                 ?? doc.Descendants("Message").FirstOrDefault()?.Value
                 ?? "Unknown error creating order";

        return new CreateOrderResult
        {
            Success = false,
            ErrorMessage = error
        };
    }
}

// DTOs
public class CreateOrderRequest
{
    public string CustomerId { get; set; } = "";
    public string? CustomerPoNumber { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.Today;
    public string? Warehouse { get; set; }
    public List<OrderLineRequest> Lines { get; set; } = new();
}

public class OrderLineRequest
{
    public string StockCode { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Warehouse { get; set; }
}

public class CreateOrderResult
{
    public bool Success { get; set; }
    public string? SysproSalesOrderNumber { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SalesOrderDetails
{
    public string SalesOrderNumber { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = "";
    public decimal TotalValue { get; set; }
    public decimal TaxAmount { get; set; }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
