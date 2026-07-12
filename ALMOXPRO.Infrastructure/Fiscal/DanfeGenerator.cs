using ALMOXPRO.Application.Interfaces;
using DanfeSharp;
using DanfeSharp.Modelo;

namespace ALMOXPRO.Infrastructure.Fiscal;

/// <summary>
/// DANFE (Documento Auxiliar da Nota Fiscal Eletrônica) em PDF no layout
/// oficial padronizado da SEFAZ, gerado com a biblioteca gratuita DanfeSharp
/// a partir do XML procNFe (nfeProc) autorizado.
/// </summary>
public class DanfeGenerator : IDanfeGenerator
{
    private const string Creditos = "Gerado pelo ALMOX PRO — sem valor fiscal, confira sempre o XML autorizado.";
    private const string Criador = "ALMOX PRO";

    public byte[] GeneratePdf(string procNFeXml)
    {
        // CreateFromXmlString desserializa o nfeProc no modelo oficial; exige
        // uma NF-e modelo 55 autorizada (com protNFe). O XML real da SEFAZ é
        // completo; as notas do modo demonstração são preenchidas para atender
        // ao mesmo esquema.
        var viewModel = DanfeViewModel.CreateFromXmlString(procNFeXml);

        using var danfe = new Danfe(viewModel, Creditos, Criador);
        danfe.Gerar();

        using var stream = new MemoryStream();
        danfe.Salvar(stream);
        return stream.ToArray();
    }
}
