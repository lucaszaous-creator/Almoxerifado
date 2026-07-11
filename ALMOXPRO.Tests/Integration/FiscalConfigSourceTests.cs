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
/// Configuração fiscal com as duas origens de certificado: arquivo A1 (.pfx)
/// e certificado instalado no Windows (thumbprint).
/// </summary>
public class FiscalConfigSourceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AlmoxProDbContext _context;
    private readonly UnitOfWork _uow;
    private readonly FiscalService _service;

    public FiscalConfigSourceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new AlmoxProDbContext(new DbContextOptionsBuilder<AlmoxProDbContext>()
            .UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _uow = new UnitOfWork(_context);
        _service = new FiscalService(_uow, new FakeGateway(), new NoopDanfe(),
            new FakeSession(), NullLogger<FiscalService>.Instance);
    }

    [Fact]
    public async Task SaveConfiguration_ComCertificadoDoWindows_GravaOrigemStoreEThumbprint()
    {
        var input = new FiscalConfigInput(
            UseWindowsStore: true,
            Thumbprint: "ABC123",
            PfxBytes: null,
            PfxPassword: null,
            Cnpj: "11.222.333/0001-81",
            Uf: "SP",
            Production: false);

        var result = await _service.SaveConfigurationAsync(input);

        Assert.True(result.IsSuccess);
        Assert.Equal("store", await Setting(SettingKeys.FiscalCertificateSource));
        Assert.Equal("ABC123", await Setting(SettingKeys.FiscalCertificateThumbprint));
        Assert.Equal("11222333000181", await Setting(SettingKeys.FiscalCnpj));

        // O certificado configurado é lido de volta a partir da loja do Windows.
        var info = await _service.GetCertificateInfoAsync();
        Assert.NotNull(info);
        Assert.Contains("STORE", info!.Subject);
    }

    [Fact]
    public async Task SaveConfiguration_StoreSemSelecionar_Falha()
    {
        var input = new FiscalConfigInput(true, null, null, null, "11222333000181", "SP", true);

        var result = await _service.SaveConfigurationAsync(input);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task SaveConfiguration_ComArquivoPfx_GravaOrigemPfx()
    {
        var input = new FiscalConfigInput(
            UseWindowsStore: false,
            Thumbprint: null,
            PfxBytes: [1, 2, 3],
            PfxPassword: "senha",
            Cnpj: "11222333000181",
            Uf: "SP",
            Production: true);

        var result = await _service.SaveConfigurationAsync(input);

        Assert.True(result.IsSuccess);
        Assert.Equal("pfx", await Setting(SettingKeys.FiscalCertificateSource));
        Assert.False(string.IsNullOrEmpty(await Setting(SettingKeys.FiscalCertificatePfx)));
    }

    private async Task<string?> Setting(string key) =>
        (await _uow.Settings.GetByKeyAsync(key))?.Value;

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class FakeGateway : IFiscalGateway
    {
        public Task<FiscalSyncResult> FetchDocumentsAsync(FiscalConfig config, string ultNsu, CancellationToken ct = default)
            => throw new InvalidOperationException();
        public Task<ManifestResult> SendManifestationAsync(FiscalConfig config, string accessKey, ManifestationType type, string? justification, CancellationToken ct = default)
            => throw new InvalidOperationException();
        public CertificateInfo InspectCertificate(byte[] pfxBytes, string password)
            => new("CN=EMPRESA PFX", "CN=AC", DateTime.Today, DateTime.Today.AddYears(1));
        public IReadOnlyList<InstalledCertificate> ListInstalledCertificates()
            => [new("ABC123", "CN=EMPRESA STORE", DateTime.Today.AddYears(1))];
        public CertificateInfo InspectStoreCertificate(string thumbprint)
            => new($"CN=EMPRESA STORE {thumbprint}", "CN=AC", DateTime.Today, DateTime.Today.AddYears(1));
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
