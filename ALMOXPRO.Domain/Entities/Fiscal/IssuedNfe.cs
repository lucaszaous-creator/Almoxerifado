using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Exceptions;

namespace ALMOXPRO.Domain.Entities.Fiscal;

/// <summary>
/// NF-e (modelo 55) emitida pela própria empresa — saídas do almoxarifado como
/// remessas, transferências e devoluções a fornecedor. Guarda o XML procNFe
/// autorizado pela SEFAZ (ou simulado, no modo demonstração).
/// </summary>
public class IssuedNfe : BaseEntity
{
    /// <summary>Chave de acesso da NF-e (44 dígitos).</summary>
    public string AccessKey { get; set; } = string.Empty;

    public int Number { get; set; }
    public int Series { get; set; }

    /// <summary>Natureza da operação (ex.: "Devolução de mercadoria").</summary>
    public string NatureOfOperation { get; set; } = string.Empty;

    /// <summary>CNPJ (14) ou CPF (11) do destinatário, só dígitos.</summary>
    public string RecipientCnpjCpf { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; }
    public decimal TotalValue { get; set; }

    public IssuedNfeStatus Status { get; set; } = IssuedNfeStatus.Autorizada;

    /// <summary>Protocolo de autorização retornado pela SEFAZ.</summary>
    public string Protocol { get; set; } = string.Empty;

    /// <summary>XML procNFe autorizado (nota + protocolo).</summary>
    public string Xml { get; set; } = string.Empty;

    /// <summary>True quando autorizada em produção; false em homologação/demonstração.</summary>
    public bool IsProduction { get; set; }

    public int? IssuedByUserId { get; set; }

    public DateTime? CanceledAt { get; set; }
    public string? CancelProtocol { get; set; }
    public string? CancelJustification { get; set; }

    public void RegisterCancellation(string protocol, string justification, int userId)
    {
        if (Status == IssuedNfeStatus.Cancelada)
            throw new DomainException("A nota já está cancelada.");
        if (justification.Trim().Length < 15)
            throw new DomainException("O cancelamento exige justificativa com ao menos 15 caracteres.");

        Status = IssuedNfeStatus.Cancelada;
        CanceledAt = DateTime.UtcNow;
        CancelProtocol = protocol;
        CancelJustification = justification.Trim();
        UpdatedBy = userId.ToString();
    }
}
