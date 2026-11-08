using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Catalog;

namespace ALMOXPRO.Domain.Entities.Stock;

/// <summary>
/// Kardex: registro histórico de toda movimentação de estoque.
/// </summary>
public class StockMovement
{
    public long Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public int? LotId { get; set; }
    public Lot? Lot { get; set; }

    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }

    /// <summary>Saldo do produto no almoxarifado após a movimentação.</summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>Documento de origem: Entrada, Saida, Transferencia, Inventario.</summary>
    public string DocumentType { get; set; } = string.Empty;
    public int DocumentId { get; set; }
    public string? DocumentNumber { get; set; }

    public int UserId { get; set; }
    public DateTime MovementDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
