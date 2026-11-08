using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Domain.Entities.Movements;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Results;
using FluentValidation;

namespace ALMOXPRO.Application.Services;

public interface IRequisitionService
{
    Task<PagedResult<RequisitionListDto>> SearchAsync(PagedQuery query, RequisitionStatus? status = null, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(RequisitionCreateDto dto, CancellationToken ct = default);
    Task<List<RequisitionItemViewDto>> GetItemsAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Atende a requisição gerando a saída de estoque vinculada (lastro).
    /// Quando <paramref name="quantities"/> é informado (id do item → quantidade a entregar),
    /// permite entrega parcial: o saldo restante continua pendente.
    /// </summary>
    Task<Result<int>> FulfillAsync(int id, IReadOnlyDictionary<int, decimal>? quantities = null,
        CancellationToken ct = default);

    Task<Result> CancelAsync(int id, CancellationToken ct = default);
    Task<RequisitionDocument?> GetPrintDocumentAsync(int id, CancellationToken ct = default);
}

public class RequisitionService : IRequisitionService
{
    private readonly IUnitOfWork _uow;
    private readonly IMaterialExitService _exits;
    private readonly ICurrentSession _session;
    private readonly IValidator<RequisitionCreateDto> _validator;

    public RequisitionService(IUnitOfWork uow, IMaterialExitService exits,
        ICurrentSession session, IValidator<RequisitionCreateDto> validator)
    {
        _uow = uow;
        _exits = exits;
        _session = session;
        _validator = validator;
    }

    public async Task<PagedResult<RequisitionListDto>> SearchAsync(PagedQuery query,
        RequisitionStatus? status = null, CancellationToken ct = default)
    {
        var page = await _uow.Requisitions.SearchAsync(query, status, null, ct);
        var exitIds = page.Items.Where(r => r.MaterialExitId.HasValue)
            .Select(r => r.MaterialExitId!.Value).Distinct().ToList();
        var exitNumbers = new Dictionary<int, string>();
        foreach (var exitId in exitIds)
        {
            var exit = await _uow.Exits.GetByIdAsync(exitId, ct);
            if (exit is not null)
                exitNumbers[exitId] = exit.Number;
        }

        return new PagedResult<RequisitionListDto>
        {
            Items = page.Items.Select(r => new RequisitionListDto(
                r.Id, r.Number, r.Status, r.Warehouse.Name, r.Sector.Name,
                r.Employee?.Name, r.RequesterName, r.RequestDate, r.FulfilledAt,
                r.MaterialExitId.HasValue ? exitNumbers.GetValueOrDefault(r.MaterialExitId.Value) : null,
                r.Items.Count)).ToList(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    public async Task<Result<int>> CreateAsync(RequisitionCreateDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result.Failure<int>(validation.Errors.Select(e => e.ErrorMessage));

        // Reserva: o disponível desconta o que outras requisições abertas já prometeram.
        foreach (var item in dto.Items)
        {
            var physical = await _uow.Stock.GetTotalQuantityAsync(item.ProductId, dto.WarehouseId, ct);
            var reserved = await _uow.Requisitions.GetReservedQuantityAsync(item.ProductId, dto.WarehouseId, null, ct);
            var free = physical - reserved;
            if (item.Quantity > free)
            {
                var product = await _uow.Products.GetByIdAsync(item.ProductId, ct);
                return Result.Failure<int>(
                    $"Estoque livre insuficiente para '{product?.Name}'. " +
                    $"Físico: {physical:N2}, reservado por outras requisições: {reserved:N2}, livre: {Math.Max(free, 0):N2}.");
            }
        }

        var requisition = new Requisition
        {
            Number = await _uow.Sequences.NextNumberAsync("requisition", "REQ", ct),
            WarehouseId = dto.WarehouseId,
            SectorId = dto.SectorId,
            EmployeeId = dto.EmployeeId,
            RequesterName = dto.RequesterName,
            CreatedByUserId = _session.UserId ?? 0,
            Notes = dto.Notes
        };

        foreach (var item in dto.Items)
        {
            requisition.Items.Add(new RequisitionItem
            {
                ProductId = item.ProductId,
                QuantityRequested = item.Quantity
            });
        }

        requisition.Validate();
        await _uow.Requisitions.AddAsync(requisition, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(requisition.Id);
    }

    public async Task<List<RequisitionItemViewDto>> GetItemsAsync(int id, CancellationToken ct = default)
    {
        var requisition = await _uow.Requisitions.GetWithItemsAsync(id, ct);
        if (requisition is null)
            return [];

        return requisition.Items.Select(i => new RequisitionItemViewDto(
            i.Id, i.ProductId, i.Product.InternalCode, i.Product.Name, i.Product.Unit,
            i.QuantityRequested, i.QuantityFulfilled)).ToList();
    }

    public async Task<Result<int>> FulfillAsync(int id, IReadOnlyDictionary<int, decimal>? quantities = null,
        CancellationToken ct = default)
    {
        var requisition = await _uow.Requisitions.GetWithItemsAsync(id, ct);
        if (requisition is null)
            return Result.Failure<int>("Requisição não encontrada.");
        if (!requisition.IsOpen)
            return Result.Failure<int>("Apenas requisições pendentes ou parciais podem ser atendidas.");

        // Monta as entregas: por padrão, tudo o que ainda falta de cada item.
        var deliveries = new List<(RequisitionItem Item, decimal Quantity)>();
        foreach (var item in requisition.Items)
        {
            var remaining = item.QuantityRequested - item.QuantityFulfilled;
            var deliver = quantities is null
                ? remaining
                : quantities.GetValueOrDefault(item.Id, 0);

            if (deliver < 0)
                return Result.Failure<int>("A quantidade a entregar não pode ser negativa.");
            if (deliver > remaining)
                return Result.Failure<int>(
                    $"'{item.Product.Name}': a entrega ({deliver:N2}) excede o saldo pendente ({remaining:N2}).");
            if (deliver > 0)
                deliveries.Add((item, deliver));
        }

        if (deliveries.Count == 0)
            return Result.Failure<int>("Informe ao menos um item com quantidade a entregar.");

        var exitDto = new ExitCreateDto
        {
            Type = ExitType.Consumo,
            WarehouseId = requisition.WarehouseId,
            SectorId = requisition.SectorId,
            EmployeeId = requisition.EmployeeId,
            Reason = $"Requisição {requisition.Number}",
            ResponsibleName = requisition.Employee?.Name ?? requisition.RequesterName,
            Notes = requisition.Notes,
            Items = deliveries.Select(d => new ExitItemDto
            {
                ProductId = d.Item.ProductId,
                Quantity = d.Quantity
            }).ToList()
        };

        var exitId = 0;
        Result<int>? exitResult = null;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            exitResult = await _exits.CreateAsync(exitDto, ct);
            if (exitResult.IsFailure)
                return;

            exitId = exitResult.Value;
            foreach (var (item, quantity) in deliveries)
                item.QuantityFulfilled += quantity;
            requisition.RegisterFulfillment(_session.UserId ?? 0, exitId);
            await _uow.SaveChangesAsync(ct);
        }, ct);

        if (exitResult is { IsFailure: true })
            return Result.Failure<int>(exitResult.Errors);

        return Result.Success(exitId);
    }

    public async Task<Result> CancelAsync(int id, CancellationToken ct = default)
    {
        var requisition = await _uow.Requisitions.GetWithItemsAsync(id, ct);
        if (requisition is null)
            return Result.Failure("Requisição não encontrada.");
        if (!requisition.IsOpen)
            return Result.Failure("Apenas requisições pendentes ou parciais podem ser canceladas.");

        requisition.Cancel();
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<RequisitionDocument?> GetPrintDocumentAsync(int id, CancellationToken ct = default)
    {
        var requisition = await _uow.Requisitions.GetWithItemsAsync(id, ct);
        if (requisition is null)
            return null;

        var company = await _uow.Settings.GetByKeyAsync(SettingKeys.CompanyName, ct);
        var logo = await _uow.Settings.GetByKeyAsync(SettingKeys.CompanyLogoPath, ct);
        var creator = await _uow.Users.GetByIdAsync(requisition.CreatedByUserId, ct);

        return new RequisitionDocument(
            logo?.Value,
            string.IsNullOrWhiteSpace(company?.Value) ? "ALMOX PRO" : company!.Value!,
            requisition.Number,
            requisition.Status.ToString(),
            requisition.Warehouse.Name,
            requisition.Sector.Name,
            requisition.Employee?.Name ?? requisition.RequesterName ?? "-",
            creator?.Name ?? "-",
            requisition.RequestDate.ToLocalTime(),
            requisition.Notes,
            requisition.Items.Select(i => new RequisitionDocumentItem(
                i.Product.InternalCode, i.Product.Name, i.Product.Unit, i.QuantityRequested)).ToList());
    }
}
