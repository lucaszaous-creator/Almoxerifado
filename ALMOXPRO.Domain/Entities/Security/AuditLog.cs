namespace ALMOXPRO.Domain.Entities.Security;

/// <summary>
/// Registro imutável de auditoria. Logs nunca são alterados ou excluídos.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Computer { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>Tela/contexto em que a alteração ocorreu.</summary>
    public string Screen { get; set; } = string.Empty;

    /// <summary>Inserção, alteração, exclusão, login, etc.</summary>
    public string Action { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;

    /// <summary>Valores anteriores em JSON.</summary>
    public string? OldValues { get; set; }

    /// <summary>Valores novos em JSON.</summary>
    public string? NewValues { get; set; }
}
