using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Exceptions;

namespace ALMOXPRO.Domain.Entities.Fiscal;

/// <summary>
/// NF-e emitida contra o CNPJ da empresa, obtida pela Distribuição DF-e da
/// SEFAZ. Permite acompanhar as notas assim que emitidas e manifestar o
/// destinatário (ciência, confirmação, desconhecimento ou recusa).
/// </summary>
public class FiscalDocument : BaseEntity
{
    /// <summary>Chave de acesso da NF-e (44 dígitos).</summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>NSU do documento na Distribuição DF-e.</summary>
    public string Nsu { get; set; } = string.Empty;

    public string EmitterCnpj { get; set; } = string.Empty;
    public string EmitterName { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public decimal TotalValue { get; set; }

    public FiscalDocumentStatus Status { get; set; } = FiscalDocumentStatus.Recebida;

    /// <summary>True quando o XML completo (procNFe) já foi baixado; false quando só o resumo.</summary>
    public bool HasFullXml { get; set; }

    /// <summary>XML do documento (procNFe completo ou resNFe resumido).</summary>
    public string Xml { get; set; } = string.Empty;

    public DateTime? ManifestedAt { get; set; }
    public int? ManifestedByUserId { get; set; }
    public string? ManifestJustification { get; set; }

    public void RegisterManifestation(FiscalDocumentStatus newStatus, int userId, string? justification)
    {
        if (newStatus == FiscalDocumentStatus.Recebida)
            throw new DomainException("Manifestação inválida.");
        if (Status is FiscalDocumentStatus.Confirmada or FiscalDocumentStatus.Desconhecida
            or FiscalDocumentStatus.OperacaoNaoRealizada)
            throw new DomainException(
                $"A nota já possui manifestação definitiva ({Status}) e não pode ser alterada pelo sistema.");
        if (newStatus == FiscalDocumentStatus.OperacaoNaoRealizada
            && (justification is null || justification.Trim().Length < 15))
            throw new DomainException("A recusa (Operação não Realizada) exige justificativa com ao menos 15 caracteres.");

        Status = newStatus;
        ManifestedAt = DateTime.UtcNow;
        ManifestedByUserId = userId;
        ManifestJustification = justification?.Trim();
    }
}
