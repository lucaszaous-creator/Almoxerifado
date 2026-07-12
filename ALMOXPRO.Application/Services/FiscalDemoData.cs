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
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        // Chave fictícia: UF 35 + AAMM + CNPJ + modelo/série/número (não passa em validação real).
        var accessKey = $"3526{cnpj}55001000000{nNF}1000012349".PadRight(44, '0')[..44];
        var cDV = accessKey[^1];
        var issued = DateTime.Now.AddHours(-Random.Shared.Next(2, 48)).ToString("yyyy-MM-ddTHH:mm:sszzz");

        var summary = $"""
            <resNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
                <chNFe>{accessKey}</chNFe>
                <CNPJ>{cnpj}</CNPJ>
                <xNome>{name}</xNome>
                <dhEmi>{issued}</dhEmi>
                <tpNF>1</tpNF>
                <vNF>{total.ToString("0.00", inv)}</vNF>
                <nProt>935260000000001</nProt>
                <cSitNFe>1</cSitNFe>
            </resNFe>
            """;

        // ICMS demonstrativo de 18% para preencher o quadro "Cálculo do Imposto".
        var vICMSTotal = items.Sum(i => Math.Round(i.Qty * i.Unit * 0.18m, 2));

        var dets = string.Join("\n", items.Select((i, index) =>
        {
            var vProd = (i.Qty * i.Unit).ToString("0.00", inv);
            var vICMS = Math.Round(i.Qty * i.Unit * 0.18m, 2).ToString("0.00", inv);
            var vBC = (i.Qty * i.Unit).ToString("0.00", inv);
            return $"""
                  <det nItem="{index + 1}">
                    <prod>
                      <cProd>{i.Code}</cProd>
                      <cEAN>SEM GTIN</cEAN>
                      <xProd>{i.Desc}</xProd>
                      <NCM>10063021</NCM>
                      <CFOP>5102</CFOP>
                      <uCom>UN</uCom>
                      <qCom>{i.Qty}.0000</qCom>
                      <vUnCom>{i.Unit.ToString("0.0000", inv)}</vUnCom>
                      <vProd>{vProd}</vProd>
                      <cEANTrib>SEM GTIN</cEANTrib>
                      <uTrib>UN</uTrib>
                      <qTrib>{i.Qty}.0000</qTrib>
                      <vUnTrib>{i.Unit.ToString("0.0000", inv)}</vUnTrib>
                      <indTot>1</indTot>
                    </prod>
                    <imposto>
                      <ICMS>
                        <ICMS00>
                          <orig>0</orig>
                          <CST>00</CST>
                          <modBC>3</modBC>
                          <vBC>{vBC}</vBC>
                          <pICMS>18.00</pICMS>
                          <vICMS>{vICMS}</vICMS>
                        </ICMS00>
                      </ICMS>
                    </imposto>
                  </det>
                """;
        }));

        var full = $"""
            <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
              <NFe>
                <infNFe Id="NFe{accessKey}" versao="4.00">
                  <ide>
                    <cUF>35</cUF>
                    <cNF>10000123</cNF>
                    <natOp>VENDA DE MERCADORIA</natOp>
                    <mod>55</mod>
                    <serie>1</serie>
                    <nNF>{nNF}</nNF>
                    <dhEmi>{issued}</dhEmi>
                    <tpNF>1</tpNF>
                    <idDest>1</idDest>
                    <cMunFG>3550308</cMunFG>
                    <tpImp>1</tpImp>
                    <tpEmis>1</tpEmis>
                    <cDV>{cDV}</cDV>
                    <tpAmb>2</tpAmb>
                    <finNFe>1</finNFe>
                    <indFinal>1</indFinal>
                    <indPres>1</indPres>
                  </ide>
                  <emit>
                    <CNPJ>{cnpj}</CNPJ>
                    <xNome>{name}</xNome>
                    <enderEmit>
                      <xLgr>Avenida Demonstração</xLgr>
                      <nro>1000</nro>
                      <xBairro>Centro</xBairro>
                      <cMun>3550308</cMun>
                      <xMun>São Paulo</xMun>
                      <UF>SP</UF>
                      <CEP>01000000</CEP>
                      <fone>1130000000</fone>
                    </enderEmit>
                    <IE>123456789</IE>
                    <CRT>3</CRT>
                  </emit>
                  <dest>
                    <CNPJ>99999999000191</CNPJ>
                    <xNome>SUA EMPRESA (DEMONSTRACAO)</xNome>
                    <enderDest>
                      <xLgr>Rua do Hotel</xLgr>
                      <nro>500</nro>
                      <xBairro>Centro</xBairro>
                      <cMun>3550308</cMun>
                      <xMun>São Paulo</xMun>
                      <UF>SP</UF>
                      <CEP>01310000</CEP>
                    </enderDest>
                    <indIEDest>1</indIEDest>
                    <IE>987654321</IE>
                  </dest>
            {dets}
                  <total>
                    <ICMSTot>
                      <vBC>{total.ToString("0.00", inv)}</vBC>
                      <vICMS>{vICMSTotal.ToString("0.00", inv)}</vICMS>
                      <vProd>{total.ToString("0.00", inv)}</vProd>
                      <vFrete>0.00</vFrete>
                      <vSeg>0.00</vSeg>
                      <vDesc>0.00</vDesc>
                      <vII>0.00</vII>
                      <vIPI>0.00</vIPI>
                      <vPIS>0.00</vPIS>
                      <vCOFINS>0.00</vCOFINS>
                      <vOutro>0.00</vOutro>
                      <vNF>{total.ToString("0.00", inv)}</vNF>
                    </ICMSTot>
                  </total>
                  <transp>
                    <modFrete>9</modFrete>
                  </transp>
                </infNFe>
              </NFe>
              <protNFe>
                <infProt>
                  <chNFe>{accessKey}</chNFe>
                  <dhRecbto>{issued}</dhRecbto>
                  <nProt>135260000000001</nProt>
                  <cStat>100</cStat>
                  <xMotivo>Autorizado o uso da NF-e</xMotivo>
                </infProt>
              </protNFe>
            </nfeProc>
            """;

        return new FiscalDemoDocument(accessKey, nsu, summary, full, startsFull);
    }
}
