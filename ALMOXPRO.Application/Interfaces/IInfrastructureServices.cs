namespace ALMOXPRO.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Sessão do usuário autenticado. Implementada pela camada de apresentação.</summary>
public interface ICurrentSession
{
    int? UserId { get; }
    string UserName { get; }
    bool IsAuthenticated { get; }
    IReadOnlySet<string> Permissions { get; }
    bool HasPermission(string permissionCode);
}

/// <summary>Informações da estação de trabalho para auditoria.</summary>
public interface IMachineInfoProvider
{
    string ComputerName { get; }
    string IpAddress { get; }
}

public interface IBarcodeGenerator
{
    /// <summary>Gera um código de barras Code128 em PNG.</summary>
    byte[] GeneratePng(string content, int width = 300, int height = 100);
}

public interface IQrCodeGenerator
{
    byte[] GeneratePng(string content, int pixelsPerModule = 10);
}

/// <summary>Tabela genérica usada pelos exportadores de relatório.</summary>
public record ReportTable(
    string Title,
    string? Subtitle,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);

public interface IReportExporter
{
    byte[] ToPdf(ReportTable table);
    byte[] ToExcel(ReportTable table);
    byte[] ToCsv(ReportTable table);
}

/// <summary>Impressão de etiquetas com código de barras / QR Code.</summary>
public interface ILabelGenerator
{
    byte[] ProductLabelPdf(string productName, string internalCode, string? barcode, string? qrContent, int copies = 1);
    byte[] LocationLabelPdf(string locationCode, string warehouseName, int copies = 1);
}

public interface IBackupService
{
    Task<string> BackupAsync(string? targetDirectory = null, bool compress = true, CancellationToken ct = default);
    Task RestoreAsync(string backupFilePath, CancellationToken ct = default);
}

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
