namespace ALMOXPRO.Application.Interfaces;

/// <summary>
/// Configuração fiscal usada nas chamadas à SEFAZ. O certificado vem do
/// arquivo A1 enviado (CertificatePfx) OU do repositório do Windows
/// (CertificateThumbprint) — exatamente um dos dois é preenchido.
/// </summary>
public record FiscalConfig(
    byte[]? CertificatePfx,
    string? CertificatePassword,
    string? CertificateThumbprint,
    string Cnpj,
    string Uf,
    bool Production);

/// <summary>Dados que a tela de configuração envia ao gravar a config fiscal.</summary>
public record FiscalConfigInput(
    bool UseWindowsStore,
    string? Thumbprint,
    byte[]? PfxBytes,
    string? PfxPassword,
    string Cnpj,
    string Uf,
    bool Production);

/// <summary>Certificado disponível no repositório do Windows.</summary>
public record InstalledCertificate(string Thumbprint, string Subject, DateTime NotAfter)
{
    /// <summary>Nome amigável extraído do campo CN do titular.</summary>
    public string DisplayName
    {
        get
        {
            var cn = Subject.Split(',').FirstOrDefault(p => p.TrimStart().StartsWith("CN=", StringComparison.OrdinalIgnoreCase));
            var name = cn?.Trim()[3..] ?? Subject;
            return $"{name}  (válido até {NotAfter:dd/MM/yyyy})";
        }
    }
}

/// <summary>Documento retornado pela Distribuição DF-e (XML bruto por NSU).</summary>
public record FiscalSyncDocument(string Nsu, string Schema, string Xml);

/// <summary>Resultado de uma consulta à Distribuição DF-e.</summary>
public record FiscalSyncResult(
    int StatusCode,
    string StatusMessage,
    string UltNsu,
    string MaxNsu,
    IReadOnlyList<FiscalSyncDocument> Documents);

/// <summary>Eventos de manifestação do destinatário.</summary>
public enum ManifestationType
{
    /// <summary>Ciência da Operação (libera o XML completo).</summary>
    Ciencia = 210210,
    /// <summary>Confirmação da Operação (mercadoria recebida ok).</summary>
    Confirmacao = 210200,
    /// <summary>Desconhecimento da Operação (nota não reconhecida).</summary>
    Desconhecimento = 210220,
    /// <summary>Operação não Realizada — a "recusa" (exige justificativa).</summary>
    OperacaoNaoRealizada = 210240
}

public record ManifestResult(bool Success, int StatusCode, string Message);

public record CertificateInfo(string Subject, string Issuer, DateTime NotBefore, DateTime NotAfter);

/// <summary>Comunicação com a SEFAZ: Distribuição DF-e e Manifestação do Destinatário.</summary>
public interface IFiscalGateway
{
    /// <summary>Busca documentos destinados ao CNPJ a partir do último NSU processado.</summary>
    Task<FiscalSyncResult> FetchDocumentsAsync(FiscalConfig config, string ultNsu, CancellationToken ct = default);

    /// <summary>Envia um evento de manifestação do destinatário para a chave informada.</summary>
    Task<ManifestResult> SendManifestationAsync(FiscalConfig config, string accessKey,
        ManifestationType type, string? justification, CancellationToken ct = default);

    /// <summary>Valida o certificado A1 (.pfx) e retorna titular e validade.</summary>
    CertificateInfo InspectCertificate(byte[] pfxBytes, string password);

    /// <summary>Lista os certificados com chave privada instalados no repositório do Windows.</summary>
    IReadOnlyList<InstalledCertificate> ListInstalledCertificates();

    /// <summary>Valida um certificado instalado no Windows pelo thumbprint.</summary>
    CertificateInfo InspectStoreCertificate(string thumbprint);
}

/// <summary>Geração do DANFE (visualização/impressão) a partir do XML procNFe.</summary>
public interface IDanfeGenerator
{
    byte[] GeneratePdf(string procNFeXml);
}
