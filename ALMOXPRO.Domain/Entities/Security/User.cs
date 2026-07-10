using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Domain.Entities.Security;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Ativo;
    public byte[]? Photo { get; set; }
    public DateTime? LastAccessAt { get; set; }

    public bool MustChangePassword { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiresAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }

    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
    public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();

    public bool IsLockedOut => LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;

    public void RegisterFailedLogin(int maxAttempts, TimeSpan lockDuration)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxAttempts)
        {
            LockedUntil = DateTime.UtcNow.Add(lockDuration);
            FailedLoginAttempts = 0;
        }
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        LockedUntil = null;
        LastAccessAt = DateTime.UtcNow;
    }
}
