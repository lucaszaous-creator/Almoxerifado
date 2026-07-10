using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Shared.Pagination;

namespace ALMOXPRO.Application.Services;

public interface IStockQueryService
{
    Task<List<StockBalanceDto>> GetBalancesAsync(CancellationToken ct = default);
    Task<PagedResult<StockMovementDto>> GetMovementsAsync(PagedQuery query, int? productId = null,
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
}

public class StockQueryService : IStockQueryService
{
    private readonly IUnitOfWork _uow;

    public StockQueryService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<StockBalanceDto>> GetBalancesAsync(CancellationToken ct = default)
    {
        var items = await _uow.Stock.GetAllWithDetailsAsync(ct);
        return items.Select(i => new StockBalanceDto(
            i.ProductId,
            i.Product.InternalCode,
            i.Product.Name,
            i.Product.Unit,
            i.Warehouse.Name,
            i.Location?.Code,
            i.Lot?.LotNumber,
            i.Lot?.ExpirationDate,
            i.Quantity,
            i.Product.AverageCost,
            Math.Round(i.Quantity * i.Product.AverageCost, 2),
            i.Product.MinStock)).ToList();
    }

    public async Task<PagedResult<StockMovementDto>> GetMovementsAsync(PagedQuery query, int? productId = null,
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var page = await _uow.Stock.GetMovementsAsync(query, productId, from, to, ct);
        return new PagedResult<StockMovementDto>
        {
            Items = page.Items.Select(m => new StockMovementDto(
                m.Id, m.MovementDate, m.Product.Name, m.Warehouse.Name, m.Type,
                m.Quantity, m.UnitCost, m.BalanceAfter, m.DocumentType, m.DocumentNumber)).ToList(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }
}
