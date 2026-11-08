using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Persistence;
using ALMOXPRO.Persistence.Context;
using DanfeSharp.Modelo;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALMOXPRO.Tests.Integration;

/// <summary>
/// Emissão de NF-e no modo demonstração em banco real (SQLite), sem SEFAZ:
/// emitir → numerar em sequência → XML compatível com a DANFE → cancelar.
/// </summary>
public class FiscalEmissionDemoTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AlmoxProDbContext _context;
    private readonly UnitOfWork _uow;
    private readonly FiscalEmissionService _service;

    public FiscalEmissionDemoTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new AlmoxProDbContext(new DbContextOptionsBuilder<AlmoxProDbContext>()
            .UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _uow = new UnitOfWork(_context);

        _context.AppSettings.Add(new AppSetting { Key = SettingKeys.FiscalDemoMode, Value = "true" });
        _context.SaveChanges();

        _service = new FiscalEmissionService(_uow, new ThrowingGateway(), new NoopDanfe(),
            new FakeSession(), NullLogger<FiscalEmissionService>.Instance);
    }

    private static IssueNfeInput ValidInput(string recipientName = "CLIENTE DEMO LTDA") => new(
        NatureOfOperation: "Remessa de material",
        IsTaxedSale: false,
        PaymentMethod: 1,
        IsDevolution: false,
        ReferencedAccessKey: null,
        RecipientCnpjCpf: "45.723.174/0001-10",
        RecipientName: recipientName,
        RecipientIeIndicator: 9,
        RecipientIe: null,
        RecipientStreet: "Rua das Entregas",
        RecipientNumber: "42",
        RecipientDistrict: "Industrial",
        RecipientCityCode: "3550308",
        RecipientCityName: "São Paulo",
        RecipientUf: "SP",
        RecipientCep: "01310-000",
        AdditionalInfo: "Remessa referente à requisição 123.",
        Items:
        [
            new IssueNfeItemInput("ARZ5", "Arroz agulhinha tipo 1 - 5kg", "10063021", "5949", "UN", 10, 22.50m),
            new IssueNfeItemInput("", "Detergente neutro 5L", "34022000", "5949", "GL", 2, 18.90m)
        ]);

    [Fact]
    public async Task Issue_ModoDemo_AutorizaESalvaComChaveValida()
    {
        var result = await _service.IssueAsync(ValidInput());

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        var dto = result.Value;
        Assert.Equal(1, dto.Number);
        Assert.Equal(IssuedNfeStatus.Autorizada, dto.Status);
        Assert.Equal(44, dto.AccessKey.Length);
        Assert.False(dto.IsProduction);
        Assert.Equal(IssuedNfeDemoXml.DemoProtocol, dto.Protocol);
        // O DV embutido na chave confere com o algoritmo oficial (módulo 11).
        Assert.Equal(dto.AccessKey[43] - '0', NfeAccessKey.CheckDigit(dto.AccessKey[..43]));
        Assert.Equal(10 * 22.50m + 2 * 18.90m, dto.TotalValue);
    }

    [Fact]
    public async Task Issue_DuasNotas_NumeraEmSequencia()
    {
        var first = await _service.IssueAsync(ValidInput());
        var second = await _service.IssueAsync(ValidInput("OUTRO CLIENTE SA"));

        Assert.Equal(1, first.Value.Number);
        Assert.Equal(2, second.Value.Number);
        Assert.NotEqual(first.Value.AccessKey, second.Value.AccessKey);
    }

    [Fact]
    public async Task Issue_XmlGerado_EhCompativelComDanfeSharp()
    {
        var result = await _service.IssueAsync(ValidInput());
        var xml = (await _service.GetXmlAsync(result.Value.Id)).Value;

        var model = DanfeViewModel.CreateFromXmlString(xml);

        Assert.NotNull(model);
        Assert.Equal(2, model.Produtos.Count);
        Assert.False(string.IsNullOrWhiteSpace(model.Emitente.RazaoSocial));
        Assert.Equal(result.Value.AccessKey, model.ChaveAcesso);
    }

    [Fact]
    public async Task Issue_SemItens_Falha()
    {
        var input = ValidInput() with { Items = [] };

        var result = await _service.IssueAsync(input);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Contains("ao menos um item"));
    }

    [Fact]
    public async Task Issue_DevolucaoSemChaveReferenciada_Falha()
    {
        var input = ValidInput() with { IsDevolution = true, ReferencedAccessKey = null };

        var result = await _service.IssueAsync(input);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Contains("devolução"));
    }

    private static IssueNfeInput TaxedSaleInput() => ValidInput("CONSUMIDOR RESTAURANTE") with
    {
        NatureOfOperation = "Venda de mercadoria",
        IsTaxedSale = true,
        PaymentMethod = 17, // PIX
        Items =
        [
            // Refeição: ICMS integral de 18% sobre 100,00.
            new IssueNfeItemInput("REF1", "Refeição executiva", "21069090", "5102", "UN", 10, 10.00m,
                IcmsCst: "00", IcmsRate: 18m),
            // Bebida: ICMS-ST já retido pelo fornecedor (CST 60).
            new IssueNfeItemInput("BEB1", "Refrigerante lata 350ml", "22021000", "5405", "UN", 10, 5.00m,
                IcmsCst: "60")
        ]
    };

    [Fact]
    public async Task Issue_VendaTributada_CalculaIcmsPisCofinsEPagamento()
    {
        var result = await _service.IssueAsync(TaxedSaleInput());

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.Equal(150.00m, result.Value.TotalValue);

        var xml = System.Xml.Linq.XDocument.Parse((await _service.GetXmlAsync(result.Value.Id)).Value);
        System.Xml.Linq.XNamespace ns = "http://www.portalfiscal.inf.br/nfe";
        var tot = xml.Descendants(ns + "ICMSTot").Single();

        // Só a refeição (CST 00) destaca ICMS: base 100,00 × 18% = 18,00.
        Assert.Equal("100.00", tot.Element(ns + "vBC")!.Value);
        Assert.Equal("18.00", tot.Element(ns + "vICMS")!.Value);
        // PIS 0,65% e COFINS 3,00% (padrão cumulativo) sobre os 150,00: 0,65+0,33 e 3,00+1,50.
        Assert.Equal("0.98", tot.Element(ns + "vPIS")!.Value);
        Assert.Equal("4.50", tot.Element(ns + "vCOFINS")!.Value);

        var pag = xml.Descendants(ns + "detPag").Single();
        Assert.Equal("17", pag.Element(ns + "tPag")!.Value);
        Assert.Equal("150.00", pag.Element(ns + "vPag")!.Value);

        // Consumidor final em operação presencial.
        Assert.Equal("1", xml.Descendants(ns + "indFinal").Single().Value);
        Assert.Equal("1", xml.Descendants(ns + "indPres").Single().Value);

        // XML continua compatível com a DANFE.
        var model = DanfeViewModel.CreateFromXmlString(xml.ToString());
        Assert.Equal(2, model.Produtos.Count);
    }

    [Fact]
    public async Task Issue_VendaTributada_PisCofinsOutras_SaiCst99Zerado()
    {
        var result = await _service.IssueAsync(TaxedSaleInput() with { PisCofinsOutras = true });

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));

        var xml = System.Xml.Linq.XDocument.Parse((await _service.GetXmlAsync(result.Value.Id)).Value);
        System.Xml.Linq.XNamespace ns = "http://www.portalfiscal.inf.br/nfe";

        // PIS/COFINS como "outras operações": CST 99 com valores zerados.
        Assert.Equal(2, xml.Descendants(ns + "PISOutr").Count());
        Assert.All(xml.Descendants(ns + "PISOutr"), p =>
        {
            Assert.Equal("99", p.Element(ns + "CST")!.Value);
            Assert.Equal("0.00", p.Element(ns + "vPIS")!.Value);
        });
        Assert.Empty(xml.Descendants(ns + "PISAliq"));

        var tot = xml.Descendants(ns + "ICMSTot").Single();
        Assert.Equal("0.00", tot.Element(ns + "vPIS")!.Value);
        Assert.Equal("0.00", tot.Element(ns + "vCOFINS")!.Value);
        // O ICMS continua destacado normalmente.
        Assert.Equal("18.00", tot.Element(ns + "vICMS")!.Value);
    }

    [Fact]
    public async Task Issue_VendaTributada_CstInvalido_Falha()
    {
        var input = TaxedSaleInput() with
        {
            Items = [new IssueNfeItemInput("X", "Item", "21069090", "5102", "UN", 1, 10m, IcmsCst: "99")]
        };

        var result = await _service.IssueAsync(input);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Contains("CST"));
    }

    [Fact]
    public async Task Issue_VendaTributada_Cst00SemAliquota_Falha()
    {
        var input = TaxedSaleInput() with
        {
            Items = [new IssueNfeItemInput("X", "Item", "21069090", "5102", "UN", 1, 10m, IcmsCst: "00")]
        };

        var result = await _service.IssueAsync(input);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Contains("alíquota"));
    }

    [Fact]
    public async Task Issue_VendaTributada_ComoDevolucao_Falha()
    {
        var input = TaxedSaleInput() with { IsDevolution = true, ReferencedAccessKey = new string('1', 44) };

        var result = await _service.IssueAsync(input);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Issue_VendaTributada_Cst20_ReduzBase()
    {
        var input = TaxedSaleInput() with
        {
            Items =
            [
                // 200,00 com base reduzida a 60% (redução de 40%): vBC 120,00 × 12% = 14,40.
                new IssueNfeItemInput("R", "Refeição no salão", "21069090", "5102", "UN", 20, 10.00m,
                    IcmsCst: "20", IcmsRate: 12m, IcmsBaseReductionPct: 40m)
            ]
        };

        var result = await _service.IssueAsync(input);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        var xml = System.Xml.Linq.XDocument.Parse((await _service.GetXmlAsync(result.Value.Id)).Value);
        System.Xml.Linq.XNamespace ns = "http://www.portalfiscal.inf.br/nfe";
        var icms20 = xml.Descendants(ns + "ICMS20").Single();
        Assert.Equal("40.00", icms20.Element(ns + "pRedBC")!.Value);
        Assert.Equal("120.00", icms20.Element(ns + "vBC")!.Value);
        Assert.Equal("14.40", icms20.Element(ns + "vICMS")!.Value);
    }

    [Fact]
    public async Task Cancel_ModoDemo_RegistraCancelamento()
    {
        var issued = await _service.IssueAsync(ValidInput());

        var cancel = await _service.CancelAsync(issued.Value.Id,
            "Nota emitida com dados incorretos do destinatário.");

        Assert.True(cancel.IsSuccess, string.Join("; ", cancel.Errors));
        var entity = await _context.IssuedNfes.SingleAsync(n => n.Id == issued.Value.Id);
        Assert.Equal(IssuedNfeStatus.Cancelada, entity.Status);
        Assert.NotNull(entity.CanceledAt);
    }

    [Fact]
    public async Task Cancel_JustificativaCurta_Falha()
    {
        var issued = await _service.IssueAsync(ValidInput());

        var cancel = await _service.CancelAsync(issued.Value.Id, "curta");

        Assert.True(cancel.IsFailure);
    }

    [Fact]
    public async Task Cancel_NotaJaCancelada_Falha()
    {
        var issued = await _service.IssueAsync(ValidInput());
        await _service.CancelAsync(issued.Value.Id, "Nota emitida com dados incorretos do destinatário.");

        var again = await _service.CancelAsync(issued.Value.Id, "Tentativa repetida de cancelamento da nota.");

        Assert.True(again.IsFailure);
    }

    [Fact]
    public async Task CheckServiceStatus_ModoDemo_RespondeOnlineSemGateway()
    {
        var result = await _service.CheckServiceStatusAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Online);
        Assert.Equal(107, result.Value.StatusCode);
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

    // No modo demonstração o gateway nunca deve ser chamado.
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
