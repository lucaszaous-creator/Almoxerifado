using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Movements;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Results;
using FluentValidation;

namespace ALMOXPRO.Application.Services;

public interface IMaterialExitService
{
    Task<PagedResult<ExitListDto>> SearchAsync(PagedQuery query, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(ExitCreateDto dto, CancellationToken ct = default);
}

public class MaterialExitService : IMaterialExitService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockOperationService _stock;
    private readonly ICurrentSession _session;
    private readonly IValidator<ExitCreateDto> _validator;

    public MaterialExitService(IUnitOfWork uow, IStockOperationService stock,
        ICurrentSession session, IValidator<ExitCreateDto> validator)
    {
        _uow = uow;
        _stock = stock;
        _session = session;
        _validator = validator;
    }

    public async Task<PagedResult<ExitListDto>> SearchAsync(PagedQuery query, DateTime? from = null,
        DateTime? to = null, CancellationToken ct = default)
    {
        var page = await _uow.Exits.SearchAsync(query, from, to, ct);
        return new PagedResult<ExitListDto>
        {
            Items = page.Items.Select(e => new ExitListDto(
                e.Id, e.Number, e.Type, e.Warehouse.Name, e.CostCenter?.Name,
                e.Employee?.Name, e.Sector?.Name, e.ExitDate, e.TotalValue, e.Items.Count)).ToList(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    public async Task<Result<int>> CreateAsync(ExitCreateDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result.Failure<int>(validation.Errors.Select(e => e.ErrorMessage));

        // Valida disponibilidade antes de abrir a transação para mensagens mais claras.
        foreach (var item in dto.Items)
        {
            var available = await _uow.Stock.GetTotalQuantityAsync(item.ProductId, dto.WarehouseId, ct);
            if (item.Quantity > available)
            {
                var product = await _uow.Products.GetByIdAsync(item.ProductId, ct);
                return Result.Failure<int>(
                    $"Estoque insuficiente para '{product?.Name}'. Disponível: {available:N2}, solicitado: {item.Quantity:N2}.");
            }
        }

        var userId = _session.UserId ?? 0;
        var exitId = 0;

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var exit = new MaterialExit
            {
                Number = await _uow.Sequences.NextNumberAsync("exit", "SAI", ct),
                Type = dto.Type,
                WarehouseId = dto.WarehouseId,
                CostCenterId = dto.CostCenterId,
                EmployeeId = dto.EmployeeId,
                SectorId = dto.SectorId,
                WorkOrder = dto.WorkOrder,
                Reason = dto.Reason,
                ResponsibleName = dto.ResponsibleName,
                Signature = dto.Signature,
                UserId = userId,
                ExitDate = dto.ExitDate.ToUniversalTime(),
                Notes = dto.Notes
            };

            foreach (var itemDto in dto.Items)
            {
                var product = await _uow.Products.GetByIdAsync(itemDto.ProductId, ct)
                    ?? throw new InvalidOperationException($"Produto {itemDto.ProductId} não encontrado.");

                exit.Items.Add(new MaterialExitItem
                {
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    UnitCost = product.AverageCost,
                    LotId = itemDto.LotId,
                    Notes = itemDto.Notes
                });
            }

            exit.Validate();
            await _uow.Exits.AddAsync(exit, ct);
            await _uow.SaveChangesAsync(ct);
            exitId = exit.Id;

            foreach (var item in exit.Items)
            {
                await _stock.DecreaseAsync(item.ProductId, dto.WarehouseId, item.LotId,
                    item.Quantity, item.UnitCost, StockMovementType.Saida,
                    "Saida", exit.Id, exit.Number, userId, ct);
                await _uow.SaveChangesAsync(ct);
            }
        }, ct);

        return Result.Success(exitId);
    }
}
