using Microsoft.AspNetCore.Mvc;
using SysproIntegrationApi.Models;
using SysproIntegrationApi.Services;

namespace SysproIntegrationApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        OrderService orderService,
        ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Get sales order by order number
    /// </summary>
    [HttpGet("{orderNumber}")]
    public async Task<ActionResult<SalesOrder>> GetOrder(
        string orderNumber, 
        CancellationToken ct)
    {
        var order = await _orderService.GetOrderAsync(orderNumber, ct);
        
        if (order == null)
            return NotFound(new { error = $"Order '{orderNumber}' not found" });
        
        return Ok(order);
    }

    /// <summary>
    /// Search sales orders
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<SalesOrderSummary>>> SearchOrders(
        [FromQuery] string? customer,
        [FromQuery] string? status,
        [FromQuery] int maxRecords = 100,
        CancellationToken ct = default)
    {
        var orders = await _orderService.SearchOrdersAsync(customer, status, maxRecords, ct);
        return Ok(orders);
    }

    /// <summary>
    /// Create new sales order
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreateOrderResult>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.CustomerCode))
            return BadRequest(new { error = "CustomerCode is required" });
        
        if (request.Lines == null || request.Lines.Count == 0)
            return BadRequest(new { error = "At least one order line is required" });

        try
        {
            var result = await _orderService.CreateOrderAsync(request, ct);
            return CreatedAtAction(
                nameof(GetOrder), 
                new { orderNumber = result.SalesOrder }, 
                result);
        }
        catch (SysproException ex)
        {
            _logger.LogError(ex, "Failed to create order");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update existing sales order
    /// </summary>
    [HttpPut("{orderNumber}")]
    public async Task<ActionResult<UpdateOrderResult>> UpdateOrder(
        string orderNumber,
        [FromBody] UpdateOrderRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _orderService.UpdateOrderAsync(orderNumber, request, ct);
            return Ok(result);
        }
        catch (SysproException ex)
        {
            _logger.LogError(ex, "Failed to update order");
            return BadRequest(new { error = ex.Message });
        }
    }
}
