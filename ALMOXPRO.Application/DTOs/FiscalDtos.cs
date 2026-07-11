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
