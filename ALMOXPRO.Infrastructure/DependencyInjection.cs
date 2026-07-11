using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Infrastructure.Backup;
using ALMOXPRO.Infrastructure.Codes;
using ALMOXPRO.Infrastructure.Email;
using ALMOXPRO.Infrastructure.Fiscal;
using ALMOXPRO.Infrastructure.Machine;
using ALMOXPRO.Infrastructure.Reports;
using ALMOXPRO.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ALMOXPRO.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IMachineInfoProvider, MachineInfoProvider>();
        services.AddSingleton<IBarcodeGenerator, BarcodeGenerator>();
        services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
        services.AddSingleton<IReportExporter, ReportExporter>();
        services.AddSingleton<ILabelGenerator, LabelGenerator>();
        services.AddSingleton<IRequisitionDocumentGenerator, RequisitionDocumentGenerator>();
        services.AddSingleton<IFiscalGateway, UnimakeFiscalGateway>();
        services.AddSingleton<IDanfeGenerator, DanfeGenerator>();
        services.AddSingleton<IEmailService, SmtpEmailService>();
        services.AddScoped<IBackupService>(provider => new PostgresBackupService(
            connectionString,
            provider.GetRequiredService<ISettingsService>(),
            provider.GetRequiredService<ILogger<PostgresBackupService>>()));

        return services;
    }
}
