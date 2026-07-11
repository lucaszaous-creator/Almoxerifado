using System.Security.Cryptography;
using System.Text;

namespace ALMOXPRO.Shared.Security;

/// <summary>
/// Proteção de segredos armazenados no banco (ex.: senha do certificado A1).
/// AES com chave embutida: evita exposição em texto puro no banco/backup,
/// mas não substitui controle de acesso ao banco de dados.
/// </summary>
public static class SecretProtector
{
    private static readonly byte[] Key = SHA256.HashData(
        Encoding.UTF8.GetBytes("ALMOXPRO::secret-protector::v1"));

    public static string Protect(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plain = Encoding.UTF8.GetBytes(plainText);
        var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
        return Convert.ToBase64String(aes.IV.Concat(cipher).ToArray());
    }

    public static string Unprotect(string protectedText)
    {
        var data = Convert.FromBase64String(protectedText);
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = data[..16];
        using var decryptor = aes.CreateDecryptor();
        var plain = decryptor.TransformFinalBlock(data, 16, data.Length - 16);
        return Encoding.UTF8.GetString(plain);
    }
}
