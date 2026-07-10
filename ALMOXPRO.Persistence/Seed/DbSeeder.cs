using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Domain.Entities.Security;
using ALMOXPRO.Persistence.Context;
using ALMOXPRO.Shared.Security;
using Microsoft.EntityFrameworkCore;

namespace ALMOXPRO.Persistence.Seed;

/// <summary>
/// Popula dados essenciais: permissões, perfil e usuário administrador,
/// almoxarifado padrão e sequências de documentos.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AlmoxProDbContext context, IPasswordHasher hasher, CancellationToken ct = default)
    {
        // Permissões: garante que todos os códigos conhecidos existam.
        var existingCodes = await context.Permissions.Select(p => p.Code).ToListAsync(ct);
        foreach (var (code, description) in PermissionCodes.All)
        {
            if (!existingCodes.Contains(code))
                context.Permissions.Add(new Permission { Code = code, Description = description });
        }
        await context.SaveChangesAsync(ct);

        // Perfil Administrador com todas as permissões.
        var adminRole = await context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Name == "Administrador", ct);
        if (adminRole is null)
        {
            adminRole = new Role { Name = "Administrador", Description = "Acesso total ao sistema", IsSystem = true };
            context.Roles.Add(adminRole);
        }

        var allPermissions = await context.Permissions.ToListAsync(ct);
        var grantedIds = adminRole.Permissions.Select(p => p.PermissionId).ToHashSet();
        foreach (var permission in allPermissions.Where(p => !grantedIds.Contains(p.Id)))
            adminRole.Permissions.Add(new RolePermission { Permission = permission });
        await context.SaveChangesAsync(ct);

        // Usuário administrador padrão (senha inicial: admin — troca obrigatória no primeiro acesso).
        if (!await context.Users.AnyAsync(u => u.Login == "admin", ct))
        {
            var admin = new User
            {
                Name = "Administrador",
                Email = "admin@almoxpro.local",
                Login = "admin",
                PasswordHash = hasher.Hash("admin"),
                Status = UserStatus.Ativo,
                MustChangePassword = true
            };
            admin.Roles.Add(new UserRole { Role = adminRole });
            context.Users.Add(admin);
            await context.SaveChangesAsync(ct);
        }

        // Almoxarifado padrão.
        if (!await context.Warehouses.AnyAsync(ct))
        {
            context.Warehouses.Add(new Warehouse { Code = "ALM01", Name = "Almoxarifado Central" });
            await context.SaveChangesAsync(ct);
        }

        // Categoria raiz padrão.
        if (!await context.Categories.AnyAsync(ct))
        {
            context.Categories.Add(new Category { Name = "Geral" });
            await context.SaveChangesAsync(ct);
        }

        // Sequências de documentos.
        var sequences = new (string Key, string Prefix)[]
        {
            ("entry", "ENT"), ("exit", "SAI"), ("transfer", "TRF"), ("inventory", "INV")
        };
        foreach (var (key, prefix) in sequences)
        {
            if (!await context.DocumentSequences.AnyAsync(s => s.Key == key, ct))
                context.DocumentSequences.Add(new DocumentSequence { Key = key, Prefix = prefix });
        }
        await context.SaveChangesAsync(ct);

        // Configurações padrão.
        if (!await context.AppSettings.AnyAsync(s => s.Key == SettingKeys.Theme, ct))
        {
            context.AppSettings.Add(new AppSetting { Key = SettingKeys.Theme, Value = "light", Description = "Tema da interface (light/dark)" });
            await context.SaveChangesAsync(ct);
        }
    }
}
