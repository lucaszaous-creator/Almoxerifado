using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Application.DTOs;

public record UserSessionDto(
    int UserId,
    string Name,
    string Login,
    string Email,
    IReadOnlySet<string> Permissions,
    bool MustChangePassword);

public record UserListDto(
    int Id,
    string Name,
    string Login,
    string Email,
    string? Department,
    string? JobTitle,
    UserStatus Status,
    DateTime CreatedAt,
    DateTime? LastAccessAt);

public class UserUpsertDto
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    /// <summary>Obrigatória apenas na criação; em edição, vazia mantém a atual.</summary>
    public string? Password { get; set; }
    public string? Phone { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Ativo;
    public byte[]? Photo { get; set; }
    public List<int> RoleIds { get; set; } = [];
    /// <summary>Permissões individuais: código → concedida/revogada.</summary>
    public Dictionary<string, bool> PermissionOverrides { get; set; } = [];
}

public record RoleDto(int Id, string Name, string? Description, bool IsSystem, List<string> PermissionCodes);

public class RoleUpsertDto
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> PermissionCodes { get; set; } = [];
}

public record AuditLogDto(
    long Id,
    string UserName,
    string Computer,
    string IpAddress,
    DateTime OccurredAt,
    string Screen,
    string Action,
    string TableName,
    string RecordId,
    string? OldValues,
    string? NewValues);
