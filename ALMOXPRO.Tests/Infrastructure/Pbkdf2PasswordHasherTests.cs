using ALMOXPRO.Infrastructure.Security;
using Xunit;

namespace ALMOXPRO.Tests.Infrastructure;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_E_Verify_SenhaCorreta_RetornaTrue()
    {
        var hash = _hasher.Hash("S3nh@Forte!");

        Assert.True(_hasher.Verify("S3nh@Forte!", hash));
    }

    [Fact]
    public void Verify_SenhaErrada_RetornaFalse()
    {
        var hash = _hasher.Hash("S3nh@Forte!");

        Assert.False(_hasher.Verify("outra-senha", hash));
    }

    [Fact]
    public void Hash_MesmaSenha_GeraHashesDiferentes_PorCausaDoSalt()
    {
        var hash1 = _hasher.Hash("mesma-senha");
        var hash2 = _hasher.Hash("mesma-senha");

        Assert.NotEqual(hash1, hash2);
        Assert.True(_hasher.Verify("mesma-senha", hash1));
        Assert.True(_hasher.Verify("mesma-senha", hash2));
    }

    [Fact]
    public void Hash_SegueFormatoIteracoesSaltHash()
    {
        var hash = _hasher.Hash("abc");
        var parts = hash.Split('.');

        Assert.Equal(3, parts.Length);
        Assert.True(int.TryParse(parts[0], out var iterations));
        Assert.True(iterations >= 100_000);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-formato")]
    [InlineData("abc.def.ghi")]
    [InlineData("100000.salt-invalido.hash-invalido")]
    public void Verify_HashMalformado_RetornaFalse_SemLancarExcecao(string malformed)
    {
        Assert.False(_hasher.Verify("qualquer", malformed));
    }
}
