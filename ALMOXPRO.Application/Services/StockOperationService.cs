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
        // Lote informado explicitamente: baixa direta naquele lote.
        if (lotId.HasValue)
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

        // Sem lote informado: consome os saldos por FEFO — o lote que vence
        // primeiro sai primeiro; saldos sem validade/sem lote saem por último.
        var items = (await _uow.Stock.GetItemsWithLotsAsync(productId, warehouseId, ct))
            .OrderBy(i => i.Lot?.ExpirationDate ?? DateOnly.MaxValue)
            .ThenBy(i => i.LotId.HasValue ? 0 : 1)
            .ToList();

        var available = items.Sum(i => i.Quantity);
        if (quantity > available)
        {
            var product = await _uow.Products.GetByIdAsync(productId, ct);
            throw new InsufficientStockException(product?.Name ?? $"#{productId}", quantity, available);
        }

        var remaining = quantity;
        var runningBalance = await _uow.Stock.GetTotalQuantityAsync(productId, warehouseId, ct);
        StockItem last = items[0];

        foreach (var item in items)
        {
            if (remaining <= 0)
                break;

            var take = Math.Min(remaining, item.Quantity);
            if (take <= 0)
                continue;

            item.Decrease(take);
            remaining -= take;
            runningBalance -= take;
            last = item;

            await _uow.Stock.AddMovementAsync(new StockMovement
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                LotId = item.LotId,
                Type = type,
                Quantity = -take,
                UnitCost = unitCost,
                BalanceAfter = runningBalance,
                DocumentType = documentType,
                DocumentId = documentId,
                DocumentNumber = documentNumber,
                UserId = userId,
                MovementDate = DateTime.UtcNow
            }, ct);
        }

        return last;
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
