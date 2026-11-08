using ALMOXPRO.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace ALMOXPRO.Infrastructure.Updates;

/// <summary>
/// Verifica atualizações no GitHub Releases do repositório configurado e
/// baixa o instalador (ALMOXPRO-Setup-*.exe) publicado pelo workflow de release.
/// </summary>
public class GitHubUpdateService : IUpdateService, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _owner;
    private readonly string _repository;
    private readonly ILogger<GitHubUpdateService> _logger;

    public GitHubUpdateService(string owner, string repository, ILogger<GitHubUpdateService> logger)
    {
        _owner = owner;
        _repository = repository;
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ALMOXPRO", CurrentVersion.ToString()));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repository}/releases/latest";
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Verificação de atualização: releases indisponíveis ({Status}).",
                    response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = json.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest))
                return null;

            var current = CurrentVersion;
            // Assembly version tem 4 partes (1.0.0.0); compara só Major.Minor.Build.
            var normalizedCurrent = new Version(current.Major, current.Minor, Math.Max(current.Build, 0));
            var normalizedLatest = new Version(latest.Major, latest.Minor, Math.Max(latest.Build, 0));
            if (normalizedLatest <= normalizedCurrent)
                return null;

            if (!root.TryGetProperty("assets", out var assets))
                return null;

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? string.Empty;
                if (name.StartsWith("ALMOXPRO-Setup", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    var downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    if (string.IsNullOrEmpty(downloadUrl))
                        continue;

                    var notes = root.TryGetProperty("body", out var body) ? body.GetString() : null;
                    return new UpdateInfo(normalizedLatest, tag, name, downloadUrl, notes);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            // Sem rede ou API fora do ar: a verificação é silenciosa, nunca derruba o app.
            _logger.LogWarning(ex, "Falha ao verificar atualizações.");
            return null;
        }
    }

    public async Task<string> DownloadInstallerAsync(UpdateInfo update, CancellationToken ct = default)
    {
        var targetDirectory = Path.Combine(Path.GetTempPath(), "ALMOXPRO", "Updates");
        Directory.CreateDirectory(targetDirectory);
        var targetPath = Path.Combine(targetDirectory, update.InstallerName);

        using var response = await _http.GetAsync(update.InstallerUrl,
            HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(targetPath);
        await source.CopyToAsync(target, ct);

        return targetPath;
    }

    public void Dispose() => _http.Dispose();
}
