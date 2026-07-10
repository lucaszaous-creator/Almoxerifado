using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Entities.Security;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Results;
using FluentValidation;

namespace ALMOXPRO.Application.Services;

public interface IUserService
{
    Task<PagedResult<UserListDto>> SearchAsync(PagedQuery query, CancellationToken ct = default);
    Task<UserUpsertDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(UserUpsertDto dto, CancellationToken ct = default);
    Task<Result> DeactivateAsync(int id, CancellationToken ct = default);
}

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IValidator<UserUpsertDto> _validator;

    public UserService(IUnitOfWork uow, IPasswordHasher hasher, IValidator<UserUpsertDto> validator)
    {
        _uow = uow;
        _hasher = hasher;
        _validator = validator;
    }

    public async Task<PagedResult<UserListDto>> SearchAsync(PagedQuery query, CancellationToken ct = default)
    {
        var page = await _uow.Users.SearchAsync(query, ct);
        return new PagedResult<UserListDto>
        {
            Items = page.Items.Select(u => new UserListDto(
                u.Id, u.Name, u.Login, u.Email, u.Department, u.JobTitle,
                u.Status, u.CreatedAt, u.LastAccessAt)).ToList(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    public async Task<UserUpsertDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetWithAccessAsync(id, ct);
        if (user is null)
            return null;

        return new UserUpsertDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Login = user.Login,
            Phone = user.Phone,
            Department = user.Department,
            JobTitle = user.JobTitle,
            Status = user.Status,
            Photo = user.Photo,
            RoleIds = user.Roles.Select(r => r.RoleId).ToList(),
            PermissionOverrides = user.Permissions.ToDictionary(p => p.Permission.Code, p => p.IsGranted)
        };
    }

    public async Task<Result<int>> SaveAsync(UserUpsertDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result.Failure<int>(validation.Errors.Select(e => e.ErrorMessage));

        var loginInUse = await _uow.Users.AnyAsync(
            u => u.Login == dto.Login && (dto.Id == null || u.Id != dto.Id), ct);
        if (loginInUse)
            return Result.Failure<int>("Já existe um usuário com este login.");

        var emailInUse = await _uow.Users.AnyAsync(
            u => u.Email == dto.Email && (dto.Id == null || u.Id != dto.Id), ct);
        if (emailInUse)
            return Result.Failure<int>("Já existe um usuário com este e-mail.");

        User user;
        if (dto.Id is null)
        {
            user = new User { PasswordHash = _hasher.Hash(dto.Password!), MustChangePassword = true };
            await _uow.Users.AddAsync(user, ct);
        }
        else
        {
            user = await _uow.Users.GetWithAccessAsync(dto.Id.Value, ct)
                ?? throw new InvalidOperationException("Usuário não encontrado.");
            if (!string.IsNullOrEmpty(dto.Password))
                user.PasswordHash = _hasher.Hash(dto.Password);
        }

        user.Name = dto.Name;
        user.Email = dto.Email;
        user.Login = dto.Login;
        user.Phone = dto.Phone;
        user.Department = dto.Department;
        user.JobTitle = dto.JobTitle;
        user.Status = dto.Status;
        if (dto.Photo is not null)
            user.Photo = dto.Photo;

        user.Roles.Clear();
        foreach (var roleId in dto.RoleIds.Distinct())
            user.Roles.Add(new UserRole { RoleId = roleId });

        user.Permissions.Clear();
        foreach (var (code, granted) in dto.PermissionOverrides)
        {
            var permission = await _uow.Permissions.GetByCodeAsync(code, ct);
            if (permission is not null)
                user.Permissions.Add(new UserPermission { PermissionId = permission.Id, IsGranted = granted });
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Success(user.Id);
    }

    public async Task<Result> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(id, ct);
        if (user is null)
            return Result.Failure("Usuário não encontrado.");

        user.Status = Domain.Common.UserStatus.Inativo;
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
