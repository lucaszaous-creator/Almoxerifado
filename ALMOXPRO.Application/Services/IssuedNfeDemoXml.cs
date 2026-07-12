using ALMOXPRO.Application.Interfaces;
using System.Globalization;
using System.Security;

namespace ALMOXPRO.Application.Services;

/// <summary>
/// Gera o procNFe "autorizado" do modo demonstração a partir do draft — o
/// mesmo esquema aceito pela DanfeSharp, permitindo testar a emissão e a
/// impressão da DANFE sem certificado e sem comunicação com a SEFAZ.
/// </summary>
public static class IssuedNfeDemoXml
{
    public const string DemoProtocol = "999260000000001";

    public static string BuildProcNfe(NfeDraft draft)
    {
        var inv = CultureInfo.InvariantCulture;
        var issued = draft.IssuedAt.ToString("yyyy-MM-ddTHH:mm:sszzz", inv);
        var emitter = draft.Emitter;
        var recipient = draft.Recipient;
        var simples = emitter.Crt == 1;

        var icms = simples
            ? "<ICMSSN102><orig>0</orig><CSOSN>400</CSOSN></ICMSSN102>"
            : "<ICMS40><orig>0</orig><CST>41</CST></ICMS40>";

        var dets = string.Join("\n", draft.Items.Select(i => $"""
                  <det nItem="{i.Number}">
                    <prod>
                      <cProd>{Esc(i.Code)}</cProd>
                      <cEAN>SEM GTIN</cEAN>
                      <xProd>{Esc(i.Description)}</xProd>
                      <NCM>{i.Ncm}</NCM>
                      <CFOP>{i.Cfop}</CFOP>
                      <uCom>{Esc(i.Unit)}</uCom>
                      <qCom>{i.Quantity.ToString("0.0000", inv)}</qCom>
                      <vUnCom>{i.UnitValue.ToString("0.0000", inv)}</vUnCom>
                      <vProd>{i.Total.ToString("0.00", inv)}</vProd>
                      <cEANTrib>SEM GTIN</cEANTrib>
                      <uTrib>{Esc(i.Unit)}</uTrib>
                      <qTrib>{i.Quantity.ToString("0.0000", inv)}</qTrib>
                      <vUnTrib>{i.UnitValue.ToString("0.0000", inv)}</vUnTrib>
                      <indTot>1</indTot>
                    </prod>
                    <imposto>
                      <ICMS>{icms}</ICMS>
                      <PIS><PISNT><CST>08</CST></PISNT></PIS>
                      <COFINS><COFINSNT><CST>08</CST></COFINSNT></COFINS>
                    </imposto>
                  </det>
            """));

        var nfref = string.IsNullOrWhiteSpace(draft.ReferencedAccessKey)
            ? string.Empty
            : $"<NFref><refNFe>{draft.ReferencedAccessKey}</refNFe></NFref>";

        var destDoc = recipient.CnpjCpf.Length == 11
            ? $"<CPF>{recipient.CnpjCpf}</CPF>"
            : $"<CNPJ>{recipient.CnpjCpf}</CNPJ>";

        var destIe = recipient.IeIndicator == 1 && !string.IsNullOrWhiteSpace(recipient.Ie)
            ? $"<IE>{Esc(recipient.Ie!)}</IE>"
            : string.Empty;

        var total = draft.TotalValue.ToString("0.00", inv);
        var infAdic = string.IsNullOrWhiteSpace(draft.AdditionalInfo)
            ? string.Empty
            : $"<infAdic><infCpl>{Esc(draft.AdditionalInfo!)}</infCpl></infAdic>";

        return $"""
            <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
              <NFe>
                <infNFe Id="NFe{draft.AccessKey}" versao="4.00">
                  <ide>
                    <cUF>{NfeAccessKey.UfCodes[emitter.Uf.ToUpperInvariant()]}</cUF>
                    <cNF>{draft.CNf}</cNF>
                    <natOp>{Esc(draft.NatureOfOperation)}</natOp>
                    <mod>55</mod>
                    <serie>{draft.Series}</serie>
                    <nNF>{draft.Number}</nNF>
                    <dhEmi>{issued}</dhEmi>
                    <tpNF>1</tpNF>
                    <idDest>{(emitter.Uf.Equals(recipient.Uf, StringComparison.OrdinalIgnoreCase) ? 1 : 2)}</idDest>
                    <cMunFG>{emitter.CityCode}</cMunFG>
                    <tpImp>1</tpImp>
                    <tpEmis>1</tpEmis>
                    <cDV>{draft.CheckDigit}</cDV>
                    <tpAmb>2</tpAmb>
                    <finNFe>{draft.Finality}</finNFe>
                    <indFinal>0</indFinal>
                    <indPres>0</indPres>
                    {nfref}
                  </ide>
                  <emit>
                    <CNPJ>{emitter.Cnpj}</CNPJ>
                    <xNome>{Esc(emitter.Name)}</xNome>
                    <enderEmit>
                      <xLgr>{Esc(emitter.Street)}</xLgr>
                      <nro>{Esc(emitter.Number)}</nro>
                      <xBairro>{Esc(emitter.District)}</xBairro>
                      <cMun>{emitter.CityCode}</cMun>
                      <xMun>{Esc(emitter.CityName)}</xMun>
                      <UF>{emitter.Uf.ToUpperInvariant()}</UF>
                      <CEP>{emitter.Cep}</CEP>
                    </enderEmit>
                    <IE>{Esc(string.IsNullOrWhiteSpace(emitter.Ie) ? "ISENTO" : emitter.Ie!)}</IE>
                    <CRT>{emitter.Crt}</CRT>
                  </emit>
                  <dest>
                    {destDoc}
                    <xNome>{Esc(recipient.Name)}</xNome>
                    <enderDest>
                      <xLgr>{Esc(recipient.Street)}</xLgr>
                      <nro>{Esc(recipient.Number)}</nro>
                      <xBairro>{Esc(recipient.District)}</xBairro>
                      <cMun>{recipient.CityCode}</cMun>
                      <xMun>{Esc(recipient.CityName)}</xMun>
                      <UF>{recipient.Uf.ToUpperInvariant()}</UF>
                      <CEP>{recipient.Cep}</CEP>
                    </enderDest>
                    <indIEDest>{recipient.IeIndicator}</indIEDest>
                    {destIe}
                  </dest>
            {dets}
                  <total>
                    <ICMSTot>
                      <vBC>0.00</vBC>
                      <vICMS>0.00</vICMS>
                      <vProd>{total}</vProd>
                      <vFrete>0.00</vFrete>
                      <vSeg>0.00</vSeg>
                      <vDesc>0.00</vDesc>
                      <vII>0.00</vII>
                      <vIPI>0.00</vIPI>
                      <vPIS>0.00</vPIS>
                      <vCOFINS>0.00</vCOFINS>
                      <vOutro>0.00</vOutro>
                      <vNF>{total}</vNF>
                    </ICMSTot>
                  </total>
                  <transp>
                    <modFrete>9</modFrete>
                  </transp>
                  <pag>
                    <detPag>
                      <tPag>90</tPag>
                      <vPag>0.00</vPag>
                    </detPag>
                  </pag>
                  {infAdic}
                </infNFe>
              </NFe>
              <protNFe>
                <infProt>
                  <chNFe>{draft.AccessKey}</chNFe>
                  <dhRecbto>{issued}</dhRecbto>
                  <nProt>{DemoProtocol}</nProt>
                  <cStat>100</cStat>
                  <xMotivo>Autorizado o uso da NF-e (DEMONSTRACAO - SEM VALOR FISCAL)</xMotivo>
                </infProt>
              </protNFe>
            </nfeProc>
            """;
    }

    private static string Esc(string value) => SecurityElement.Escape(value);
}
