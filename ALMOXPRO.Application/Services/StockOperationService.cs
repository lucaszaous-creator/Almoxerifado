using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Stock;
using ALMOXPRO.Domain.Exceptions;

namespace ALMOXPRO.Application.Services;

/// <summary>
/// Operações internas de baixa/acréscimo de saldo e escrita do kardex.
/// Usado pelos serviços de entrada, saída, transferência e inventário.
/// Deve ser chamado dentro de uma transação do Unit of Work.
/// </summary>
public interface IStockOperationService
{
    Task<StockItem> IncreaseAsync(int productId, int warehouseId, int? lotId, int? locationId,
        decimal quantity, decimal unitCost, StockMovementType type,
        string documentType, int documentId, string? documentNumber, int userId, CancellationToken ct = default);

    Task<StockItem> DecreaseAsync(int productId, int warehouseId, int? lotId,
        decimal quantity, decimal unitCost, StockMovementType type,
        string documentType, int documentId, string? documentNumber, int userId, CancellationToken ct = default);

    /// <summary>Ajusta o saldo para a quantidade contada (inventário).</summary>
    Task AdjustAsync(int productId, int warehouseId, int? lotId, decimal countedQuantity,
        string documentType, int documentId, string? documentNumber, int userId, CancellationToken ct = default);

    Task<Lot> GetOrCreateLotAsync(int productId, string lotNumber, DateOnly? expirationDate, CancellationToken ct = default);
}

public class StockOperationService : IStockOperationService
{
    private readonly IUnitOfWork _uow;

    public StockOperationService(IUnitOfWork uow) => _uow = uow;

    public async Task<StockItem> IncreaseAsync(int productId, int warehouseId, int? lotId, int? locationId,
        decimal quantity, decimal unitCost, StockMovementType type,
        string documentType, int documentId, string? documentNumber, int userId, CancellationToken ct = default)
    {
        var item = await _uow.Stock.GetItemAsync(productId, warehouseId, lotId, ct);
        if (item is null)
        {
            item = new StockItem
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                LotId = lotId,
                LocationId = locationId
            };
            await _uow.Stock.AddItemAsync(item, ct);
        }
        else if (locationId.HasValue)
        {
            item.LocationId = locationId;
        }

        item.Increase(quantity);
        await WriteMovementAsync(productId, warehouseId, lotId, type, quantity, unitCost,
            documentType, documentId, documentNumber, userId, ct);
        return item;
    }

    public async Task<StockItem> DecreaseAsync(int productId, int warehouseId, int? lotId,
        decimal quantity, decimal unitCost, StockMovementType type,
        string documentType, int documentId, string? documentNumber, int userId, CancellationToken ct = default)
    {
        var item = await _uow.Stock.GetItemAsync(productId, warehouseId, lotId, ct);
        if (item is null)
        {
            var product = await _uow.Products.GetByIdAsync(productId, ct);
            throw new InsufficientStockException(product?.Name ?? $"#{productId}", quantity, 0);
        }

        item.Decrease(quantity);
        await WriteMovementAsync(productId, warehouseId, lotId, type, -quantity, unitCost,
            documentType, documentId, documentNumber, userId, ct);
        return item;
    }

    public async Task AdjustAsync(int productId, int warehouseId, int? lotId, decimal countedQuantity,
        string documentType, int documentId, string? documentNumber, int userId, CancellationToken ct = default)
    {
        var item = await _uow.Stock.GetItemAsync(productId, warehouseId, lotId, ct);
        if (item is null)
        {
            item = new StockItem { ProductId = productId, WarehouseId = warehouseId, LotId = lotId };
            await _uow.Stock.AddItemAsync(item, ct);
        }

        var difference = countedQuantity - item.Quantity;
        if (difference == 0)
            return;

        item.SetQuantity(countedQuantity);

        var product = await _uow.Products.GetByIdAsync(productId, ct)
            ?? throw new DomainException("Produto não encontrado para ajuste.");

        await WriteMovementAsync(productId, warehouseId, lotId, StockMovementType.AjusteInventario,
            difference, product.AverageCost, documentType, documentId, documentNumber, userId, ct);
    }

    public async Task<Lot> GetOrCreateLotAsync(int productId, string lotNumber, DateOnly? expirationDate, CancellationToken ct = default)
    {
        var lot = await _uow.Lots.GetByNumberAsync(productId, lotNumber.Trim(), ct);
        if (lot is not null)
        {
            if (expirationDate.HasValue && lot.ExpirationDate != expirationDate)
                lot.ExpirationDate = expirationDate;
            return lot;
        }

        lot = new Lot { ProductId = productId, LotNumber = lotNumber.Trim(), ExpirationDate = expirationDate };
        await _uow.Lots.AddAsync(lot, ct);
        await _uow.SaveChangesAsync(ct);
        return lot;
    }

    private async Task WriteMovementAsync(int productId, int warehouseId, int? lotId,
        StockMovementType type, decimal signedQuantity, decimal unitCost,
        string documentType, int documentId, string? documentNumber, int userId, CancellationToken ct)
    {
        var balance = await _uow.Stock.GetTotalQuantityAsync(productId, warehouseId, ct);
        await _uow.Stock.AddMovementAsync(new StockMovement
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            LotId = lotId,
            Type = type,
            Quantity = signedQuantity,
            UnitCost = unitCost,
            BalanceAfter = balance + signedQuantity,
            DocumentType = documentType,
            DocumentId = documentId,
            DocumentNumber = documentNumber,
            UserId = userId,
            MovementDate = DateTime.UtcNow
        }, ct);
    }
}
