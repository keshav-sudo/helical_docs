using Microsoft.AspNetCore.Mvc;
using SysproIntegrationApi.Models;
using SysproIntegrationApi.Services;

namespace SysproIntegrationApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly InventoryService _inventoryService;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        InventoryService inventoryService,
        ILogger<InventoryController> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    /// <summary>
    /// Get inventory item by stock code
    /// </summary>
    [HttpGet("{stockCode}")]
    public async Task<ActionResult<InventoryItem>> GetItem(
        string stockCode, 
        CancellationToken ct)
    {
        var item = await _inventoryService.GetItemAsync(stockCode, ct);
        
        if (item == null)
            return NotFound(new { error = $"Stock code '{stockCode}' not found" });
        
        return Ok(item);
    }

    /// <summary>
    /// Search inventory items
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<InventoryItem>>> SearchItems(
        [FromQuery] string? warehouse,
        [FromQuery] int maxRecords = 100,
        CancellationToken ct = default)
    {
        var items = await _inventoryService.SearchItemsAsync(warehouse, maxRecords, ct);
        return Ok(items);
    }

    /// <summary>
    /// Post inventory movement (receipt, issue, adjustment)
    /// </summary>
    [HttpPost("movement")]
    public async Task<ActionResult<InventoryMovementResult>> PostMovement(
        [FromBody] InventoryMovement movement,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(movement.StockCode))
            return BadRequest(new { error = "StockCode is required" });
        
        if (movement.Quantity <= 0)
            return BadRequest(new { error = "Quantity must be positive" });

        try
        {
            var result = await _inventoryService.PostMovementAsync(movement, ct);
            return Ok(result);
        }
        catch (SysproException ex)
        {
            _logger.LogError(ex, "Failed to post inventory movement");
            return BadRequest(new { error = ex.Message });
        }
    }
}
