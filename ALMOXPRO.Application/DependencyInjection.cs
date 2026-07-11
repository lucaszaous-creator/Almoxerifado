using ALMOXPRO.Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ALMOXPRO.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IStockOperationService, StockOperationService>();
        services.AddScoped<IStockQueryService, StockQueryService>();
        services.AddScoped<IMaterialEntryService, MaterialEntryService>();
        services.AddScoped<IMaterialExitService, MaterialExitService>();
        services.AddScoped<IStockTransferService, StockTransferService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IRequisitionService, RequisitionService>();
        services.AddScoped<IFiscalService, FiscalService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ISettingsService, SettingsService>();

        return services;
    }
}
