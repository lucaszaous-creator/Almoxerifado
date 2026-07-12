using ALMOXPRO.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;
using Unimake.Business.DFe.Servicos;
using Unimake.Business.DFe.Servicos.NFe;
using Unimake.Business.DFe.Xml.NFe;

namespace ALMOXPRO.Infrastructure.Fiscal;

/// <summary>
/// Comunicação com a SEFAZ usando a biblioteca gratuita Unimake.DFe:
/// Distribuição DF-e (NFeDistribuicaoDFe), Manifestação do Destinatário
/// (RecepcaoEvento com os eventos 210200/210210/210220/210240), emissão de
/// NF-e (Autorizacao síncrona), cancelamento (evento 110111) e consulta do
/// status do serviço (StatusServico).
/// </summary>
public class UnimakeFiscalGateway : IFiscalGateway
{
    private readonly ILogger<UnimakeFiscalGateway> _logger;

    public UnimakeFiscalGateway(ILogger<UnimakeFiscalGateway> logger) => _logger = logger;

    public CertificateInfo InspectCertificate(byte[] pfxBytes, string password)
    {
        using var certificate = new X509Certificate2(pfxBytes, password,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        return ToInfo(certificate);
    }

    public IReadOnlyList<InstalledCertificate> ListInstalledCertificates()
    {
        var found = new List<InstalledCertificate>();
        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            try
            {
                using var store = new X509Store(StoreName.My, location);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                foreach (var cert in store.Certificates)
                {
                    // Só certificados utilizáveis para assinar (com chave privada).
                    if (cert.HasPrivateKey && !string.IsNullOrEmpty(cert.Thumbprint))
                        found.Add(new InstalledCertificate(cert.Thumbprint, cert.Subject, cert.NotAfter));
                }
            }
            catch (Exception)
            {
                // Repositório inexistente/sem acesso nesta localização: ignora.
            }
        }

        return found
            .GroupBy(c => c.Thumbprint)
            .Select(g => g.First())
            .OrderBy(c => c.Subject)
            .ToList();
    }

    public CertificateInfo InspectStoreCertificate(string thumbprint)
    {
        using var certificate = LoadFromStore(thumbprint);
        return ToInfo(certificate);
    }

    private static CertificateInfo ToInfo(X509Certificate2 cert) =>
        new(cert.Subject, cert.Issuer, cert.NotBefore, cert.NotAfter);

    private static X509Certificate2 LoadFromStore(string thumbprint)
    {
        var clean = thumbprint.Replace(" ", "").Trim();
        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            try
            {
                using var store = new X509Store(StoreName.My, location);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                var match = store.Certificates.Find(X509FindType.FindByThumbprint, clean, validOnly: false);
                if (match.Count > 0)
                    return match[0];
            }
            catch (Exception)
            {
                // Tenta a próxima localização.
            }
        }

        throw new InvalidOperationException(
            "Certificado não encontrado no repositório do Windows. Verifique se ainda está instalado.");
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

    public Task<NfeAuthorizationResult> AuthorizeNfeAsync(FiscalConfig config, NfeDraft draft,
        CancellationToken ct = default) => Task.Run(() =>
    {
        var enviNFe = new EnviNFe
        {
            Versao = "4.00",
            IdLote = draft.Number.ToString("D15"),
            IndSinc = SimNao.Sim,
            NFe = [BuildNfe(config, draft)]
        };

        var service = new Autorizacao(enviNFe, BuildConfiguration(config));
        service.Executar();

        var ret = service.Result;
        var prot = ret.ProtNFe?.InfProt;
        if (prot is null)
            return new NfeAuthorizationResult(false, ret.CStat, ret.XMotivo ?? string.Empty, null, null);

        _logger.LogInformation("Autorização NF-e {Chave}: cStat {CStat} ({Motivo}), protocolo {Prot}",
            draft.AccessKey, prot.CStat, prot.XMotivo, prot.NProt);

        // 100 = Autorizado o uso da NF-e.
        if (prot.CStat != 100)
            return new NfeAuthorizationResult(false, prot.CStat, prot.XMotivo ?? string.Empty, prot.NProt, null);

        var procXml = service.NfeProcResult.GerarXML().OuterXml;
        return new NfeAuthorizationResult(true, prot.CStat, prot.XMotivo ?? string.Empty, prot.NProt, procXml);
    }, ct);

    public Task<NfeCancelResult> CancelNfeAsync(FiscalConfig config, string accessKey, string protocol,
        string justification, CancellationToken ct = default) => Task.Run(() =>
    {
        var envEvento = new EnvEvento
        {
            Versao = "1.00",
            IdLote = "1",
            Evento =
            [
                new Evento
                {
                    Versao = "1.00",
                    InfEvento = new InfEvento(new DetEventoCanc
                    {
                        Versao = "1.00",
                        DescEvento = "Cancelamento",
                        NProt = protocol,
                        XJust = justification
                    })
                    {
                        COrgao = ParseUf(config.Uf),
                        ChNFe = accessKey,
                        CNPJ = config.Cnpj,
                        DhEvento = DateTime.Now,
                        TpEvento = TipoEventoNFe.Cancelamento,
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

        _logger.LogInformation("Cancelamento da NF-e {Chave}: cStat {CStat} ({Motivo})", accessKey, cStat, motivo);

        // 135 = evento registrado e vinculado; 155 = cancelamento homologado fora de prazo.
        return new NfeCancelResult(cStat is 135 or 155, cStat, motivo, evento?.NProt);
    }, ct);

    public Task<SefazServiceStatus> CheckServiceStatusAsync(FiscalConfig config,
        CancellationToken ct = default) => Task.Run(() =>
    {
        var consulta = new ConsStatServ
        {
            Versao = "4.00",
            TpAmb = config.Production ? TipoAmbiente.Producao : TipoAmbiente.Homologacao,
            CUF = ParseUf(config.Uf),
            XServ = "STATUS"
        };

        var service = new StatusServico(consulta, BuildConfiguration(config));
        service.Executar();

        var ret = service.Result;
        _logger.LogInformation("Status do serviço SEFAZ {Uf}: cStat {CStat} ({Motivo})",
            config.Uf, ret.CStat, ret.XMotivo);

        // 107 = serviço em operação.
        return new SefazServiceStatus(ret.CStat == 107, ret.CStat, ret.XMotivo ?? string.Empty);
    }, ct);

    /// <summary>
    /// Converte o draft (agnóstico de biblioteca) no modelo da Unimake.DFe.
    /// Tributação simplificada para operações de almoxarifado sem destaque de
    /// imposto: CSOSN 400 (Simples Nacional) ou CST 41 (não tributada), com
    /// PIS/COFINS sem incidência (CST 08).
    /// </summary>
    private static NFe BuildNfe(FiscalConfig config, NfeDraft draft)
    {
        var production = config.Production;
        var emitterUf = ParseUf(draft.Emitter.Uf);
        var recipientUf = ParseUf(draft.Recipient.Uf);
        var simples = draft.Emitter.Crt == 1;

        var ide = new Ide
        {
            CUF = emitterUf,
            CNF = draft.CNf,
            NatOp = draft.NatureOfOperation,
            Mod = ModeloDFe.NFe,
            Serie = draft.Series,
            NNF = draft.Number,
            DhEmi = draft.IssuedAt,
            TpNF = TipoOperacao.Saida,
            IdDest = emitterUf == recipientUf ? DestinoOperacao.OperacaoInterna : DestinoOperacao.OperacaoInterestadual,
            CMunFG = draft.Emitter.CityCode,
            TpImp = FormatoImpressaoDANFE.NormalRetrato,
            TpEmis = TipoEmissao.Normal,
            CDV = draft.CheckDigit,
            TpAmb = production ? TipoAmbiente.Producao : TipoAmbiente.Homologacao,
            FinNFe = draft.Finality == 4 ? FinalidadeNFe.Devolucao : FinalidadeNFe.Normal,
            IndFinal = SimNao.Nao,
            IndPres = IndicadorPresenca.NaoSeAplica,
            ProcEmi = ProcessoEmissao.AplicativoContribuinte,
            VerProc = "ALMOXPRO 1.0"
        };
        if (!string.IsNullOrWhiteSpace(draft.ReferencedAccessKey))
            ide.NFref = [new NFref { RefNFe = draft.ReferencedAccessKey }];

        var det = draft.Items.Select(item => new Det
        {
            NItem = item.Number,
            Prod = new Prod
            {
                CProd = item.Code,
                CEAN = "SEM GTIN",
                XProd = production
                    ? item.Description
                    // Texto exigido pela SEFAZ no item 1 em homologação.
                    : (item.Number == 1
                        ? "NOTA FISCAL EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL"
                        : item.Description),
                NCM = item.Ncm,
                CFOP = item.Cfop,
                UCom = item.Unit,
                QCom = item.Quantity,
                VUnCom = item.UnitValue,
                VProd = (double)item.Total,
                CEANTrib = "SEM GTIN",
                UTrib = item.Unit,
                QTrib = item.Quantity,
                VUnTrib = item.UnitValue,
                IndTot = SimNao.Sim
            },
            Imposto = new Imposto
            {
                ICMS = simples
                    ? new ICMS { ICMSSN102 = new ICMSSN102 { Orig = OrigemMercadoria.Nacional, CSOSN = "400" } }
                    : new ICMS { ICMS40 = new ICMS40 { Orig = OrigemMercadoria.Nacional, CST = "41" } },
                PIS = new PIS { PISNT = new PISNT { CST = "08" } },
                COFINS = new COFINS { COFINSNT = new COFINSNT { CST = "08" } }
            }
        }).ToList();

        return new NFe
        {
            InfNFeField =
                new InfNFe
                {
                    Versao = "4.00",
                    Id = $"NFe{draft.AccessKey}",
                    Ide = ide,
                    Emit = new Emit
                    {
                        CNPJ = draft.Emitter.Cnpj,
                        XNome = draft.Emitter.Name,
                        EnderEmit = new EnderEmit
                        {
                            XLgr = draft.Emitter.Street,
                            Nro = draft.Emitter.Number,
                            XBairro = draft.Emitter.District,
                            CMun = draft.Emitter.CityCode,
                            XMun = draft.Emitter.CityName,
                            UF = emitterUf,
                            CEP = draft.Emitter.Cep,
                            Fone = string.IsNullOrWhiteSpace(draft.Emitter.Phone) ? null : draft.Emitter.Phone
                        },
                        IE = string.IsNullOrWhiteSpace(draft.Emitter.Ie) ? "ISENTO" : draft.Emitter.Ie,
                        CRT = simples ? CRT.SimplesNacional : CRT.RegimeNormal
                    },
                    Dest = new Dest
                    {
                        CNPJ = draft.Recipient.CnpjCpf.Length == 14 ? draft.Recipient.CnpjCpf : null,
                        CPF = draft.Recipient.CnpjCpf.Length == 11 ? draft.Recipient.CnpjCpf : null,
                        // Texto exigido pela SEFAZ em homologação.
                        XNome = production
                            ? draft.Recipient.Name
                            : "NF-E EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL",
                        EnderDest = new EnderDest
                        {
                            XLgr = draft.Recipient.Street,
                            Nro = draft.Recipient.Number,
                            XBairro = draft.Recipient.District,
                            CMun = draft.Recipient.CityCode,
                            XMun = draft.Recipient.CityName,
                            UF = recipientUf,
                            CEP = draft.Recipient.Cep
                        },
                        IndIEDest = draft.Recipient.IeIndicator switch
                        {
                            1 => IndicadorIEDestinatario.ContribuinteICMS,
                            2 => IndicadorIEDestinatario.ContribuinteIsento,
                            _ => IndicadorIEDestinatario.NaoContribuinte
                        },
                        IE = draft.Recipient.IeIndicator == 1 ? draft.Recipient.Ie : null
                    },
                    Det = det,
                    Total = new Total
                    {
                        ICMSTot = new ICMSTot
                        {
                            VBC = 0,
                            VICMS = 0,
                            VProd = (double)draft.TotalValue,
                            VNF = (double)draft.TotalValue
                        }
                    },
                    Transp = new Transp { ModFrete = ModalidadeFrete.SemOcorrenciaTransporte },
                    Pag = new Pag
                    {
                        DetPag = [new DetPag { TPag = MeioPagamento.SemPagamento, VPag = 0 }]
                    },
                    InfAdic = string.IsNullOrWhiteSpace(draft.AdditionalInfo)
                        ? null
                        : new InfAdic { InfCpl = draft.AdditionalInfo }
                }
        };
    }

    private static Configuracao BuildConfiguration(FiscalConfig config) => new()
    {
        TipoDFe = TipoDFe.NFe,
        CertificadoDigital = LoadCertificate(config),
        CodigoUF = (int)ParseUf(config.Uf),
        TipoAmbiente = config.Production ? TipoAmbiente.Producao : TipoAmbiente.Homologacao,
        // Apresenta o certificado no handshake TLS antes do envio; alguns
        // endpoints da SEFAZ recusam (403) quando isso não é feito.
        PrepararConexaoTLSAntesDoEnvio = true
    };

    private static X509Certificate2 LoadCertificate(FiscalConfig config) =>
        !string.IsNullOrWhiteSpace(config.CertificateThumbprint)
            ? LoadFromStore(config.CertificateThumbprint!)
            // PersistKeySet (não Ephemeral): o SChannel do Windows exige a chave
            // privada em container persistido para a autenticação TLS de cliente.
            // Com EphemeralKeySet o certificado não é apresentado e a SEFAZ retorna 403.
            : new X509Certificate2(config.CertificatePfx!, config.CertificatePassword,
                X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

    private static UFBrasil ParseUf(string uf) =>
        Enum.TryParse<UFBrasil>(uf.Trim().ToUpperInvariant(), out var parsed) ? parsed : UFBrasil.SP;
}
