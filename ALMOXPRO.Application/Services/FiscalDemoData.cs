namespace ALMOXPRO.Application.Services;

/// <summary>Nota de exemplo do modo demonstração.</summary>
public record FiscalDemoDocument(string AccessKey, string Nsu, string SummaryXml, string FullXml, bool StartsWithFullXml);

/// <summary>
/// Notas fiscais fictícias do modo demonstração: permitem exercitar todo o
/// fluxo (sincronizar, ciência, DANFE, confirmação e recusa) sem certificado
/// e sem comunicação com a SEFAZ. CNPJs e chaves são inválidos de propósito.
/// </summary>
public static class FiscalDemoData
{
    public static IReadOnlyList<FiscalDemoDocument> Documents { get; } =
    [
        Build("11111111000191", "DISTRIBUIDORA DE ALIMENTOS DEMO LTDA", "2001", 1523.45m,
            [("ARZ5", "Arroz agulhinha tipo 1 - 5kg", 60, 22.50m), ("FEJ1", "Feijão carioca 1kg", 80, 7.90m), ("OLE9", "Óleo de soja 900ml", 24, 6.75m)],
            nsu: "000000000000101", startsFull: true),
        Build("22222222000191", "HIGIENE E LIMPEZA PROFISSIONAL DEMO SA", "2002", 894.30m,
            [("DET5", "Detergente neutro 5L", 30, 18.90m), ("PAP300", "Papel toalha interfolha (cx 3000)", 12, 27.40m)],
            nsu: "000000000000102", startsFull: false),
        Build("33333333000191", "HORTIFRUTI CENTRAL DEMO EIRELI", "2003", 412.80m,
            [("TOM1", "Tomate italiano kg", 48, 5.60m), ("BAT1", "Batata inglesa kg", 60, 2.40m)],
            nsu: "000000000000103", startsFull: false),
    ];

    private static FiscalDemoDocument Build(string cnpj, string name, string nNF, decimal total,
        (string Code, string Desc, int Qty, decimal Unit)[] items, string nsu, bool startsFull)
    {
        // Chave fictícia: UF 35 + AAMM + CNPJ + modelo/série/número (não passa em validação real).
        var accessKey = $"3526{cnpj}55001000000{nNF}1000012349".PadRight(44, '0')[..44];
        var issued = DateTime.Now.AddHours(-Random.Shared.Next(2, 48)).ToString("yyyy-MM-ddTHH:mm:sszzz");

        var summary = $"""
            <resNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
                <chNFe>{accessKey}</chNFe>
                <CNPJ>{cnpj}</CNPJ>
                <xNome>{name}</xNome>
                <dhEmi>{issued}</dhEmi>
                <tpNF>1</tpNF>
                <vNF>{total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}</vNF>
                <nProt>935260000000001</nProt>
                <cSitNFe>1</cSitNFe>
            </resNFe>
            """;

        var dets = string.Join("\n", items.Select((i, index) => $"""
              <det nItem="{index + 1}">
                <prod>
                  <cProd>{i.Code}</cProd>
                  <xProd>{i.Desc}</xProd>
                  <NCM>10063021</NCM>
                  <uCom>UN</uCom>
                  <qCom>{i.Qty}.0000</qCom>
                  <vUnCom>{i.Unit.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)}</vUnCom>
                  <vProd>{(i.Qty * i.Unit).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}</vProd>
                </prod>
              </det>
            """));

        var full = $"""
            <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
              <NFe>
                <infNFe Id="NFe{accessKey}" versao="4.00">
                  <ide>
                    <nNF>{nNF}</nNF>
                    <serie>1</serie>
                    <dhEmi>{issued}</dhEmi>
                  </ide>
                  <emit>
                    <CNPJ>{cnpj}</CNPJ>
                    <xNome>{name}</xNome>
                    <IE>123456789</IE>
                    <enderEmit>
                      <xLgr>Avenida Demonstração</xLgr>
                      <nro>1000</nro>
                      <xBairro>Centro</xBairro>
                      <xMun>São Paulo</xMun>
                      <UF>SP</UF>
                      <CEP>01000000</CEP>
                    </enderEmit>
                  </emit>
                  <dest>
                    <CNPJ>99999999000191</CNPJ>
                    <xNome>SUA EMPRESA (DEMONSTRACAO)</xNome>
                    <enderDest>
                      <xLgr>Rua do Hotel</xLgr>
                      <nro>500</nro>
                      <xMun>São Paulo</xMun>
                      <UF>SP</UF>
                    </enderDest>
                  </dest>
            {dets}
                  <total>
                    <ICMSTot>
                      <vProd>{total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}</vProd>
                      <vFrete>0.00</vFrete>
                      <vICMS>0.00</vICMS>
                      <vNF>{total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}</vNF>
                    </ICMSTot>
                  </total>
                </infNFe>
              </NFe>
              <protNFe>
                <infProt>
                  <nProt>935260000000001</nProt>
                  <dhRecbto>{issued}</dhRecbto>
                </infProt>
              </protNFe>
            </nfeProc>
            """;

        return new FiscalDemoDocument(accessKey, nsu, summary, full, startsFull);
    }
}
