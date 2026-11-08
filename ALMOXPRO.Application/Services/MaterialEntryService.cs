using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Movements;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Results;
using FluentValidation;

namespace ALMOXPRO.Application.Services;

public interface IMaterialEntryService
{
    Task<PagedResult<EntryListDto>> SearchAsync(PagedQuery query, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(EntryCreateDto dto, CancellationToken ct = default);
}

public class MaterialEntryService : IMaterialEntryService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockOperationService _stock;
    private readonly ICurrentSession _session;
    private readonly IValidator<EntryCreateDto> _validator;

    public MaterialEntryService(IUnitOfWork uow, IStockOperationService stock,
        ICurrentSession session, IValidator<EntryCreateDto> validator)
    {
        _uow = uow;
        _stock = stock;
        _session = session;
        _validator = validator;
    }

    public async Task<PagedResult<EntryListDto>> SearchAsync(PagedQuery query, DateTime? from = null,
        DateTime? to = null, CancellationToken ct = default)
    {
        var page = await _uow.Entries.SearchAsync(query, from, to, ct);
        return new PagedResult<EntryListDto>
        {
            Items = page.Items.Select(e => new EntryListDto(
                e.Id, e.Number, e.Type, e.Warehouse.Name, e.Supplier?.TradeName ?? e.Supplier?.CompanyName,
                e.DocumentNumber, e.EntryDate, e.TotalValue, e.Items.Count)).ToList(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    public async Task<Result<int>> CreateAsync(EntryCreateDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result.Failure<int>(validation.Errors.Select(e => e.ErrorMessage));

        var userId = _session.UserId ?? 0;
        var entryId = 0;

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var entry = new MaterialEntry
            {
                Number = await _uow.Sequences.NextNumberAsync("entry", "ENT", ct),
                Type = dto.Type,
                WarehouseId = dto.WarehouseId,
                SupplierId = dto.SupplierId,
                DocumentNumber = dto.DocumentNumber,
                UserId = userId,
                EntryDate = dto.EntryDate.ToUniversalTime(),
                Notes = dto.Notes
            };

            foreach (var itemDto in dto.Items)
            {
                entry.Items.Add(new MaterialEntryItem
                {
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    UnitCost = itemDto.UnitCost,
                    LotNumber = itemDto.LotNumber,
                    ExpirationDate = itemDto.ExpirationDate,
                    LocationId = itemDto.LocationId,
                    Notes = itemDto.Notes
                });
            }

            entry.Validate();
            await _uow.Entries.AddAsync(entry, ct);
            await _uow.SaveChangesAsync(ct);
            entryId = entry.Id;

            foreach (var item in entry.Items)
            {
                var product = await _uow.Products.GetByIdAsync(item.ProductId, ct)
                    ?? throw new InvalidOperationException($"Produto {item.ProductId} não encontrado.");

                if (product.TracksLot && string.IsNullOrWhiteSpace(item.LotNumber))
                    throw new Domain.Exceptions.DomainException($"O produto '{product.Name}' exige informar o lote.");
                if (product.TracksExpiration && item.ExpirationDate is null)
                    throw new Domain.Exceptions.DomainException($"O produto '{product.Name}' exige informar a validade.");

                int? lotId = null;
                if (!string.IsNullOrWhiteSpace(item.LotNumber))
                {
                    var lot = await _stock.GetOrCreateLotAsync(item.ProductId, item.LotNumber, item.ExpirationDate, ct);
                    lotId = lot.Id;
                }

                var currentStock = await _uow.Stock.GetTotalQuantityAsync(item.ProductId, null, ct);
                product.UpdateAverageCost(currentStock, item.Quantity, item.UnitCost);

                await _stock.IncreaseAsync(item.ProductId, dto.WarehouseId, lotId,
                    item.LocationId ?? product.DefaultLocationId,
                    item.Quantity, item.UnitCost, StockMovementType.Entrada,
                    "Entrada", entry.Id, entry.Number, userId, ct);

                await _uow.SaveChangesAsync(ct);
            }
        }, ct);

        return Result.Success(entryId);
    }
}
