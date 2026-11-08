using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Shared.Results;
using ALMOXPRO.Shared.Security;

namespace ALMOXPRO.Application.Services;

/// <summary>
/// Leitura da configuração fiscal gravada em AppSettings (certificado, CNPJ,
/// UF, ambiente), compartilhada pelos serviços de recebimento e de emissão.
/// </summary>
internal static class FiscalConfigLoader
{
    public static async Task<bool> IsDemoModeAsync(IUnitOfWork uow, CancellationToken ct) =>
        (await uow.Settings.GetByKeyAsync(SettingKeys.FiscalDemoMode, ct))?.Value == "true";

    public static async Task<Result<FiscalConfig>> LoadAsync(IUnitOfWork uow, CancellationToken ct)
    {
        var cnpj = (await uow.Settings.GetByKeyAsync(SettingKeys.FiscalCnpj, ct))?.Value;
        var uf = (await uow.Settings.GetByKeyAsync(SettingKeys.FiscalUf, ct))?.Value;
        var production = (await uow.Settings.GetByKeyAsync(SettingKeys.FiscalProduction, ct))?.Value != "false";
        var source = (await uow.Settings.GetByKeyAsync(SettingKeys.FiscalCertificateSource, ct))?.Value ?? "pfx";

        if (string.IsNullOrWhiteSpace(cnpj) || string.IsNullOrWhiteSpace(uf))
            return Result.Failure<FiscalConfig>(
                "Configuração fiscal incompleta. Em Configurações, informe o certificado, o CNPJ e a UF.");

        if (source == "store")
        {
            var thumbprint = (await uow.Settings.GetByKeyAsync(SettingKeys.FiscalCertificateThumbprint, ct))?.Value;
            if (string.IsNullOrWhiteSpace(thumbprint))
                return Result.Failure<FiscalConfig>(
                    "Selecione o certificado instalado no Windows em Configurações.");
            return Result.Success(new FiscalConfig(null, null, thumbprint, cnpj, uf, production));
        }

        var pfx = (await uow.Settings.GetByKeyAsync(SettingKeys.FiscalCertificatePfx, ct))?.Value;
        var password = (await uow.Settings.GetByKeyAsync(SettingKeys.FiscalCertificatePassword, ct))?.Value;
        if (string.IsNullOrWhiteSpace(pfx))
            return Result.Failure<FiscalConfig>(
                "Configuração fiscal incompleta. Em Configurações, envie o certificado A1.");

        return Result.Success(new FiscalConfig(
            Convert.FromBase64String(pfx),
            string.IsNullOrEmpty(password) ? string.Empty : SecretProtector.Unprotect(password),
            null, cnpj, uf, production));
    }
}
