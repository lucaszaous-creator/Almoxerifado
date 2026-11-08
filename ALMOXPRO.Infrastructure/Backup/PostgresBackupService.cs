using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Entities.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Compression;

namespace ALMOXPRO.Infrastructure.Backup;

/// <summary>
/// Backup e restauração do PostgreSQL via pg_dump/pg_restore,
/// com compactação opcional em .zip.
/// </summary>
public class PostgresBackupService : IBackupService
{
    private readonly string _connectionString;
    private readonly ISettingsService _settings;
    private readonly ILogger<PostgresBackupService> _logger;

    public PostgresBackupService(string connectionString, ISettingsService settings, ILogger<PostgresBackupService> logger)
    {
        _connectionString = connectionString;
        _settings = settings;
        _logger = logger;
    }

    public async Task<string> BackupAsync(string? targetDirectory = null, bool compress = true, CancellationToken ct = default)
    {
        var directory = targetDirectory
            ?? await _settings.GetAsync(SettingKeys.BackupDirectory, ct)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ALMOXPRO", "Backups");
        Directory.CreateDirectory(directory);

        var pgDump = await _settings.GetAsync(SettingKeys.PgDumpPath, ct) ?? "pg_dump";
        var parameters = ParseConnectionString();
        var fileName = $"almoxpro_{DateTime.Now:yyyyMMdd_HHmmss}.backup";
        var filePath = Path.Combine(directory, fileName);

        var arguments = $"-h {parameters.Host} -p {parameters.Port} -U {parameters.User} -F c -f \"{filePath}\" {parameters.Database}";
        await RunToolAsync(pgDump, arguments, parameters.Password, ct);

        if (!compress)
        {
            _logger.LogInformation("Backup gerado em {Path}", filePath);
            return filePath;
        }

        var zipPath = Path.ChangeExtension(filePath, ".zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(filePath, fileName, CompressionLevel.Optimal);
        }
        File.Delete(filePath);
        _logger.LogInformation("Backup compactado gerado em {Path}", zipPath);
        return zipPath;
    }

    public async Task RestoreAsync(string backupFilePath, CancellationToken ct = default)
    {
        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException("Arquivo de backup não encontrado.", backupFilePath);

        var workingFile = backupFilePath;
        string? extractedFile = null;

        if (Path.GetExtension(backupFilePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"almoxpro_restore_{Guid.NewGuid():N}");
            ZipFile.ExtractToDirectory(backupFilePath, tempDirectory);
            extractedFile = Directory.GetFiles(tempDirectory).FirstOrDefault()
                ?? throw new InvalidOperationException("O arquivo .zip não contém um backup válido.");
            workingFile = extractedFile;
        }

        try
        {
            var pgRestore = await _settings.GetAsync(SettingKeys.PgRestorePath, ct) ?? "pg_restore";
            var parameters = ParseConnectionString();
            var arguments = $"-h {parameters.Host} -p {parameters.Port} -U {parameters.User} -d {parameters.Database} --clean --if-exists \"{workingFile}\"";
            await RunToolAsync(pgRestore, arguments, parameters.Password, ct);
            _logger.LogInformation("Backup restaurado a partir de {Path}", backupFilePath);
        }
        finally
        {
            if (extractedFile is not null)
                Directory.Delete(Path.GetDirectoryName(extractedFile)!, recursive: true);
        }
    }

    private async Task RunToolAsync(string tool, string arguments, string password, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = tool,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        startInfo.EnvironmentVariables["PGPASSWORD"] = password;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Não foi possível iniciar '{tool}'. Verifique o caminho nas configurações.");

        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"'{Path.GetFileName(tool)}' falhou (código {process.ExitCode}): {stderr}");
    }

    private (string Host, string Port, string Database, string User, string Password) ParseConnectionString()
    {
        var parts = _connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim().ToLowerInvariant(), p => p[1].Trim());

        return (
            parts.GetValueOrDefault("host", "localhost"),
            parts.GetValueOrDefault("port", "5432"),
            parts.GetValueOrDefault("database", "almoxpro"),
            parts.GetValueOrDefault("username", parts.GetValueOrDefault("user id", "postgres")),
            parts.GetValueOrDefault("password", ""));
    }
}
