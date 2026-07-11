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
