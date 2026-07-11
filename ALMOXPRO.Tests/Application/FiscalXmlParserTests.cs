using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Fiscal;
using ALMOXPRO.Domain.Exceptions;
using Xunit;

namespace ALMOXPRO.Tests.Application;

public class FiscalXmlParserTests
{
    private const string ResNFeXml = """
        <resNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
            <chNFe>35260711222333000181550010000012341000012349</chNFe>
            <CNPJ>11222333000181</CNPJ>
            <xNome>Distribuidora de Alimentos Ltda</xNome>
            <IE>123456789</IE>
            <dhEmi>2026-07-10T14:30:00-03:00</dhEmi>
            <tpNF>1</tpNF>
            <vNF>1523.45</vNF>
            <digVal>abc=</digVal>
            <dhRecbto>2026-07-10T14:31:00-03:00</dhRecbto>
            <nProt>135260000000001</nProt>
            <cSitNFe>1</cSitNFe>
        </resNFe>
        """;

    private const string ProcNFeXml = """
        <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
          <NFe>
            <infNFe Id="NFe35260711222333000181550010000012341000012349" versao="4.00">
              <ide>
                <nNF>1234</nNF>
                <serie>1</serie>
                <dhEmi>2026-07-10T14:30:00-03:00</dhEmi>
              </ide>
              <emit>
                <CNPJ>11222333000181</CNPJ>
                <xNome>Distribuidora de Alimentos Ltda</xNome>
              </emit>
              <det nItem="1">
                <prod>
                  <cProd>ABC1</cProd>
                  <xProd>Leite integral 1L</xProd>
                  <qCom>100.0000</qCom>
                  <vUnCom>5.5000</vUnCom>
                  <vProd>550.00</vProd>
                </prod>
              </det>
              <total>
                <ICMSTot>
                  <vProd>550.00</vProd>
                  <vNF>1523.45</vNF>
                </ICMSTot>
              </total>
            </infNFe>
          </NFe>
          <protNFe><infProt><nProt>135260000000001</nProt></infProt></protNFe>
        </nfeProc>
        """;

    [Fact]
    public void ParseResNFe_ExtraiCamposDoResumo()
    {
        var parsed = FiscalXmlParser.ParseResNFe(ResNFeXml);

        Assert.Equal("35260711222333000181550010000012341000012349", parsed.AccessKey);
        Assert.Equal("11222333000181", parsed.EmitterCnpj);
        Assert.Equal("Distribuidora de Alimentos Ltda", parsed.EmitterName);
        Assert.Equal(1523.45m, parsed.TotalValue);
        Assert.Equal(new DateTime(2026, 7, 10, 17, 30, 0, DateTimeKind.Utc), parsed.IssuedAt);
    }

    [Fact]
    public void ParseProcNFe_ExtraiCamposDaNotaCompleta()
    {
        var parsed = FiscalXmlParser.ParseProcNFe(ProcNFeXml);

        Assert.Equal("35260711222333000181550010000012341000012349", parsed.AccessKey);
        Assert.Equal("11222333000181", parsed.EmitterCnpj);
        Assert.Equal("Distribuidora de Alimentos Ltda", parsed.EmitterName);
        Assert.Equal(1523.45m, parsed.TotalValue);
    }
}

public class FiscalDocumentDomainTests
{
    private static FiscalDocument BuildDocument() => new()
    {
        AccessKey = new string('1', 44),
        EmitterCnpj = "11222333000181",
        EmitterName = "Fornecedor"
    };

    [Fact]
    public void RegisterManifestation_Ciencia_AtualizaStatus()
    {
        var document = BuildDocument();

        document.RegisterManifestation(FiscalDocumentStatus.Ciencia, 7, null);

        Assert.Equal(FiscalDocumentStatus.Ciencia, document.Status);
        Assert.Equal(7, document.ManifestedByUserId);
        Assert.NotNull(document.ManifestedAt);
    }

    [Fact]
    public void RegisterManifestation_AposCiencia_PodeConfirmarOuRecusar()
    {
        var document = BuildDocument();
        document.RegisterManifestation(FiscalDocumentStatus.Ciencia, 1, null);

        document.RegisterManifestation(FiscalDocumentStatus.Confirmada, 1, null);

        Assert.Equal(FiscalDocumentStatus.Confirmada, document.Status);
    }

    [Fact]
    public void RegisterManifestation_AposDefinitiva_LancaExcecao()
    {
        var document = BuildDocument();
        document.RegisterManifestation(FiscalDocumentStatus.Confirmada, 1, null);

        Assert.Throws<DomainException>(() =>
            document.RegisterManifestation(FiscalDocumentStatus.Desconhecida, 1, null));
    }

    [Fact]
    public void RegisterManifestation_RecusaSemJustificativa_LancaExcecao()
    {
        var document = BuildDocument();

        Assert.Throws<DomainException>(() =>
            document.RegisterManifestation(FiscalDocumentStatus.OperacaoNaoRealizada, 1, "curta"));
    }

    [Fact]
    public void RegisterManifestation_RecusaComJustificativa_Passa()
    {
        var document = BuildDocument();

        document.RegisterManifestation(FiscalDocumentStatus.OperacaoNaoRealizada, 1,
            "Mercadoria recebida em desacordo com o pedido de compra.");

        Assert.Equal(FiscalDocumentStatus.OperacaoNaoRealizada, document.Status);
        Assert.NotNull(document.ManifestJustification);
    }
}
