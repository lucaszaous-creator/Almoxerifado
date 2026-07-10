using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Domain.Entities.Stock;
using ALMOXPRO.Domain.Exceptions;

namespace ALMOXPRO.Domain.Entities.Movements;

/// <summary>Transferência de materiais entre almoxarifados.</summary>
public class StockTransfer : BaseEntity
{
    public string Number { get; set; } = string.Empty;

    public int SourceWarehouseId { get; set; }
    public Warehouse SourceWarehouse { get; set; } = null!;

    public int DestinationWarehouseId { get; set; }
    public Warehouse DestinationWarehouse { get; set; } = null!;

    public int UserId { get; set; }
    public DateTime TransferDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public ICollection<StockTransferItem> Items { get; set; } = new List<StockTransferItem>();

    public void Validate()
    {
        if (SourceWarehouseId == DestinationWarehouseId)
            throw new DomainException("O almoxarifado de origem deve ser diferente do destino.");
        if (Items.Count == 0)
            throw new DomainException("A transferência deve possuir ao menos um item.");
    }
}

public class StockTransferItem : BaseEntity
{
    public int StockTransferId { get; set; }
    public StockTransfer StockTransfer { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal Quantity { get; set; }

    public int? LotId { get; set; }
    public Lot? Lot { get; set; }
}
