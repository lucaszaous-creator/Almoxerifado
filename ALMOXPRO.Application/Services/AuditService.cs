using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Entities.Security;
using ALMOXPRO.Shared.Pagination;

namespace ALMOXPRO.Application.Services;

public interface IAuditService
{
    /// <summary>Registra uma ação de negócio (login, exportação, ajuste...). Alterações de dados são registradas automaticamente pela persistência.</summary>
    Task LogActionAsync(string action, string screen, string tableName, string recordId, CancellationToken ct = default);
    Task<PagedResult<AuditLogDto>> SearchAsync(PagedQuery query, DateTime? from, DateTime? to, string? tableName, CancellationToken ct = default);
}

public class AuditService : IAuditService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentSession _session;
    private readonly IMachineInfoProvider _machine;

    public AuditService(IUnitOfWork uow, ICurrentSession session, IMachineInfoProvider machine)
    {
        _uow = uow;
        _session = session;
        _machine = machine;
    }

    public async Task LogActionAsync(string action, string screen, string tableName, string recordId, CancellationToken ct = default)
    {
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _session.UserId,
            UserName = _session.UserName,
            Computer = _machine.ComputerName,
            IpAddress = _machine.IpAddress,
            OccurredAt = DateTime.UtcNow,
            Screen = screen,
            Action = action,
            TableName = tableName,
            RecordId = recordId
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AuditLogDto>> SearchAsync(PagedQuery query, DateTime? from, DateTime? to,
        string? tableName, CancellationToken ct = default)
    {
        var page = await _uow.AuditLogs.SearchAsync(query, from, to, tableName, ct);
        return new PagedResult<AuditLogDto>
        {
            Items = page.Items.Select(l => new AuditLogDto(
                l.Id, l.UserName, l.Computer, l.IpAddress, l.OccurredAt, l.Screen,
                l.Action, l.TableName, l.RecordId, l.OldValues, l.NewValues)).ToList(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }
}
