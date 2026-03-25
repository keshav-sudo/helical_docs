using System.Text;
using System.Xml.Linq;
using SysproIntegrationApi.Models;

namespace SysproIntegrationApi.Services;

public class OrderService
{
    private readonly SysproSessionPool _pool;
    private readonly SysproEnetClient _client;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        SysproSessionPool pool,
        SysproEnetClient client,
        ILogger<OrderService> logger)
    {
        _pool = pool;
        _client = client;
        _logger = logger;
    }

    public async Task<SalesOrder?> GetOrderAsync(
        string orderNumber, 
        CancellationToken ct = default)
    {
        return await _pool.ExecuteAsync(async sessionId =>
        {
            var queryXml = $@"<Query>
                <Key>
                    <SalesOrder>{orderNumber}</SalesOrder>
                </Key>
                <Option>
                    <IncludeLines>Y</IncludeLines>
                </Option>
            </Query>";

            var response = await _client.QueryAsync(sessionId, "SORQRY", queryXml, ct);
            return ParseOrder(response);
        }, ct);
    }

    public async Task<List<SalesOrderSummary>> SearchOrdersAsync(
        string? customerCode = null,
        string? status = null,
        int maxRecords = 100,
        CancellationToken ct = default)
    {
        return await _pool.ExecuteAsync(async sessionId =>
        {
            var filters = new StringBuilder();
            if (!string.IsNullOrEmpty(customerCode))
                filters.Append($"<FilterByCustomer>{customerCode}</FilterByCustomer>");
            if (!string.IsNullOrEmpty(status))
                filters.Append($"<FilterByStatus>{status}</FilterByStatus>");

            var queryXml = $@"<Query>
                <Key>
                    <SalesOrder FilterType=""A"" FilterValue=""""></SalesOrder>
                </Key>
                <Option>
                    <MaxRecords>{maxRecords}</MaxRecords>
                    <IncludeLines>N</IncludeLines>
                    {filters}
                </Option>
            </Query>";

            var response = await _client.QueryAsync(sessionId, "SORQRY", queryXml, ct);
            return ParseOrderSummaries(response);
        }, ct);
    }

    public async Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken ct = default)
    {
        return await _pool.ExecuteAsync(async sessionId =>
        {
            var linesXml = new StringBuilder();
            foreach (var line in request.Lines)
            {
                linesXml.Append($@"
                    <StockLine>
                        <StockCode>{line.StockCode}</StockCode>
                        <OrderQty>{line.Quantity}</OrderQty>
                        <Price>{line.Price}</Price>
                        <LineActionType>A</LineActionType>
                    </StockLine>");
            }

            var xml = $@"<OrderToBuild>
                <OrderHeader>
                    <Customer>{request.CustomerCode}</Customer>
                    <CustomerPoNumber>{request.CustomerPo ?? ""}</CustomerPoNumber>
                    <OrderDate>{DateTime.Now:yyyy-MM-dd}</OrderDate>
                    <RequiredDate>{request.RequiredDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.AddDays(7).ToString("yyyy-MM-dd")}</RequiredDate>
                    <Warehouse>{request.Warehouse ?? "01"}</Warehouse>
                    <ShipAddress1>{request.ShipAddress1 ?? ""}</ShipAddress1>
                    <ShipAddress2>{request.ShipAddress2 ?? ""}</ShipAddress2>
                    <SpecialInstrs>{request.Notes ?? ""}</SpecialInstrs>
                </OrderHeader>
                <OrderDetails>
                    {linesXml}
                </OrderDetails>
            </OrderToBuild>";

            var response = await _client.TransactionAsync(sessionId, "SORTOI", xml, ct);
            var orderNumber = ExtractOrderNumber(response);
            
            _logger.LogInformation(
                "Created sales order {OrderNumber} for customer {Customer}",
                orderNumber, request.CustomerCode);

            return new CreateOrderResult 
            { 
                Success = true, 
                SalesOrder = orderNumber,
                Total = request.Lines.Sum(l => l.Quantity * l.Price)
            };
        }, ct);
    }

    public async Task<UpdateOrderResult> UpdateOrderAsync(
        string orderNumber,
        UpdateOrderRequest request,
        CancellationToken ct = default)
    {
        return await _pool.ExecuteAsync(async sessionId =>
        {
            var linesXml = new StringBuilder();
            
            // Updates to existing lines
            foreach (var line in request.UpdateLines ?? [])
            {
                linesXml.Append($@"
                    <StockLine>
                        <SalesOrderLine>{line.LineNumber}</SalesOrderLine>
                        <OrderQty>{line.Quantity}</OrderQty>
                        <LineActionType>C</LineActionType>
                    </StockLine>");
            }
            
            // New lines
            foreach (var line in request.AddLines ?? [])
            {
                linesXml.Append($@"
                    <StockLine>
                        <StockCode>{line.StockCode}</StockCode>
                        <OrderQty>{line.Quantity}</OrderQty>
                        <Price>{line.Price}</Price>
                        <LineActionType>A</LineActionType>
                    </StockLine>");
            }

            var xml = $@"<OrderToBuild>
                <OrderHeader>
                    <SalesOrder>{orderNumber}</SalesOrder>
                    <SpecialInstrs>{request.Notes ?? ""}</SpecialInstrs>
                </OrderHeader>
                <OrderDetails>
                    {linesXml}
                </OrderDetails>
            </OrderToBuild>";

            var response = await _client.TransactionAsync(sessionId, "SORTOI", xml, ct);
            
            _logger.LogInformation("Updated sales order {OrderNumber}", orderNumber);

            return new UpdateOrderResult { Success = true, SalesOrder = orderNumber };
        }, ct);
    }

    private SalesOrder? ParseOrder(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var order = doc.Descendants("SorMaster").FirstOrDefault();
            
            if (order == null) return null;

            var lines = doc.Descendants("SorDetail")
                .Select(l => new SalesOrderLine
                {
                    LineNumber = int.TryParse(l.Element("SalesOrderLine")?.Value, out var ln) ? ln : 0,
                    StockCode = l.Element("StockCode")?.Value ?? "",
                    Description = l.Element("Description")?.Value ?? "",
                    Quantity = decimal.TryParse(l.Element("OrderQty")?.Value, out var q) ? q : 0,
                    Price = decimal.TryParse(l.Element("UnitPrice")?.Value, out var p) ? p : 0,
                    LineTotal = decimal.TryParse(l.Element("LineValue")?.Value, out var t) ? t : 0
                })
                .ToList();

            return new SalesOrder
            {
                OrderNumber = order.Element("SalesOrder")?.Value ?? "",
                CustomerCode = order.Element("Customer")?.Value ?? "",
                CustomerPo = order.Element("CustomerPoNumber")?.Value,
                OrderDate = DateTime.TryParse(order.Element("OrderDate")?.Value, out var od) ? od : DateTime.MinValue,
                RequiredDate = DateTime.TryParse(order.Element("RequiredDate")?.Value, out var rd) ? rd : null,
                Status = order.Element("OrderStatus")?.Value ?? "",
                Total = decimal.TryParse(order.Element("OrderValue")?.Value, out var ov) ? ov : 0,
                Lines = lines
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse order response");
            return null;
        }
    }

    private List<SalesOrderSummary> ParseOrderSummaries(string xml)
    {
        var orders = new List<SalesOrderSummary>();
        try
        {
            var doc = XDocument.Parse(xml);
            foreach (var order in doc.Descendants("SorMaster"))
            {
                orders.Add(new SalesOrderSummary
                {
                    OrderNumber = order.Element("SalesOrder")?.Value ?? "",
                    CustomerCode = order.Element("Customer")?.Value ?? "",
                    OrderDate = DateTime.TryParse(order.Element("OrderDate")?.Value, out var od) ? od : DateTime.MinValue,
                    Status = order.Element("OrderStatus")?.Value ?? "",
                    Total = decimal.TryParse(order.Element("OrderValue")?.Value, out var ov) ? ov : 0
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse order summaries");
        }
        return orders;
    }

    private string ExtractOrderNumber(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            return doc.Descendants("SalesOrder").FirstOrDefault()?.Value ?? "UNKNOWN";
        }
        catch
        {
            return "UNKNOWN";
        }
    }
}
