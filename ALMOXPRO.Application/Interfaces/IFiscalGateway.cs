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

/// <summary>Dados do emitente para a emissão de NF-e própria.</summary>
public record NfeEmitter(
    string Cnpj,
    string Name,
    string? Ie,
    /// <summary>CRT: 1 = Simples Nacional, 3 = Regime Normal.</summary>
    int Crt,
    string Street,
    string Number,
    string District,
    int CityCode,
    string CityName,
    string Uf,
    string Cep,
    string? Phone);

/// <summary>Destinatário da NF-e emitida.</summary>
public record NfeRecipient(
    /// <summary>CNPJ (14 dígitos) ou CPF (11 dígitos), só dígitos.</summary>
    string CnpjCpf,
    string Name,
    /// <summary>indIEDest: 1 = contribuinte ICMS, 2 = isento, 9 = não contribuinte.</summary>
    int IeIndicator,
    string? Ie,
    string Street,
    string Number,
    string District,
    int CityCode,
    string CityName,
    string Uf,
    string Cep);

public record NfeDraftItem(
    int Number,
    string Code,
    string Description,
    string Ncm,
    string Cfop,
    string Unit,
    decimal Quantity,
    decimal UnitValue,
    decimal Total);

/// <summary>
/// NF-e pronta para envio, independente de biblioteca. A chave de acesso, o
/// código numérico (cNF) e o dígito verificador já vêm calculados pelo serviço.
/// </summary>
public record NfeDraft(
    string AccessKey,
    string CNf,
    int CheckDigit,
    int Number,
    int Series,
    DateTimeOffset IssuedAt,
    string NatureOfOperation,
    /// <summary>finNFe: 1 = normal, 4 = devolução (exige chave referenciada).</summary>
    int Finality,
    /// <summary>Chave da NF-e referenciada (obrigatória na devolução).</summary>
    string? ReferencedAccessKey,
    NfeEmitter Emitter,
    NfeRecipient Recipient,
    IReadOnlyList<NfeDraftItem> Items,
    decimal TotalValue,
    string? AdditionalInfo);

public record NfeAuthorizationResult(
    bool Success,
    int StatusCode,
    string Message,
    string? Protocol,
    /// <summary>XML procNFe (nota + protocolo) quando autorizada.</summary>
    string? ProcNFeXml);

public record NfeCancelResult(bool Success, int StatusCode, string Message, string? Protocol);

public record SefazServiceStatus(bool Online, int StatusCode, string Message);

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

    /// <summary>Monta, assina e envia a NF-e à SEFAZ (autorização síncrona).</summary>
    Task<NfeAuthorizationResult> AuthorizeNfeAsync(FiscalConfig config, NfeDraft draft,
        CancellationToken ct = default);

    /// <summary>Envia o evento de cancelamento (110111) de uma NF-e autorizada.</summary>
    Task<NfeCancelResult> CancelNfeAsync(FiscalConfig config, string accessKey, string protocol,
        string justification, CancellationToken ct = default);

    /// <summary>Consulta o status do serviço de autorização da SEFAZ da UF configurada.</summary>
    Task<SefazServiceStatus> CheckServiceStatusAsync(FiscalConfig config, CancellationToken ct = default);
}

/// <summary>Geração do DANFE (visualização/impressão) a partir do XML procNFe.</summary>
public interface IDanfeGenerator
{
    byte[] GeneratePdf(string procNFeXml);
}
