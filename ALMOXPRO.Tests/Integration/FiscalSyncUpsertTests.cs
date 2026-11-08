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
/// Na Distribuição DF-e, a mesma chave de acesso pode chegar duas vezes dentro do
/// mesmo lote: primeiro o resumo (resNFe) e, na sequência, a nota completa
/// (procNFe). Como o SaveChanges só acontece ao fim do laço, a consulta de
/// duplicidade precisa enxergar o documento recém-adicionado e ainda não salvo —
/// caso contrário um segundo INSERT viola o índice único de AccessKey e a
/// sincronização falha com "An error occurred while saving the entity changes".
/// </summary>
public class FiscalSyncUpsertTests : IDisposable
{
    private const string AccessKey = "35200114200166000187550010000000015123456789";

    private readonly SqliteConnection _connection;
    private readonly AlmoxProDbContext _context;
    private readonly FiscalService _service;
    private readonly BatchGateway _gateway = new();

    public FiscalSyncUpsertTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new AlmoxProDbContext(new DbContextOptionsBuilder<AlmoxProDbContext>()
            .UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        _context.AppSettings.AddRange(
            new AppSetting { Key = SettingKeys.FiscalCnpj, Value = "11222333000181" },
            new AppSetting { Key = SettingKeys.FiscalUf, Value = "RJ" },
            new AppSetting { Key = SettingKeys.FiscalCertificatePfx, Value = "QUFBQQ==" });
        _context.SaveChanges();

        _service = new FiscalService(new UnitOfWork(_context), _gateway, new NoopDanfe(),
            new FakeSession(), NullLogger<FiscalService>.Instance);
    }

    [Fact]
    public async Task Sync_ResumoEProcNfeDaMesmaChaveNoMesmoLote_NaoDuplicaNemFalha()
    {
        var result = await _service.SyncAsync();

        Assert.True(result.IsSuccess, string.Join(" ", result.Errors));

        // Uma única nota persistida, já com o XML completo (procNFe substituiu o resumo).
        var docs = await _context.FiscalDocuments.ToListAsync();
        var doc = Assert.Single(docs);
        Assert.Equal(AccessKey, doc.AccessKey);
        Assert.True(doc.HasFullXml);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Devolve um lote (cStat 138) com o resumo e a nota completa da mesma chave.</summary>
    private sealed class BatchGateway : IFiscalGateway
    {
        public Task<FiscalSyncResult> FetchDocumentsAsync(FiscalConfig config, string ultNsu, CancellationToken ct = default)
        {
            var resumo = new FiscalSyncDocument("1", "resNFe_v1.01.xsd", $"""
                <resNFe versao="1.01" xmlns="http://www.portalfiscal.inf.br/nfe">
                  <chNFe>{AccessKey}</chNFe>
                  <CNPJ>14200166000187</CNPJ>
                  <xNome>FORNECEDOR TESTE LTDA</xNome>
                  <dhEmi>2026-07-14T10:00:00-03:00</dhEmi>
                  <vNF>150.00</vNF>
                </resNFe>
                """);

            var completo = new FiscalSyncDocument("2", "procNFe_v4.00.xsd", $"""
                <nfeProc versao="4.00" xmlns="http://www.portalfiscal.inf.br/nfe">
                  <NFe><infNFe Id="NFe{AccessKey}" versao="4.00">
                    <ide><dhEmi>2026-07-14T10:00:00-03:00</dhEmi></ide>
                    <emit><CNPJ>14200166000187</CNPJ><xNome>FORNECEDOR TESTE LTDA</xNome></emit>
                    <total><ICMSTot><vNF>150.00</vNF></ICMSTot></total>
                  </infNFe></NFe>
                </nfeProc>
                """);

            // UltNsu == MaxNsu encerra o laço após esta iteração.
            return Task.FromResult(new FiscalSyncResult(138, "Documentos localizados",
                "2", "2", new[] { resumo, completo }));
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
