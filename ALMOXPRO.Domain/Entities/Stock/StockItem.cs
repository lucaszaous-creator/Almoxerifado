using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Domain.Exceptions;

namespace ALMOXPRO.Domain.Entities.Stock;

/// <summary>
/// Saldo de estoque de um produto por almoxarifado (e lote, quando controlado).
/// </summary>
public class StockItem : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public int? LocationId { get; set; }
    public Location? Location { get; set; }

    public int? LotId { get; set; }
    public Lot? Lot { get; set; }

    public decimal Quantity { get; private set; }

    public void Increase(decimal quantity)
    {
        if (quantity <= 0)
            throw new DomainException("A quantidade da movimentação deve ser maior que zero.");
        Quantity += quantity;
    }

    public void Decrease(decimal quantity)
    {
        if (quantity <= 0)
            throw new DomainException("A quantidade da movimentação deve ser maior que zero.");
        if (quantity > Quantity)
            throw new InsufficientStockException(Product?.Name ?? $"#{ProductId}", quantity, Quantity);
        Quantity -= quantity;
    }

    /// <summary>Define o saldo diretamente (usado apenas em ajuste de inventário).</summary>
    public void SetQuantity(decimal quantity)
    {
        if (quantity < 0)
            throw new DomainException("O saldo de estoque não pode ser negativo.");
        Quantity = quantity;
    }
}
