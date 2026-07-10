using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Security;
using ALMOXPRO.Shared.Results;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace ALMOXPRO.Application.Services;

public interface IAuthService
{
    Task<Result<UserSessionDto>> LoginAsync(string login, string password, CancellationToken ct = default);
    Task<Result> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<Result> RequestPasswordResetAsync(string loginOrEmail, CancellationToken ct = default);
    Task<Result> ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default);
    Task LogoffAsync(int userId, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUnitOfWork uow, IPasswordHasher hasher, IAuditService audit,
        IEmailService email, ILogger<AuthService> logger)
    {
        _uow = uow;
        _hasher = hasher;
        _audit = audit;
        _email = email;
        _logger = logger;
    }

    public async Task<Result<UserSessionDto>> LoginAsync(string login, string password, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByLoginAsync(login.Trim(), ct);
        if (user is null)
            return Result.Failure<UserSessionDto>("Usuário ou senha inválidos.");

        if (user.Status != UserStatus.Ativo)
            return Result.Failure<UserSessionDto>("Usuário inativo ou bloqueado. Contate o administrador.");

        if (user.IsLockedOut)
            return Result.Failure<UserSessionDto>("Usuário temporariamente bloqueado por tentativas inválidas. Tente novamente mais tarde.");

        if (!_hasher.Verify(password, user.PasswordHash))
        {
            user.RegisterFailedLogin(MaxFailedAttempts, LockDuration);
            await _uow.SaveChangesAsync(ct);
            _logger.LogWarning("Tentativa de login inválida para {Login}", login);
            return Result.Failure<UserSessionDto>("Usuário ou senha inválidos.");
        }

        user.RegisterSuccessfulLogin();
        await _uow.SaveChangesAsync(ct);

        var permissions = await GetEffectivePermissionsAsync(user.Id, ct);
        await _audit.LogActionAsync("Login", "Login efetuado", "users", user.Id.ToString(), ct);

        return Result.Success(new UserSessionDto(
            user.Id, user.Name, user.Login, user.Email, permissions, user.MustChangePassword));
    }

    public async Task<Result> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure("Usuário não encontrado.");

        if (!_hasher.Verify(currentPassword, user.PasswordHash))
            return Result.Failure("A senha atual está incorreta.");

        if (newPassword.Length < 6)
            return Result.Failure("A nova senha deve ter ao menos 6 caracteres.");

        user.PasswordHash = _hasher.Hash(newPassword);
        user.MustChangePassword = false;
        await _uow.SaveChangesAsync(ct);
        await _audit.LogActionAsync("AlterarSenha", "Senha alterada pelo usuário", "users", user.Id.ToString(), ct);
        return Result.Success();
    }

    public async Task<Result> RequestPasswordResetAsync(string loginOrEmail, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByLoginAsync(loginOrEmail.Trim(), ct)
                   ?? await _uow.Users.GetByEmailAsync(loginOrEmail.Trim(), ct);

        // Não revela se o usuário existe.
        if (user is null)
            return Result.Success();

        user.PasswordResetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        user.PasswordResetExpiresAt = DateTime.UtcNow.AddHours(2);
        await _uow.SaveChangesAsync(ct);

        await _email.SendAsync(user.Email, "ALMOX PRO - Recuperação de senha",
            $"Olá {user.Name},\n\nUtilize o código abaixo para redefinir sua senha (válido por 2 horas):\n\n{user.PasswordResetToken}", ct);

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByResetTokenAsync(token.Trim(), ct);
        if (user is null || user.PasswordResetExpiresAt is null || user.PasswordResetExpiresAt < DateTime.UtcNow)
            return Result.Failure("Código de recuperação inválido ou expirado.");

        if (newPassword.Length < 6)
            return Result.Failure("A nova senha deve ter ao menos 6 caracteres.");

        user.PasswordHash = _hasher.Hash(newPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpiresAt = null;
        user.MustChangePassword = false;
        user.LockedUntil = null;
        user.FailedLoginAttempts = 0;
        await _uow.SaveChangesAsync(ct);
        await _audit.LogActionAsync("RecuperarSenha", "Senha redefinida por recuperação", "users", user.Id.ToString(), ct);
        return Result.Success();
    }

    public async Task LogoffAsync(int userId, CancellationToken ct = default)
    {
        await _audit.LogActionAsync("Logoff", "Sessão encerrada", "users", userId.ToString(), ct);
    }

    private async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(int userId, CancellationToken ct)
    {
        var user = await _uow.Users.GetWithAccessAsync(userId, ct)
            ?? throw new InvalidOperationException("Usuário não encontrado ao montar permissões.");

        var permissions = new HashSet<string>(
            user.Roles.SelectMany(r => r.Role.Permissions).Select(p => p.Permission.Code));

        foreach (var overridePermission in user.Permissions)
        {
            if (overridePermission.IsGranted)
                permissions.Add(overridePermission.Permission.Code);
            else
                permissions.Remove(overridePermission.Permission.Code);
        }

        return permissions;
    }
}
