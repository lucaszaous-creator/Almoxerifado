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

    /// <summary>Restringe o login a dias úteis (segunda a sexta).</summary>
    public bool WeekdaysOnly { get; set; }

    /// <summary>Janela de horário permitida para login (opcional).</summary>
    public TimeOnly? AccessStartTime { get; set; }
    public TimeOnly? AccessEndTime { get; set; }

    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
    public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();

    public bool IsLockedOut => LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;

    /// <summary>Verifica se o login é permitido no momento local informado.</summary>
    public bool CanLoginAt(DateTime localNow)
    {
        if (WeekdaysOnly && localNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;

        if (AccessStartTime.HasValue && AccessEndTime.HasValue)
        {
            var time = TimeOnly.FromDateTime(localNow);
            return time >= AccessStartTime.Value && time <= AccessEndTime.Value;
        }

        return true;
    }

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
