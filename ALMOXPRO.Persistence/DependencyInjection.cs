using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Persistence.Auditing;
using ALMOXPRO.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ALMOXPRO.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<AlmoxProDbContext>((provider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3));
            options.AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
