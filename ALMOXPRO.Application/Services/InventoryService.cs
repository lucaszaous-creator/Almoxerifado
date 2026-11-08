using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Movements;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Results;

namespace ALMOXPRO.Application.Services;

public interface IInventoryService
{
    Task<PagedResult<InventoryListDto>> SearchAsync(PagedQuery query, CancellationToken ct = default);
    Task<Result<int>> OpenAsync(InventoryOpenDto dto, CancellationToken ct = default);
    Task<List<InventoryItemDto>> GetItemsAsync(int inventoryId, CancellationToken ct = default);
    Task<Result> RegisterCountAsync(int inventoryId, int itemId, decimal counted, CancellationToken ct = default);
    /// <summary>Registra contagem localizando o item pelo código de barras/QR do produto.</summary>
    Task<Result<InventoryItemDto>> RegisterCountByCodeAsync(int inventoryId, string code, decimal counted, CancellationToken ct = default);
    Task<Result> ApplyAdjustmentsAsync(int inventoryId, CancellationToken ct = default);
    Task<Result> FinishAsync(int inventoryId, CancellationToken ct = default);
    Task<Result> CancelAsync(int inventoryId, CancellationToken ct = default);
}

public class InventoryService : IInventoryService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockOperationService _stock;
    private readonly ICurrentSession _session;

    public InventoryService(IUnitOfWork uow, IStockOperationService stock, ICurrentSession session)
    {
        _uow = uow;
        _stock = stock;
        _session = session;
    }

    public async Task<PagedResult<InventoryListDto>> SearchAsync(PagedQuery query, CancellationToken ct = default)
    {
        var page = await _uow.Inventories.SearchAsync(query, ct);
        return new PagedResult<InventoryListDto>
        {
            Items = page.Items.Select(i => new InventoryListDto(
                i.Id, i.Number, i.Type, i.Status, i.Warehouse.Name, i.StartedAt, i.FinishedAt,
                i.Items.Count,
                i.Items.Count(x => x.CountedQuantity.HasValue),
                i.Items.Count(x => x.HasDifference))).ToList(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    public async Task<Result<int>> OpenAsync(InventoryOpenDto dto, CancellationToken ct = default)
    {
        if (dto.WarehouseId <= 0)
            return Result.Failure<int>("Selecione o almoxarifado.");

        var hasOpen = await _uow.Inventories.AnyAsync(
            i => i.WarehouseId == dto.WarehouseId &&
                 (i.Status == InventoryStatus.Aberto || i.Status == InventoryStatus.EmContagem), ct);
        if (hasOpen)
            return Result.Failure<int>("Já existe um inventário em andamento para este almoxarifado.");

        var inventoryId = 0;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var inventory = new InventoryCount
            {
                Number = await _uow.Sequences.NextNumberAsync("inventory", "INV", ct),
                Type = dto.Type,
                WarehouseId = dto.WarehouseId,
                UserId = _session.UserId ?? 0,
                Notes = dto.Notes
            };

            // Congela os saldos do sistema no momento da abertura.
            var stockItems = await _uow.Stock.GetByWarehouseAsync(dto.WarehouseId, ct);
            foreach (var stockItem in stockItems)
            {
                if (dto.CategoryIds.Count > 0 && !dto.CategoryIds.Contains(stockItem.Product.CategoryId))
                    continue;

                inventory.Items.Add(new InventoryCountItem
                {
                    ProductId = stockItem.ProductId,
                    LotId = stockItem.LotId,
                    SystemQuantity = stockItem.Quantity
                });
            }

            inventory.Start();
            await _uow.Inventories.AddAsync(inventory, ct);
            await _uow.SaveChangesAsync(ct);
            inventoryId = inventory.Id;
        }, ct);

        return Result.Success(inventoryId);
    }

    public async Task<List<InventoryItemDto>> GetItemsAsync(int inventoryId, CancellationToken ct = default)
    {
        var inventory = await _uow.Inventories.GetWithItemsAsync(inventoryId, ct);
        if (inventory is null)
            return [];

        return inventory.Items.Select(i => new InventoryItemDto(
            i.Id, i.ProductId, i.Product.InternalCode, i.Product.Name, i.Product.Barcode,
            i.Lot?.LotNumber, i.SystemQuantity, i.CountedQuantity, i.Difference, i.Adjusted)).ToList();
    }

    public async Task<Result> RegisterCountAsync(int inventoryId, int itemId, decimal counted, CancellationToken ct = default)
    {
        var inventory = await _uow.Inventories.GetWithItemsAsync(inventoryId, ct);
        if (inventory is null)
            return Result.Failure("Inventário não encontrado.");
        if (inventory.Status != InventoryStatus.EmContagem)
            return Result.Failure("O inventário não está em contagem.");

        var item = inventory.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Result.Failure("Item do inventário não encontrado.");

        item.RegisterCount(counted, _session.UserId ?? 0);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<InventoryItemDto>> RegisterCountByCodeAsync(int inventoryId, string code,
        decimal counted, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByAnyCodeAsync(code.Trim(), ct);
        if (product is null)
            return Result.Failure<InventoryItemDto>($"Nenhum produto encontrado para o código '{code}'.");

        var inventory = await _uow.Inventories.GetWithItemsAsync(inventoryId, ct);
        if (inventory is null)
            return Result.Failure<InventoryItemDto>("Inventário não encontrado.");
        if (inventory.Status != InventoryStatus.EmContagem)
            return Result.Failure<InventoryItemDto>("O inventário não está em contagem.");

        var item = inventory.Items.FirstOrDefault(i => i.ProductId == product.Id && !i.CountedQuantity.HasValue)
                   ?? inventory.Items.FirstOrDefault(i => i.ProductId == product.Id);
        if (item is null)
        {
            // Produto não estava no saldo: inclui com saldo de sistema zero.
            item = new InventoryCountItem { ProductId = product.Id, SystemQuantity = 0 };
            inventory.Items.Add(item);
        }

        item.RegisterCount(counted, _session.UserId ?? 0);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new InventoryItemDto(
            item.Id, product.Id, product.InternalCode, product.Name, product.Barcode,
            item.Lot?.LotNumber, item.SystemQuantity, item.CountedQuantity, item.Difference, item.Adjusted));
    }

    public async Task<Result> ApplyAdjustmentsAsync(int inventoryId, CancellationToken ct = default)
    {
        var inventory = await _uow.Inventories.GetWithItemsAsync(inventoryId, ct);
        if (inventory is null)
            return Result.Failure("Inventário não encontrado.");
        if (inventory.Status != InventoryStatus.EmContagem)
            return Result.Failure("O inventário não está em contagem.");

        var userId = _session.UserId ?? 0;

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            foreach (var item in inventory.Items.Where(i => i.HasDifference && !i.Adjusted))
            {
                await _stock.AdjustAsync(item.ProductId, inventory.WarehouseId, item.LotId,
                    item.CountedQuantity!.Value, "Inventario", inventory.Id, inventory.Number, userId, ct);
                item.Adjusted = true;
                await _uow.SaveChangesAsync(ct);
            }
        }, ct);

        return Result.Success();
    }

    public async Task<Result> FinishAsync(int inventoryId, CancellationToken ct = default)
    {
        var inventory = await _uow.Inventories.GetWithItemsAsync(inventoryId, ct);
        if (inventory is null)
            return Result.Failure("Inventário não encontrado.");

        try
        {
            inventory.Finish();
        }
        catch (Domain.Exceptions.DomainException ex)
        {
            return Result.Failure(ex.Message);
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> CancelAsync(int inventoryId, CancellationToken ct = default)
    {
        var inventory = await _uow.Inventories.GetByIdAsync(inventoryId, ct);
        if (inventory is null)
            return Result.Failure("Inventário não encontrado.");

        try
        {
            inventory.Cancel();
        }
        catch (Domain.Exceptions.DomainException ex)
        {
            return Result.Failure(ex.Message);
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
