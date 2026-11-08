using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Persistence;
using ALMOXPRO.Persistence.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALMOXPRO.Tests.Integration;

/// <summary>
/// Proteção contra o cStat 656 "Consumo Indevido": a SEFAZ bloqueia por 1 hora
/// quem repete a consulta DF-e sem notas novas. Depois de um 137 (sem
/// documentos) ou de um 656, o sistema deve recusar nova sincronização
/// localmente, sem sequer chamar a SEFAZ, até o horário liberado.
/// </summary>
public class FiscalSyncGuardTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AlmoxProDbContext _context;
    private readonly FiscalService _service;
    private readonly CountingGateway _gateway = new();

    public FiscalSyncGuardTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new AlmoxProDbContext(new DbContextOptionsBuilder<AlmoxProDbContext>()
            .UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        // Configuração fiscal mínima para o caminho real (o gateway é fake).
        _context.AppSettings.AddRange(
            new AppSetting { Key = SettingKeys.FiscalCnpj, Value = "11222333000181" },
            new AppSetting { Key = SettingKeys.FiscalUf, Value = "RJ" },
            new AppSetting { Key = SettingKeys.FiscalCertificatePfx, Value = "QUFBQQ==" });
        _context.SaveChanges();

        _service = new FiscalService(new UnitOfWork(_context), _gateway, new NoopDanfe(),
            new FakeSession(), NullLogger<FiscalService>.Instance);
    }

    [Fact]
    public async Task Sync_Apos137_BloqueiaNovaConsultaSemChamarASefaz()
    {
        _gateway.StatusToReturn = 137;

        var first = await _service.SyncAsync();
        Assert.True(first.IsSuccess);
        Assert.Equal(1, _gateway.Calls);

        var second = await _service.SyncAsync();
        Assert.True(second.IsFailure);
        Assert.Contains("656", string.Join(" ", second.Errors));
        // A SEFAZ não foi consultada de novo — o bloqueio é local.
        Assert.Equal(1, _gateway.Calls);
    }

    [Fact]
    public async Task Sync_656Consecutivos_AumentaORecuo_ESucessoZera()
    {
        _gateway.StatusToReturn = 656;
        await _service.SyncAsync();
        var afterFirst = await _context.AppSettings
            .FirstAsync(s => s.Key == SettingKeys.FiscalSyncBackoffLevel);
        Assert.Equal("1", afterFirst.Value);

        // Libera o bloqueio local e leva outro 656: o recuo sobe para o nível 2.
        (await _context.AppSettings.FirstAsync(s => s.Key == SettingKeys.FiscalSyncBlockedUntil))
            .Value = string.Empty;
        await _context.SaveChangesAsync();
        await _service.SyncAsync();
        var afterSecond = await _context.AppSettings
            .FirstAsync(s => s.Key == SettingKeys.FiscalSyncBackoffLevel);
        Assert.Equal("2", afterSecond.Value);

        // Uma consulta bem-sucedida (138) zera a penalidade acumulada.
        (await _context.AppSettings.FirstAsync(s => s.Key == SettingKeys.FiscalSyncBlockedUntil))
            .Value = string.Empty;
        await _context.SaveChangesAsync();
        _gateway.StatusToReturn = 138;
        await _service.SyncAsync();
        var afterSuccess = await _context.AppSettings
            .FirstAsync(s => s.Key == SettingKeys.FiscalSyncBackoffLevel);
        Assert.Equal("0", afterSuccess.Value);
    }

    [Fact]
    public async Task Sync_Com656_RegistraBloqueioEExplica()
    {
        _gateway.StatusToReturn = 656;

        var first = await _service.SyncAsync();
        Assert.True(first.IsFailure);
        Assert.Contains(first.Errors, e => e.Contains("656"));

        var second = await _service.SyncAsync();
        Assert.True(second.IsFailure);
        Assert.Equal(1, _gateway.Calls);
    }

    [Fact]
    public async Task Sync_ComBloqueioExpirado_ConsultaNormalmente()
    {
        _context.AppSettings.Add(new AppSetting
        {
            Key = SettingKeys.FiscalSyncBlockedUntil,
            Value = DateTime.UtcNow.AddMinutes(-1).ToString("O")
        });
        _context.SaveChanges();
        _gateway.StatusToReturn = 137;

        var result = await _service.SyncAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _gateway.Calls);
    }

    [Fact]
    public async Task ResyncFromStart_ZeraNsuEBloqueio_ELeDoInicio()
    {
        // NSU já avançado e bloqueio ativo, como se as notas tivessem ficado para
        // trás (ex.: após trocar de ambiente) e o guard de 1 hora travasse a consulta.
        _context.AppSettings.AddRange(
            new AppSetting { Key = SettingKeys.FiscalUltNsu, Value = "000000000000500" },
            new AppSetting
            {
                Key = SettingKeys.FiscalSyncBlockedUntil,
                Value = DateTime.UtcNow.AddHours(1).ToString("O")
            });
        _context.SaveChanges();
        _gateway.StatusToReturn = 137;

        var result = await _service.ResyncFromStartAsync();

        Assert.True(result.IsSuccess);
        // Releu desde o início, apesar do bloqueio ativo, e a partir do NSU 0.
        Assert.Equal(1, _gateway.Calls);
        Assert.Equal("0", _gateway.LastUltNsu);
        var nsu = await _context.AppSettings.FirstAsync(s => s.Key == SettingKeys.FiscalUltNsu);
        Assert.Equal("0", nsu.Value);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class CountingGateway : IFiscalGateway
    {
        public int Calls;
        public int StatusToReturn = 137;
        public string LastUltNsu = string.Empty;

        public Task<FiscalSyncResult> FetchDocumentsAsync(FiscalConfig config, string ultNsu, CancellationToken ct = default)
        {
            Calls++;
            LastUltNsu = ultNsu;
            return Task.FromResult(new FiscalSyncResult(StatusToReturn,
                StatusToReturn == 656 ? "Rejeicao: Consumo Indevido" : "Nenhum documento localizado",
                ultNsu, ultNsu, []));
        }

        public Task<ManifestResult> SendManifestationAsync(FiscalConfig config, string accessKey, ManifestationType type, string? justification, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<NfeAuthorizationResult> AuthorizeNfeAsync(FiscalConfig config, NfeDraft draft, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<NfeCancelResult> CancelNfeAsync(FiscalConfig config, string accessKey, string protocol, string justification, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<SefazServiceStatus> CheckServiceStatusAsync(FiscalConfig config, CancellationToken ct = default)
            => throw new NotSupportedException();
        public CertificateInfo InspectCertificate(byte[] pfxBytes, string password) => throw new NotSupportedException();
        public IReadOnlyList<InstalledCertificate> ListInstalledCertificates() => [];
        public CertificateInfo InspectStoreCertificate(string thumbprint) => throw new NotSupportedException();
    }

    private sealed class FakeSession : ICurrentSession
    {
        public int? UserId => 1;
        public string UserName => "teste";
        public bool IsAuthenticated => true;
        public IReadOnlySet<string> Permissions => new HashSet<string>();
        public bool HasPermission(string permissionCode) => true;
    }

    private sealed class NoopDanfe : IDanfeGenerator
    {
        public byte[] GeneratePdf(string procNFeXml) => [];
    }
}
