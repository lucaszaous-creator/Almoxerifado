using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Movements;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Results;
using FluentValidation;

namespace ALMOXPRO.Application.Services;

public interface IStockTransferService
{
    Task<PagedResult<TransferListDto>> SearchAsync(PagedQuery query, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(TransferCreateDto dto, CancellationToken ct = default);
}

public class StockTransferService : IStockTransferService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockOperationService _stock;
    private readonly ICurrentSession _session;
    private readonly IValidator<TransferCreateDto> _validator;

    public StockTransferService(IUnitOfWork uow, IStockOperationService stock,
        ICurrentSession session, IValidator<TransferCreateDto> validator)
    {
        _uow = uow;
        _stock = stock;
        _session = session;
        _validator = validator;
    }

    public async Task<PagedResult<TransferListDto>> SearchAsync(PagedQuery query, CancellationToken ct = default)
    {
        var page = await _uow.Transfers.SearchAsync(query, ct);
        return new PagedResult<TransferListDto>
        {
            Items = page.Items.Select(t => new TransferListDto(
                t.Id, t.Number, t.SourceWarehouse.Name, t.DestinationWarehouse.Name,
                t.TransferDate, t.Items.Count)).ToList(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    public async Task<Result<int>> CreateAsync(TransferCreateDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result.Failure<int>(validation.Errors.Select(e => e.ErrorMessage));

        foreach (var item in dto.Items)
        {
            var available = await _uow.Stock.GetTotalQuantityAsync(item.ProductId, dto.SourceWarehouseId, ct);
            if (item.Quantity > available)
            {
                var product = await _uow.Products.GetByIdAsync(item.ProductId, ct);
                return Result.Failure<int>(
                    $"Estoque insuficiente para '{product?.Name}' na origem. Disponível: {available:N2}.");
            }
        }

        var userId = _session.UserId ?? 0;
        var transferId = 0;

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var transfer = new StockTransfer
            {
                Number = await _uow.Sequences.NextNumberAsync("transfer", "TRF", ct),
                SourceWarehouseId = dto.SourceWarehouseId,
                DestinationWarehouseId = dto.DestinationWarehouseId,
                UserId = userId,
                TransferDate = dto.TransferDate.ToUniversalTime(),
                Notes = dto.Notes
            };

            foreach (var itemDto in dto.Items)
            {
                transfer.Items.Add(new StockTransferItem
                {
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    LotId = itemDto.LotId
                });
            }

            transfer.Validate();
            await _uow.Transfers.AddAsync(transfer, ct);
            await _uow.SaveChangesAsync(ct);
            transferId = transfer.Id;

            foreach (var item in transfer.Items)
            {
                var product = await _uow.Products.GetByIdAsync(item.ProductId, ct)
                    ?? throw new InvalidOperationException($"Produto {item.ProductId} não encontrado.");

                await _stock.DecreaseAsync(item.ProductId, dto.SourceWarehouseId, item.LotId,
                    item.Quantity, product.AverageCost, StockMovementType.TransferenciaSaida,
                    "Transferencia", transfer.Id, transfer.Number, userId, ct);
                await _uow.SaveChangesAsync(ct);

                await _stock.IncreaseAsync(item.ProductId, dto.DestinationWarehouseId, item.LotId, null,
                    item.Quantity, product.AverageCost, StockMovementType.TransferenciaEntrada,
                    "Transferencia", transfer.Id, transfer.Number, userId, ct);
                await _uow.SaveChangesAsync(ct);
            }
        }, ct);

        return Result.Success(transferId);
    }
}
