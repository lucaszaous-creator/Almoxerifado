using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Domain.Entities.Security;

public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Perfis de sistema (ex.: Administrador) não podem ser excluídos.</summary>
    public bool IsSystem { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> Users { get; set; } = new List<UserRole>();
}

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ICollection<RolePermission> Roles { get; set; } = new List<RolePermission>();
}

public class RolePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

public class UserRole
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

/// <summary>
/// Permissão individual do usuário. Sobrepõe as permissões herdadas dos perfis:
/// IsGranted = true concede; IsGranted = false revoga explicitamente.
/// </summary>
public class UserPermission
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
    public bool IsGranted { get; set; }
}
