using ALMOXPRO.Application.Services;
using Xunit;

namespace ALMOXPRO.Tests.Unit;

/// <summary>
/// Leitura do TXT de NF-e exportado pelo PMS (leiaute pipe-delimited do
/// emissor gratuito/UniNFe), incluindo as variações 3.10/4.00 do registro B
/// e a localização do CFOP no registro I pelo formato.
/// </summary>
public class NfeTxtParserTests
{
    private const string Sample400 = """
        NOTAFISCAL|1
        A|4.00|
        B|35|12345678|VENDA DE MERCADORIA|55|1|4321|2026-07-12T12:00:00-03:00||1|1|3550308|1|1|0|2|1|1|1|0|PMS HOTEL 9.1||
        C|HOTEL DEMONSTRACAO LTDA|HOTEL DEMO|123456789012|||5510801|3|
        C02|11222333000181|
        C05|AVENIDA BEIRA MAR|1000||CENTRO|3550308|SAO PAULO|SP|01000000|1058|BRASIL|1130000000|
        E|CLIENTE RESTAURANTE|9||||cliente@email.com|
        E03|39053344705|
        E05|RUA DAS FLORES|55|AP 101|JARDIM|3550308|SAO PAULO|SP|01310000|1058|BRASIL||
        H|1||
        I|REF1|SEM GTIN|REFEICAO EXECUTIVA|21069090||5102|UN|2.0000|45.5000|91.00|SEM GTIN|UN|2.0000|45.5000|||||1|||
        N|
        N02|0|00|3|91.00|18.00|16.38|
        Q|
        Q02|01|91.00|0.65|0.59|
        S|
        S02|01|91.00|3.00|2.73|
        H|2||
        I|BEB1|SEM GTIN|REFRIGERANTE LATA 350ML|22021000||5405|UN|3.0000|8.0000|24.00|SEM GTIN|UN|3.0000|8.0000|||||1|||
        N|
        N08|0|60|||||
        Q|
        Q04|08|
        S|
        S04|08|
        YA|17|115.00|
        Z||PEDIDO 1234 - MESA 07|
        """;

    [Fact]
    public void Parse_Leiaute400_LeCabecalhoItensEImpostos()
    {
        var ok = NfeTxtParser.TryParse(Sample400, out var note, out var errors);

        Assert.True(ok, string.Join("; ", errors));
        Assert.Equal("VENDA DE MERCADORIA", note.NatOp);
        Assert.Equal(1, note.Finality);
        Assert.Equal("11222333000181", note.EmitterCnpj);
        Assert.Equal("39053344705", note.RecipientCnpjCpf);
        Assert.Equal("CLIENTE RESTAURANTE", note.RecipientName);
        Assert.Equal(9, note.RecipientIeIndicator);
        Assert.Equal("RUA DAS FLORES", note.Street);
        Assert.Equal("55", note.Number);
        Assert.Equal("JARDIM", note.District);
        Assert.Equal("3550308", note.CityCode);
        Assert.Equal("SP", note.Uf);
        Assert.Equal(17, note.PaymentMethod);
        Assert.Equal("PEDIDO 1234 - MESA 07", note.AdditionalInfo);
        Assert.True(note.LooksLikeTaxedSale);

        Assert.Equal(2, note.Items.Count);
        var refeicao = note.Items[0];
        Assert.Equal("REFEICAO EXECUTIVA", refeicao.Description);
        Assert.Equal("21069090", refeicao.Ncm);
        Assert.Equal("5102", refeicao.Cfop);
        Assert.Equal("UN", refeicao.Unit);
        Assert.Equal(2m, refeicao.Quantity);
        Assert.Equal(45.5m, refeicao.UnitValue);
        Assert.Equal("00", refeicao.IcmsCst);
        Assert.Equal(18m, refeicao.IcmsRate);

        var bebida = note.Items[1];
        Assert.Equal("5405", bebida.Cfop);
        Assert.Equal("60", bebida.IcmsCst);
        Assert.Null(bebida.IcmsRate);
    }

    [Fact]
    public void Parse_Leiaute310_ComIndPagNoRegistroB_DetectaDeslocamento()
    {
        // 3.10 traz indPag entre natOp e mod; o modelo (55) fica um campo à frente.
        var txt = """
            NOTAFISCAL|1
            A|3.10|
            B|35|00000001|REMESSA DE MATERIAL|0|55|1|77|2026-07-12T09:00:00-03:00||1|1|3550308|1|1|0|2|1|0|0|1|ALMOX||
            C02|11222333000181|
            E|FILIAL PRAIA|1|123456789|||
            E02|44555666000199|
            E05|ROD LITORANEA|500||PRAIA|3550308|SAO PAULO|SP|11700000|1058|BRASIL||
            I|ARZ|SEM GTIN|ARROZ 5KG|10063021||5949|UN|10.0000|20.0000|200.00|SEM GTIN|UN|10.0000|20.0000|||||1|||
            N06|0|41|||
            """;

        var ok = NfeTxtParser.TryParse(txt, out var note, out var errors);

        Assert.True(ok, string.Join("; ", errors));
        Assert.Equal("REMESSA DE MATERIAL", note.NatOp);
        Assert.False(note.LooksLikeTaxedSale);
        Assert.Equal(1, note.RecipientIeIndicator);
        Assert.Equal("41", note.Items[0].IcmsCst);
        Assert.Equal("5949", note.Items[0].Cfop);
    }

    [Fact]
    public void Parse_Devolucao_LeChaveReferenciada()
    {
        var key = new string('7', 44);
        var txt = $"""
            A|4.00|
            B|35|00000002|DEVOLUCAO DE MERCADORIA|55|1|78|2026-07-12T09:00:00-03:00||1|1|3550308|1|1|0|2|4|0|0|1|PMS||
            BA02|{key}|
            E|FORNECEDOR X|1|111||||
            E02|44555666000199|
            E05|RUA A|1||B|3550308|SP|SP|01000000||||
            I|X|SEM GTIN|MERCADORIA DEVOLVIDA|10063021||5202|UN|1.0000|10.0000|10.00|SEM GTIN|UN|1.0000|10.0000|||||1|||
            N06|0|41|||
            """;

        var ok = NfeTxtParser.TryParse(txt, out var note, out var errors);

        Assert.True(ok, string.Join("; ", errors));
        Assert.Equal(4, note.Finality);
        Assert.Equal(key, note.ReferencedKey);
    }

    [Fact]
    public void Parse_Nfce_Modelo65_Rejeita()
    {
        var txt = """
            A|4.00|
            B|35|1|VENDA|65|1|1|2026-07-12T09:00:00-03:00||1|1|3550308|4|1|0|2|1|1|1|0|PDV||
            E02|44555666000199|
            I|X|SEM GTIN|ITEM|10063021||5102|UN|1.0000|10.0000|10.00|SEM GTIN|UN|1.0000|10.0000|||||1|||
            """;

        var ok = NfeTxtParser.TryParse(txt, out _, out var errors);

        Assert.False(ok);
        Assert.Contains(errors, e => e.Contains("modelo 65"));
    }

    /// <summary>
    /// Estrutura idêntica à exportada pelo PMS hoteleiro real (registros
    /// M/N/Q/S vazios, Q05/S05 com CST 99 zerado, pagamento em YA01 com
    /// indPag vazio, X sem separador final e observações no Z).
    /// </summary>
    [Fact]
    public void Parse_ArquivoRealDePmsHoteleiro_LeTudo()
    {
        var txt = """
            NOTAFISCAL|1
            A|4.00|NFe33260211222333000181550010000364791005837615|
            B|33|00583761|VENDA|55|1|36479|2026-02-03T10:37:51-03:00|2026-02-03T10:37:51-03:00|1|1|3302403|1|1|5|1|1|1|1|1|4.01_b029|||
            C|HOTEL EXEMPLO LTDA|HOTEL EXEMPLO|78735821||||3|
            C02|11222333000181|
            C05|AV. BEIRA MAR, 1642|1642||CENTRO|3302403|Macae                    |RJ|27920390|1058|BRASIL||
            E|AGENCIA DE VIAGENS EXEMPLO|9|135853970110||||
            E02|44555666000199|
            E05|RUA EXEMPLO|755|2 ANDAR|CONSOLACAO|3550308|Sao Paulo                |SP|01415003|1058|BRASIL||
            H|1||
            I|704|SEM GTIN|FRUTAS DA ESTACAO GRANDE|21069090|||5102|UN|1.0000|33.00|33.00|SEM GTIN|UN|1.000|33.00|||||1||||||
            M||
            N|
            N02|0|00|3|33.00|12.00|3.96||||||||
            Q|
            Q05|99|0.00|
            Q10|0.0000|0.0000|
            S|
            S05|99|0.00|
            S09|0.0000|0.0000|
            H|2||
            I|8|SEM GTIN|AGUA S/ GAS (GARRAFA 310ML)|22021000|||5102|UN|2.0000|9.00|18.00|SEM GTIN|UN|2.000|9.00|||||1||||||
            M||
            N|
            N08|0|60|18.00|0.00|0.00|0.00||||||||
            Q|
            Q05|99|0.00|
            Q10|0.0000|0.0000|
            S|
            S05|99|0.00|
            S09|0.0000|0.0000|
            W|
            W02|33.00|3.96|0.00|0.00|51.00|0.00|0.00|0.00|0.00|0.00|0.00|0.00|0.00|0.00|51.00|
            W04c|0.00|
            X|9
            YA
            YA01||99|51.00|
            Z||NH: 583761    Procon - RJ Rua da Ajuda, 05, Centro - RJ, Tel 151
            """;

        var ok = NfeTxtParser.TryParse(txt, out var note, out var errors);

        Assert.True(ok, string.Join("; ", errors));
        Assert.Equal("VENDA", note.NatOp);
        Assert.Equal(1, note.Finality);
        Assert.Equal("11222333000181", note.EmitterCnpj);
        Assert.Equal("44555666000199", note.RecipientCnpjCpf);
        Assert.Equal(9, note.RecipientIeIndicator);
        Assert.Equal("SP", note.Uf);
        Assert.Equal("3550308", note.CityCode);
        Assert.Equal(99, note.PaymentMethod);
        Assert.True(note.UsesPisCofinsOutras);
        Assert.True(note.LooksLikeTaxedSale);
        Assert.StartsWith("NH: 583761", note.AdditionalInfo);

        Assert.Equal(2, note.Items.Count);
        Assert.Equal("00", note.Items[0].IcmsCst);
        Assert.Equal(12m, note.Items[0].IcmsRate);
        Assert.Equal(33m, note.Items[0].UnitValue);
        Assert.Equal("60", note.Items[1].IcmsCst);
        Assert.Equal(2m, note.Items[1].Quantity);
        Assert.Equal("5102", note.Items[1].Cfop);
    }

    [Fact]
    public void Parse_ArquivoSemRegistros_Falha()
    {
        var ok = NfeTxtParser.TryParse("conteudo qualquer sem pipes", out _, out var errors);

        Assert.False(ok);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Parse_DuasNotas_ImportaPrimeiraEAvisa()
    {
        var txt = Sample400 + "\n" + """
            A|4.00|
            B|35|999|OUTRA NOTA|55|1|5000|2026-07-12T13:00:00-03:00||1|1|3550308|1|1|0|2|1|1|1|0|PMS||
            """;

        var ok = NfeTxtParser.TryParse(txt.Replace("NOTAFISCAL|1", "NOTAFISCAL|2"), out var note, out var errors);

        Assert.True(ok, string.Join("; ", errors));
        Assert.Equal("VENDA DE MERCADORIA", note.NatOp);
        Assert.Contains(note.Warnings, w => w.Contains("2 notas"));
    }
}
