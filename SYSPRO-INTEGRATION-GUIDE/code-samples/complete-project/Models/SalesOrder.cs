namespace SysproIntegrationApi.Models;

public class SalesOrder
{
    public string OrderNumber { get; set; } = "";
    public string CustomerCode { get; set; } = "";
    public string? CustomerPo { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public List<SalesOrderLine> Lines { get; set; } = new();
}

public class SalesOrderLine
{
    public int LineNumber { get; set; }
    public string StockCode { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal LineTotal { get; set; }
}

public class SalesOrderSummary
{
    public string OrderNumber { get; set; } = "";
    public string CustomerCode { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
}

public class CreateOrderRequest
{
    public string CustomerCode { get; set; } = "";
    public string? CustomerPo { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Warehouse { get; set; } = "01";
    public string? ShipAddress1 { get; set; }
    public string? ShipAddress2 { get; set; }
    public string? Notes { get; set; }
    public List<OrderLineRequest> Lines { get; set; } = new();
}

public class OrderLineRequest
{
    public string StockCode { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
}

public class CreateOrderResult
{
    public bool Success { get; set; }
    public string SalesOrder { get; set; } = "";
    public decimal Total { get; set; }
    public string? Error { get; set; }
}

public class UpdateOrderRequest
{
    public string? Notes { get; set; }
    public List<UpdateLineRequest>? UpdateLines { get; set; }
    public List<OrderLineRequest>? AddLines { get; set; }
}

public class UpdateLineRequest
{
    public int LineNumber { get; set; }
    public decimal Quantity { get; set; }
}

public class UpdateOrderResult
{
    public bool Success { get; set; }
    public string SalesOrder { get; set; } = "";
    public string? Error { get; set; }
}
