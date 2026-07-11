using ALMOXPRO.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;
using Unimake.Business.DFe.Servicos;
using Unimake.Business.DFe.Servicos.NFe;
using Unimake.Business.DFe.Xml.NFe;

namespace ALMOXPRO.Infrastructure.Fiscal;

/// <summary>
/// Comunicação com a SEFAZ usando a biblioteca gratuita Unimake.DFe:
/// Distribuição DF-e (NFeDistribuicaoDFe) e Manifestação do Destinatário
/// (RecepcaoEvento com os eventos 210200/210210/210220/210240).
/// </summary>
public class UnimakeFiscalGateway : IFiscalGateway
{
    private readonly ILogger<UnimakeFiscalGateway> _logger;

    public UnimakeFiscalGateway(ILogger<UnimakeFiscalGateway> logger) => _logger = logger;

    public CertificateInfo InspectCertificate(byte[] pfxBytes, string password)
    {
        using var certificate = new X509Certificate2(pfxBytes, password,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        return new CertificateInfo(certificate.Subject, certificate.Issuer,
            certificate.NotBefore, certificate.NotAfter);
    }

    public Task<FiscalSyncResult> FetchDocumentsAsync(FiscalConfig config, string ultNsu,
        CancellationToken ct = default) => Task.Run(() =>
    {
        var uf = ParseUf(config.Uf);
        var xml = new DistDFeInt
        {
            Versao = "1.01",
            TpAmb = config.Production ? TipoAmbiente.Producao : TipoAmbiente.Homologacao,
            CNPJ = config.Cnpj,
            CUFAutor = uf,
            DistNSU = new DistNSU { UltNSU = ultNsu.PadLeft(15, '0') }
        };

        var service = new DistribuicaoDFe(xml, BuildConfiguration(config));
        service.Executar();

        var result = service.Result;
        var documents = new List<FiscalSyncDocument>();
        if (result.LoteDistDFeInt?.DocZip is not null)
        {
            foreach (var docZip in result.LoteDistDFeInt.DocZip)
                documents.Add(new FiscalSyncDocument(docZip.NSU, docZip.Schema ?? string.Empty,
                    docZip.ConteudoXML ?? string.Empty));
        }

        _logger.LogInformation("Distribuição DF-e: cStat {CStat} ({Motivo}), {Count} documento(s), ultNSU {Ult}/{Max}",
            result.CStat, result.XMotivo, documents.Count, result.UltNSU, result.MaxNSU);

        return new FiscalSyncResult(result.CStat, result.XMotivo ?? string.Empty,
            result.UltNSU ?? ultNsu, result.MaxNSU ?? ultNsu, documents);
    }, ct);

    public Task<ManifestResult> SendManifestationAsync(FiscalConfig config, string accessKey,
        ManifestationType type, string? justification, CancellationToken ct = default) => Task.Run(() =>
    {
        var (tpEvento, descEvento) = type switch
        {
            ManifestationType.Confirmacao => (TipoEventoNFe.ManifestacaoConfirmacaoOperacao, "Confirmacao da Operacao"),
            ManifestationType.Ciencia => (TipoEventoNFe.ManifestacaoCienciaOperacao, "Ciencia da Operacao"),
            ManifestationType.Desconhecimento => (TipoEventoNFe.ManifestacaoDesconhecimentoOperacao, "Desconhecimento da Operacao"),
            _ => (TipoEventoNFe.ManifestacaoOperacaoNaoRealizada, "Operacao nao Realizada")
        };

        var envEvento = new EnvEvento
        {
            Versao = "1.00",
            IdLote = "1",
            Evento =
            [
                new Evento
                {
                    Versao = "1.00",
                    InfEvento = new InfEvento(new DetEventoManif
                    {
                        Versao = "1.00",
                        DescEvento = descEvento,
                        XJust = type == ManifestationType.OperacaoNaoRealizada ? justification : null
                    })
                    {
                        // Manifestação do destinatário é sempre endereçada ao Ambiente Nacional.
                        COrgao = UFBrasil.AN,
                        ChNFe = accessKey,
                        CNPJ = config.Cnpj,
                        DhEvento = DateTime.Now,
                        TpEvento = tpEvento,
                        NSeqEvento = 1,
                        VerEvento = "1.00",
                        TpAmb = config.Production ? TipoAmbiente.Producao : TipoAmbiente.Homologacao
                    }
                }
            ]
        };

        var service = new RecepcaoEvento(envEvento, BuildConfiguration(config));
        service.Executar();

        var lote = service.Result;
        var evento = lote.RetEvento?.FirstOrDefault()?.InfEvento;
        var cStat = evento?.CStat ?? lote.CStat;
        var motivo = evento?.XMotivo ?? lote.XMotivo ?? string.Empty;

        _logger.LogInformation("Manifestação {Tipo} para {Chave}: cStat {CStat} ({Motivo})",
            descEvento, accessKey, cStat, motivo);

        // 135 = evento registrado e vinculado; 136 = registrado sem vínculo.
        return new ManifestResult(cStat is 135 or 136, cStat, motivo);
    }, ct);

    private static Configuracao BuildConfiguration(FiscalConfig config) => new()
    {
        TipoDFe = TipoDFe.NFe,
        CertificadoDigital = new X509Certificate2(config.CertificatePfx, config.CertificatePassword,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet),
        CodigoUF = (int)ParseUf(config.Uf),
        TipoAmbiente = config.Production ? TipoAmbiente.Producao : TipoAmbiente.Homologacao
    };

    private static UFBrasil ParseUf(string uf) =>
        Enum.TryParse<UFBrasil>(uf.Trim().ToUpperInvariant(), out var parsed) ? parsed : UFBrasil.SP;
}
