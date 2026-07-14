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

    /// <summary>
    /// A Distribuição DF-e também entrega NFC-e (modelo 65) quando a compra é
    /// feita com o CNPJ da empresa. A DanfeSharp só desenha NF-e (55); o
    /// serviço deve responder com mensagem clara em vez de estourar exceção.
    /// </summary>
    [Fact]
    public async Task Danfe_DeNfceModelo65_RetornaMensagemAmigavel()
    {
        _context.FiscalDocuments.Add(new global::ALMOXPRO.Domain.Entities.Fiscal.FiscalDocument
        {
            AccessKey = new string('5', 44),
            Nsu = "999",
            EmitterCnpj = "11222333000181",
            EmitterName = "MERCADO ATACADISTA DEMO",
            IssuedAt = DateTime.UtcNow,
            TotalValue = 150.00m,
            HasFullXml = true,
            Xml = """
                <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
                  <NFe><infNFe Id="NFe55555" versao="4.00">
                    <ide><cUF>33</cUF><mod>65</mod><serie>1</serie><nNF>123</nNF></ide>
                  </infNFe></NFe>
                </nfeProc>
                """
        });
        _context.SaveChanges();

        var doc = await _context.FiscalDocuments.FirstAsync(d => d.EmitterName == "MERCADO ATACADISTA DEMO");
        var result = await _service.GetDanfePdfAsync(doc.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("NFC-e", result.Error);

        // O XML continua disponível para exportação.
        var xml = await _service.GetXmlAsync(doc.Id);
        Assert.True(xml.IsSuccess);
        Assert.Contains("<mod>65</mod>", xml.Value);
    }

    [Fact]
    public async Task PurgeDemoData_RemoveNotasFicticiasELiberaSincronizacao()
    {
        // Notas recebidas de exemplo.
        await _service.SyncAsync();
        Assert.Equal(FiscalDemoData.Documents.Count, await _context.FiscalDocuments.CountAsync());

        // Uma nota emitida de demonstração (protocolo fictício) e uma "real".
        _context.IssuedNfes.Add(new global::ALMOXPRO.Domain.Entities.Fiscal.IssuedNfe
        {
            AccessKey = new string('1', 44),
            Number = 1,
            Series = 1,
            Protocol = IssuedNfeDemoXml.DemoProtocol,
            IssuedAt = DateTime.UtcNow,
            TotalValue = 10m,
            Xml = "<nfeProc/>"
        });
        _context.IssuedNfes.Add(new global::ALMOXPRO.Domain.Entities.Fiscal.IssuedNfe
        {
            AccessKey = new string('2', 44),
            Number = 2,
            Series = 1,
            Protocol = "135260000000009",
            IssuedAt = DateTime.UtcNow,
            TotalValue = 20m,
            IsProduction = true,
            Xml = "<nfeProc/>"
        });
        // Bloqueio de sincronização ativo (cStat 656).
        _context.AppSettings.Add(new AppSetting
        {
            Key = SettingKeys.FiscalSyncBlockedUntil,
            Value = DateTime.UtcNow.AddHours(1).ToString("O")
        });
        _context.SaveChanges();

        var purge = await _service.PurgeDemoDataAsync();

        Assert.True(purge.IsSuccess);
        Assert.Equal(FiscalDemoData.Documents.Count + 1, purge.Value);
        // Recebidas de exemplo: todas removidas.
        Assert.Equal(0, await _context.FiscalDocuments.CountAsync());
        // Emitidas: a real permanece, a de demonstração some.
        var restantes = await _context.IssuedNfes.ToListAsync();
        Assert.Single(restantes);
        Assert.True(restantes[0].IsProduction);
        // Bloqueio liberado.
        var block = await _context.AppSettings.FirstAsync(s => s.Key == SettingKeys.FiscalSyncBlockedUntil);
        Assert.Equal(string.Empty, block.Value);
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
