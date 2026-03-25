namespace SysproIntegrationApi.Models;

// Inventory Models
public class InventoryItem
{
    public string StockCode { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal QtyOnHand { get; set; }
    public decimal QtyAvailable { get; set; }
    public decimal QtyAllocated { get; set; }
    public string UnitOfMeasure { get; set; } = "EA";
    public List<WarehouseStock> Warehouses { get; set; } = new();
}

public class WarehouseStock
{
    public string Warehouse { get; set; } = "";
    public decimal QtyOnHand { get; set; }
}

public class InventoryMovement
{
    public string StockCode { get; set; } = "";
    public string Warehouse { get; set; } = "01";
    public decimal Quantity { get; set; }
    public string JournalType { get; set; } = "IN"; // IN, OUT, ADJ, TRF
    public string Reference { get; set; } = "";
    public string? Notes { get; set; }
}

public class InventoryMovementResult
{
    public bool Success { get; set; }
    public string Reference { get; set; } = "";
    public string? Error { get; set; }
}
