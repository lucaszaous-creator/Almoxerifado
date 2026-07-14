using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Domain.Entities.Configuration;

/// <summary>Parâmetro de configuração do sistema (chave/valor).</summary>
public class AppSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Description { get; set; }
}

/// <summary>Chaves de configuração conhecidas.</summary>
public static class SettingKeys
{
    public const string CompanyName = "company.name";
    public const string CompanyCnpj = "company.cnpj";
    public const string CompanyLogoPath = "company.logo_path";
    public const string Theme = "ui.theme";
    public const string BackupDirectory = "backup.directory";
    public const string BackupAutoEnabled = "backup.auto_enabled";
    public const string BackupScheduleTime = "backup.schedule_time";
    public const string BackupCompress = "backup.compress";
    public const string PgDumpPath = "backup.pg_dump_path";
    public const string PgRestorePath = "backup.pg_restore_path";
    public const string BackupLastAutoAt = "backup.last_auto_at";
    public const string SessionTimeoutMinutes = "security.session_timeout_minutes";
    public const string FiscalCnpj = "fiscal.cnpj";
    public const string FiscalUf = "fiscal.uf";
    public const string FiscalProduction = "fiscal.production";
    public const string FiscalUltNsu = "fiscal.ult_nsu";
    public const string FiscalCertificatePfx = "fiscal.cert_pfx";
    public const string FiscalCertificatePassword = "fiscal.cert_password";
    /// <summary>Origem do certificado: "pfx" (arquivo enviado) ou "store" (repositório do Windows).</summary>
    public const string FiscalCertificateSource = "fiscal.cert_source";
    public const string FiscalCertificateThumbprint = "fiscal.cert_thumbprint";
    public const string FiscalDemoMode = "fiscal.demo_mode";
    /// <summary>"true" = sincroniza a Distribuição DF-e automaticamente a cada ~65 min com o app aberto.</summary>
    public const string FiscalAutoSync = "fiscal.auto_sync";
    /// <summary>Próximo horário (UTC, ISO 8601) em que a consulta DF-e é permitida — evita o cStat 656 "Consumo Indevido".</summary>
    public const string FiscalSyncBlockedUntil = "fiscal.sync_blocked_until";
    // Dados do emitente para a emissão de NF-e própria.
    public const string FiscalEmitName = "fiscal.emit_name";
    public const string FiscalEmitIe = "fiscal.emit_ie";
    /// <summary>Código de Regime Tributário: "1" Simples Nacional, "3" Regime Normal.</summary>
    public const string FiscalEmitCrt = "fiscal.emit_crt";
    public const string FiscalEmitStreet = "fiscal.emit_street";
    public const string FiscalEmitNumber = "fiscal.emit_number";
    public const string FiscalEmitDistrict = "fiscal.emit_district";
    /// <summary>Código IBGE do município do emitente (7 dígitos).</summary>
    public const string FiscalEmitCityCode = "fiscal.emit_city_code";
    public const string FiscalEmitCityName = "fiscal.emit_city_name";
    public const string FiscalEmitCep = "fiscal.emit_cep";
    public const string FiscalEmitPhone = "fiscal.emit_phone";
    /// <summary>Série usada nas NF-e emitidas (padrão 1).</summary>
    public const string FiscalEmitSeries = "fiscal.emit_series";
    /// <summary>Alíquota de PIS (%) das vendas tributadas. Padrão 0,65 (cumulativo/Lucro Presumido).</summary>
    public const string FiscalPisRate = "fiscal.pis_rate";
    /// <summary>Alíquota de COFINS (%) das vendas tributadas. Padrão 3,00 (cumulativo/Lucro Presumido).</summary>
    public const string FiscalCofinsRate = "fiscal.cofins_rate";
    public const string LabelPrinter = "printers.label";
    public const string ReportPrinter = "printers.report";
    public const string ReportsDirectory = "directories.reports";
    public const string ImagesDirectory = "directories.images";
}

/// <summary>Sequência de numeração automática de documentos.</summary>
public class DocumentSequence : BaseEntity
{
    public string Key { get; set; } = string.Empty;      // ex.: "entry", "exit", "transfer", "inventory"
    public string Prefix { get; set; } = string.Empty;   // ex.: "ENT"
    public long NextNumber { get; set; } = 1;

    public string TakeNext()
    {
        var formatted = $"{Prefix}{NextNumber:D6}";
        NextNumber++;
        return formatted;
    }
}
