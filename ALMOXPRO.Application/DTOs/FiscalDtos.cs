using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Application.DTOs;

public record FiscalDocumentDto(
    int Id,
    string AccessKey,
    string EmitterCnpj,
    string EmitterName,
    DateTime IssuedAt,
    decimal TotalValue,
    FiscalDocumentStatus Status,
    bool HasFullXml,
    DateTime? ManifestedAt,
    string? ManifestJustification);

public record IssuedNfeDto(
    int Id,
    int Number,
    int Series,
    string AccessKey,
    string RecipientCnpjCpf,
    string RecipientName,
    string NatureOfOperation,
    DateTime IssuedAt,
    decimal TotalValue,
    IssuedNfeStatus Status,
    string Protocol,
    bool IsProduction,
    DateTime? CanceledAt,
    string? CancelJustification)
{
    /// <summary>Número formatado como na DANFE (ex.: "000.000.123 / 1").</summary>
    public string DisplayNumber => $"{Number:D9}"[..3] + "." + $"{Number:D9}"[3..6] + "." + $"{Number:D9}"[6..] + $" / {Series}";
}
