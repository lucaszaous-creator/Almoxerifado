using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Entities.Security;
using ALMOXPRO.Shared.Results;

namespace ALMOXPRO.Application.Services;

public interface IRoleService
{
    Task<List<RoleDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<int>> SaveAsync(RoleUpsertDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public class RoleService : IRoleService
{
    private readonly IUnitOfWork _uow;

    public RoleService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<RoleDto>> GetAllAsync(CancellationToken ct = default)
    {
        var roles = await _uow.Roles.GetAllWithPermissionsAsync(ct);
        return roles.Select(r => new RoleDto(
            r.Id, r.Name, r.Description, r.IsSystem,
            r.Permissions.Select(p => p.Permission.Code).ToList())).ToList();
    }

    public async Task<Result<int>> SaveAsync(RoleUpsertDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result.Failure<int>("O nome do perfil é obrigatório.");

        var nameInUse = await _uow.Roles.AnyAsync(
            r => r.Name == dto.Name && (dto.Id == null || r.Id != dto.Id), ct);
        if (nameInUse)
            return Result.Failure<int>("Já existe um perfil com este nome.");

        Role role;
        if (dto.Id is null)
        {
            role = new Role();
            await _uow.Roles.AddAsync(role, ct);
        }
        else
        {
            role = await _uow.Roles.GetWithPermissionsAsync(dto.Id.Value, ct)
                ?? throw new InvalidOperationException("Perfil não encontrado.");
        }

        role.Name = dto.Name.Trim();
        role.Description = dto.Description;

        role.Permissions.Clear();
        foreach (var code in dto.PermissionCodes.Distinct())
        {
            var permission = await _uow.Permissions.GetByCodeAsync(code, ct);
            if (permission is not null)
                role.Permissions.Add(new RolePermission { PermissionId = permission.Id });
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Success(role.Id);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var role = await _uow.Roles.GetByIdAsync(id, ct);
        if (role is null)
            return Result.Failure("Perfil não encontrado.");
        if (role.IsSystem)
            return Result.Failure("Perfis de sistema não podem ser excluídos.");

        _uow.Roles.Remove(role);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
