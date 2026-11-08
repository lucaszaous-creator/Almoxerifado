using ALMOXPRO.Application.Services;
using DanfeSharp.Modelo;
using Xunit;

namespace ALMOXPRO.Tests.Integration;

/// <summary>
/// Garante que o XML das notas do modo demonstração é compatível com o
/// modelo oficial da DanfeSharp (NF-e modelo 55, com protNFe). Valida apenas a
/// desserialização/modelo — a renderização em PDF (GDI+) só roda no Windows,
/// portanto não é exercitada na CI Linux.
/// </summary>
public class DanfeDemoXmlTests
{
    [Fact]
    public void TodasAsNotasDemo_ProduzemModeloDanfeValido()
    {
        Assert.NotEmpty(FiscalDemoData.Documents);

        foreach (var doc in FiscalDemoData.Documents)
        {
            var model = DanfeViewModel.CreateFromXmlString(doc.FullXml);

            Assert.NotNull(model);
            Assert.NotNull(model.Emitente);
            Assert.False(string.IsNullOrWhiteSpace(model.Emitente.RazaoSocial));
            Assert.NotEmpty(model.Produtos);
        }
    }
}
