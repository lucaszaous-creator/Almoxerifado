using System.Security.Cryptography;

namespace ALMOXPRO.Application.Services;

/// <summary>
/// Composição da chave de acesso da NF-e (44 dígitos):
/// cUF(2) AAMM(4) CNPJ(14) mod(2) série(3) nNF(9) tpEmis(1) cNF(8) cDV(1).
/// </summary>
public static class NfeAccessKey
{
    /// <summary>Código IBGE de cada UF, usado no início da chave.</summary>
    public static readonly IReadOnlyDictionary<string, int> UfCodes = new Dictionary<string, int>
    {
        ["RO"] = 11, ["AC"] = 12, ["AM"] = 13, ["RR"] = 14, ["PA"] = 15, ["AP"] = 16, ["TO"] = 17,
        ["MA"] = 21, ["PI"] = 22, ["CE"] = 23, ["RN"] = 24, ["PB"] = 25, ["PE"] = 26, ["AL"] = 27,
        ["SE"] = 28, ["BA"] = 29,
        ["MG"] = 31, ["ES"] = 32, ["RJ"] = 33, ["SP"] = 35,
        ["PR"] = 41, ["SC"] = 42, ["RS"] = 43,
        ["MS"] = 50, ["MT"] = 51, ["GO"] = 52, ["DF"] = 53
    };

    public static string Build(string uf, DateTimeOffset issuedAt, string cnpj, int model,
        int series, int number, int tpEmis, string cnf)
    {
        if (!UfCodes.TryGetValue(uf.Trim().ToUpperInvariant(), out var ufCode))
            throw new ArgumentException($"UF desconhecida: {uf}", nameof(uf));
        if (cnpj.Length != 14)
            throw new ArgumentException("O CNPJ do emitente deve ter 14 dígitos.", nameof(cnpj));
        if (cnf.Length != 8)
            throw new ArgumentException("O código numérico (cNF) deve ter 8 dígitos.", nameof(cnf));

        var key43 = $"{ufCode:D2}{issuedAt:yyMM}{cnpj}{model:D2}{series:D3}{number:D9}{tpEmis}{cnf}";
        return key43 + CheckDigit(key43);
    }

    /// <summary>Dígito verificador módulo 11 com pesos 2 a 9 da direita para a esquerda.</summary>
    public static int CheckDigit(string key43)
    {
        if (key43.Length != 43 || key43.Any(c => !char.IsAsciiDigit(c)))
            throw new ArgumentException("A chave sem DV deve ter 43 dígitos numéricos.", nameof(key43));

        var sum = 0;
        var weight = 2;
        for (var i = key43.Length - 1; i >= 0; i--)
        {
            sum += (key43[i] - '0') * weight;
            weight = weight == 9 ? 2 : weight + 1;
        }

        var remainder = sum % 11;
        return remainder <= 1 ? 0 : 11 - remainder;
    }

    /// <summary>Código numérico aleatório (cNF), sempre diferente do nNF (NT 2019.001).</summary>
    public static string NewRandomCode(int number)
    {
        while (true)
        {
            var code = RandomNumberGenerator.GetInt32(0, 100_000_000);
            if (code != number)
                return code.ToString("D8");
        }
    }
}
