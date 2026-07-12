using ALMOXPRO.Application.Services;
using Xunit;

namespace ALMOXPRO.Tests.Application;

public class NfeAccessKeyTests
{
    // Exemplo público do Manual de Orientação do Contribuinte (MOC) da NF-e.
    [Fact]
    public void CheckDigit_ExemploDoManualDaNFe_CalculaDvCorreto()
    {
        const string chaveReal = "52060433009911002506550120000007800267301615";
        Assert.Equal(chaveReal[43] - '0', NfeAccessKey.CheckDigit(chaveReal[..43]));
    }

    [Theory]
    [InlineData("3526071122233300018155001000000123112345678", 6)]
    [InlineData("3125019988877700016655003000045678187654321", 9)]
    public void CheckDigit_VetoresConhecidos(string key43, int expected) =>
        Assert.Equal(expected, NfeAccessKey.CheckDigit(key43));

    [Fact]
    public void Build_MontaChaveCom44DigitosNaOrdemDoLayout()
    {
        var issuedAt = new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.FromHours(-3));

        var key = NfeAccessKey.Build("SP", issuedAt, "11222333000181", 55, 1, 123, 1, "12345678");

        Assert.Equal(44, key.Length);
        Assert.Equal("3526071122233300018155001000000123112345678" + "6", key);
    }

    [Fact]
    public void Build_UfInvalida_Lanca() =>
        Assert.Throws<ArgumentException>(() =>
            NfeAccessKey.Build("XX", DateTimeOffset.Now, "11222333000181", 55, 1, 1, 1, "12345678"));

    [Fact]
    public void CheckDigit_TamanhoErrado_Lanca() =>
        Assert.Throws<ArgumentException>(() => NfeAccessKey.CheckDigit("123"));

    [Fact]
    public void NewRandomCode_Tem8DigitosEDifereDoNumero()
    {
        for (var i = 0; i < 50; i++)
        {
            var code = NfeAccessKey.NewRandomCode(1234);
            Assert.Equal(8, code.Length);
            Assert.True(code.All(char.IsAsciiDigit));
            Assert.NotEqual(1234, int.Parse(code));
        }
    }
}
