using System.Xml.Linq;
using SysproIntegrationApi.Models;

namespace SysproIntegrationApi.Services;

public class InventoryService
{
    private readonly SysproSessionPool _pool;
    private readonly SysproEnetClient _client;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        SysproSessionPool pool,
        SysproEnetClient client,
        ILogger<InventoryService> logger)
    {
        _pool = pool;
        _client = client;
        _logger = logger;
    }

    public async Task<InventoryItem?> GetItemAsync(
        string stockCode, 
        CancellationToken ct = default)
    {
        return await _pool.ExecuteAsync(async sessionId =>
        {
            var queryXml = $@"<Query>
                <Key>
                    <StockCode>{stockCode}</StockCode>
                </Key>
                <Option>
                    <IncludeAllWarehouses>Y</IncludeAllWarehouses>
                </Option>
            </Query>";

            var response = await _client.QueryAsync(sessionId, "INVQRY", queryXml, ct);
            return ParseInventoryItem(response);
        }, ct);
    }

    public async Task<List<InventoryItem>> SearchItemsAsync(
        string? warehouse = null,
        int maxRecords = 100,
        CancellationToken ct = default)
    {
        return await _pool.ExecuteAsync(async sessionId =>
        {
            var warehouseFilter = string.IsNullOrEmpty(warehouse) 
                ? "" 
                : $"<FilterByWarehouse>{warehouse}</FilterByWarehouse>";

            var queryXml = $@"<Query>
                <Key>
                    <StockCode FilterType=""A"" FilterValue=""""></StockCode>
                </Key>
                <Option>
                    <MaxRecords>{maxRecords}</MaxRecords>
                    {warehouseFilter}
                </Option>
            </Query>";

            var response = await _client.QueryAsync(sessionId, "INVQRY", queryXml, ct);
            return ParseInventoryItems(response);
        }, ct);
    }

    public async Task<InventoryMovementResult> PostMovementAsync(
        InventoryMovement movement,
        CancellationToken ct = default)
    {
        return await _pool.ExecuteAsync(async sessionId =>
        {
            var xml = $@"<PostInvMovements>
                <Item>
                    <Journal>{movement.JournalType}</Journal>
                    <StockCode>{movement.StockCode}</StockCode>
                    <Warehouse>{movement.Warehouse}</Warehouse>
                    <Quantity>{movement.Quantity}</Quantity>
                    <Reference>{movement.Reference}</Reference>
                    <Notation>{movement.Notes}</Notation>
                </Item>
            </PostInvMovements>";

            var response = await _client.TransactionAsync(sessionId, "INVTMM", xml, ct);
            
            _logger.LogInformation(
                "Posted inventory movement: {Type} {Qty} of {Stock} to {Warehouse}",
                movement.JournalType, movement.Quantity, movement.StockCode, movement.Warehouse);

            return new InventoryMovementResult { Success = true, Reference = movement.Reference };
        }, ct);
    }

    private InventoryItem? ParseInventoryItem(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var item = doc.Descendants("InvMaster").FirstOrDefault();
            
            if (item == null) return null;

            return new InventoryItem
            {
                StockCode = item.Element("StockCode")?.Value ?? "",
                Description = item.Element("Description")?.Value ?? "",
                QtyOnHand = decimal.TryParse(item.Element("QtyOnHand")?.Value, out var qoh) ? qoh : 0,
                QtyAvailable = decimal.TryParse(item.Element("QtyAvailable")?.Value, out var qa) ? qa : 0,
                QtyAllocated = decimal.TryParse(item.Element("QtyAllocated")?.Value, out var qal) ? qal : 0,
                UnitOfMeasure = item.Element("StockUom")?.Value ?? "EA",
                Warehouses = ParseWarehouses(doc)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse inventory response");
            return null;
        }
    }

    private List<InventoryItem> ParseInventoryItems(string xml)
    {
        var items = new List<InventoryItem>();
        try
        {
            var doc = XDocument.Parse(xml);
            foreach (var itemEl in doc.Descendants("InvMaster"))
            {
                items.Add(new InventoryItem
                {
                    StockCode = itemEl.Element("StockCode")?.Value ?? "",
                    Description = itemEl.Element("Description")?.Value ?? "",
                    QtyOnHand = decimal.TryParse(itemEl.Element("QtyOnHand")?.Value, out var qoh) ? qoh : 0,
                    QtyAvailable = decimal.TryParse(itemEl.Element("QtyAvailable")?.Value, out var qa) ? qa : 0
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse inventory list response");
        }
        return items;
    }

    private List<WarehouseStock> ParseWarehouses(XDocument doc)
    {
        var warehouses = new List<WarehouseStock>();
        foreach (var wh in doc.Descendants("Warehouse"))
        {
            warehouses.Add(new WarehouseStock
            {
                Warehouse = wh.Element("Warehouse")?.Value ?? "",
                QtyOnHand = decimal.TryParse(wh.Element("QtyOnHand")?.Value, out var q) ? q : 0
            });
        }
        return warehouses;
    }
}
