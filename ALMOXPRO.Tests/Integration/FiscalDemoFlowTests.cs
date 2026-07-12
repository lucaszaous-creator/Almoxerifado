using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Persistence;
using ALMOXPRO.Persistence.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALMOXPRO.Tests.Integration;

/// <summary>
/// Fluxo do modo demonstração fiscal em banco real (SQLite), sem SEFAZ:
/// sincronizar → ciência → nova sincronização libera o XML completo.
/// </summary>
public class FiscalDemoFlowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AlmoxProDbContext _context;
    private readonly UnitOfWork _uow;
    private readonly FiscalService _service;

    public FiscalDemoFlowTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new AlmoxProDbContext(new DbContextOptionsBuilder<AlmoxProDbContext>()
            .UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _uow = new UnitOfWork(_context);

        // Ativa o modo demonstração.
        _context.AppSettings.Add(new AppSetting { Key = SettingKeys.FiscalDemoMode, Value = "true" });
        _context.SaveChanges();

        _service = new FiscalService(_uow, new ThrowingGateway(), new NoopDanfe(),
            new FakeSession(), NullLogger<FiscalService>.Instance);
    }

    [Fact]
    public async Task Sync_ModoDemo_CriaNotasDeExemplo()
    {
        var result = await _service.SyncAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(FiscalDemoData.Documents.Count, result.Value.NewDocuments);
        Assert.Equal(FiscalDemoData.Documents.Count, await _context.FiscalDocuments.CountAsync());
    }

    [Fact]
    public async Task Sync_ExecutadoDuasVezes_NaoDuplica()
    {
        await _service.SyncAsync();
        var second = await _service.SyncAsync();

        Assert.Equal(0, second.Value.NewDocuments);
        Assert.Equal(FiscalDemoData.Documents.Count, await _context.FiscalDocuments.CountAsync());
    }

    [Fact]
    public async Task Ciencia_LiberaXmlCompletoNaProximaSincronizacao()
    {
        await _service.SyncAsync();

        // Nota que chegou apenas como resumo (sem XML completo).
        var resumo = await _context.FiscalDocuments.FirstAsync(d => !d.HasFullXml);

        var manifest = await _service.ManifestAsync(resumo.Id, ManifestationType.Ciencia, null);
        Assert.True(manifest.IsSuccess);

        var afterManifest = await _context.FiscalDocuments.FirstAsync(d => d.Id == resumo.Id);
        Assert.Equal(FiscalDocumentStatus.Ciencia, afterManifest.Status);
        Assert.False(afterManifest.HasFullXml);

        // Sincroniza de novo: o XML completo é liberado (como na SEFAZ real).
        await _service.SyncAsync();
        var afterSync = await _context.FiscalDocuments.FirstAsync(d => d.Id == resumo.Id);
        Assert.True(afterSync.HasFullXml);
    }

    [Fact]
    public async Task Recusa_ModoDemo_RegistraOperacaoNaoRealizada()
    {
        await _service.SyncAsync();
        var doc = await _context.FiscalDocuments.FirstAsync();

        var result = await _service.ManifestAsync(doc.Id, ManifestationType.OperacaoNaoRealizada,
            "Mercadoria recebida em desacordo com o pedido.");

        Assert.True(result.IsSuccess);
        var updated = await _context.FiscalDocuments.FirstAsync(d => d.Id == doc.Id);
        Assert.Equal(FiscalDocumentStatus.OperacaoNaoRealizada, updated.Status);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class FakeSession : ICurrentSession
    {
        public int? UserId => 1;
        public string UserName => "teste";
        public bool IsAuthenticated => true;
        public IReadOnlySet<string> Permissions => new HashSet<string>();
        public bool HasPermission(string permissionCode) => true;
    }

    // No modo demo o gateway nunca é chamado; se for, o teste falha claramente.
    private sealed class ThrowingGateway : IFiscalGateway
    {
        public Task<FiscalSyncResult> FetchDocumentsAsync(FiscalConfig config, string ultNsu, CancellationToken ct = default)
            => throw new InvalidOperationException("Gateway não deve ser chamado no modo demonstração.");
        public Task<ManifestResult> SendManifestationAsync(FiscalConfig config, string accessKey, ManifestationType type, string? justification, CancellationToken ct = default)
            => throw new InvalidOperationException("Gateway não deve ser chamado no modo demonstração.");
        public CertificateInfo InspectCertificate(byte[] pfxBytes, string password)
            => throw new InvalidOperationException();
        public IReadOnlyList<InstalledCertificate> ListInstalledCertificates() => [];
        public CertificateInfo InspectStoreCertificate(string thumbprint)
            => throw new InvalidOperationException();
        public Task<NfeAuthorizationResult> AuthorizeNfeAsync(FiscalConfig config, NfeDraft draft, CancellationToken ct = default)
            => throw new InvalidOperationException("Gateway não deve ser chamado no modo demonstração.");
        public Task<NfeCancelResult> CancelNfeAsync(FiscalConfig config, string accessKey, string protocol, string justification, CancellationToken ct = default)
            => throw new InvalidOperationException("Gateway não deve ser chamado no modo demonstração.");
        public Task<SefazServiceStatus> CheckServiceStatusAsync(FiscalConfig config, CancellationToken ct = default)
            => throw new InvalidOperationException("Gateway não deve ser chamado no modo demonstração.");
    }

    private sealed class NoopDanfe : IDanfeGenerator
    {
        public byte[] GeneratePdf(string procNFeXml) => [];
    }
}
