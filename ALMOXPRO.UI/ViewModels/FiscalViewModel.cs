using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Security;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace ALMOXPRO.UI.ViewModels;

/// <summary>Notas fiscais emitidas contra o CNPJ da empresa (Distribuição DF-e + manifestação).</summary>
public partial class FiscalViewModel : ViewModelBase
{
    private readonly ISessionService _session;

    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private string _statusFilter = "Todas";

    [ObservableProperty]
    private FiscalDocumentDto? _selected;

    [ObservableProperty]
    private bool _isRefuseOpen;

    [ObservableProperty]
    private string _refuseJustification = string.Empty;

    public ObservableCollection<FiscalDocumentDto> Items { get; } = [];
    public string[] StatusFilters { get; } = ["Todas", "Recebida", "Ciência", "Confirmada", "Desconhecida", "Recusada"];

    public bool CanManifest => _session.HasPermission(PermissionCodes.FiscalManifest);
    public bool CanEmit => _session.HasPermission(PermissionCodes.FiscalEmit);

    // ===== NF-e emitidas pela empresa =====
    [ObservableProperty]
    private int _issuedPage = 1;

    [ObservableProperty]
    private int _issuedTotalPages;

    [ObservableProperty]
    private string _issuedSearch = string.Empty;

    [ObservableProperty]
    private IssuedNfeDto? _selectedIssued;

    public ObservableCollection<IssuedNfeDto> IssuedItems { get; } = [];

    // Formulário de emissão
    public const string OperationSemImposto = "Remessa / transferência / devolução (sem imposto)";
    public const string OperationVenda = "Venda tributada (restaurante / balcão)";

    [ObservableProperty]
    private bool _isIssueOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTaxedSaleSelected))]
    private string _issueOperationKind = OperationSemImposto;

    public string[] OperationKinds { get; } = [OperationSemImposto, OperationVenda];

    public bool IsTaxedSaleSelected => IssueOperationKind == OperationVenda;

    [ObservableProperty]
    private string _issuePaymentMethod = "Dinheiro";

    public string[] PaymentMethods { get; } = ["Dinheiro", "PIX", "Cartão de crédito", "Cartão de débito", "Boleto", "Outros"];

    public const string PisAliquota = "Por alíquota (CST 01)";
    public const string PisOutras = "Outras operações (CST 99, valores zerados)";

    [ObservableProperty]
    private string _issuePisCofinsMode = PisAliquota;

    public string[] PisCofinsModes { get; } = [PisAliquota, PisOutras];

    [ObservableProperty]
    private string _issueNatOp = "Remessa de material";

    [ObservableProperty]
    private bool _issueIsDevolution;

    [ObservableProperty]
    private string _issueReferencedKey = string.Empty;

    [ObservableProperty]
    private string _issueRecipientDoc = string.Empty;

    [ObservableProperty]
    private string _issueRecipientName = string.Empty;

    [ObservableProperty]
    private string _issueRecipientIeOption = "Não contribuinte";

    [ObservableProperty]
    private string _issueRecipientIe = string.Empty;

    [ObservableProperty]
    private string _issueStreet = string.Empty;

    [ObservableProperty]
    private string _issueNumber = string.Empty;

    [ObservableProperty]
    private string _issueDistrict = string.Empty;

    [ObservableProperty]
    private string _issueCityCode = string.Empty;

    [ObservableProperty]
    private string _issueCityName = string.Empty;

    [ObservableProperty]
    private string _issueUf = "SP";

    [ObservableProperty]
    private string _issueCep = string.Empty;

    [ObservableProperty]
    private string _issueAdditionalInfo = string.Empty;

    public ObservableCollection<IssueNfeItemRow> IssueItems { get; } = [];

    public string[] IeOptions { get; } = ["Contribuinte ICMS", "Contribuinte isento", "Não contribuinte"];

    public string[] Ufs { get; } =
    [
        "AC","AL","AP","AM","BA","CE","DF","ES","GO","MA","MT","MS","MG","PA",
        "PB","PR","PE","PI","RJ","RN","RS","RO","RR","SC","SP","SE","TO"
    ];

    // Painel de cancelamento da NF-e emitida
    [ObservableProperty]
    private bool _isCancelIssuedOpen;

    [ObservableProperty]
    private string _cancelIssuedJustification = string.Empty;

    // Filtros de consulta avançada (estilo NFeMail)
    [ObservableProperty]
    private DateTime? _filterFrom;

    [ObservableProperty]
    private DateTime? _filterTo;

    [ObservableProperty]
    private string _filterNumber = string.Empty;

    [ObservableProperty]
    private DateTime? _issuedFilterFrom;

    [ObservableProperty]
    private DateTime? _issuedFilterTo;

    [ObservableProperty]
    private string _issuedFilterNumber = string.Empty;

    // Painel de resumo (cards + gráfico dos últimos 5 dias)
    [ObservableProperty]
    private int _summaryTotal;

    [ObservableProperty]
    private int _summaryReceivedMonth;

    [ObservableProperty]
    private int _summaryIssuedMonth;

    [ObservableProperty]
    private decimal _summaryIssuedMonthValue;

    public ObservableCollection<FiscalDaySummaryRow> SummaryDays { get; } = [];

    public override string Title => "Notas Fiscais";

    public FiscalViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog, ISessionService session)
        : base(scopeFactory, dialog)
    {
        _session = session;
    }

    public override Task LoadAsync() => RunAsync(async services =>
    {
        await LoadIntoAsync(services);
        await LoadIssuedIntoAsync(services);
        await LoadSummaryAsync(services);
    });

    [RelayCommand]
    private Task SearchDocumentsAsync() => RunAsync(LoadIntoAsync);

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (Page < TotalPages) { Page++; await SearchDocumentsAsync(); }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (Page > 1) { Page--; await SearchDocumentsAsync(); }
    }

    [RelayCommand]
    private Task SyncAsync() => RunAsync(async services =>
    {
        var fiscal = services.GetRequiredService<IFiscalService>();
        var result = await fiscal.SyncAsync();
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }

        Dialog.Notify(result.Value.NewDocuments > 0
            ? $"{result.Value.NewDocuments} nota(s) nova(s) recebida(s) da SEFAZ."
            : "Nenhuma nota nova na SEFAZ.");
        await LoadIntoAsync(services);
    });

    [RelayCommand]
    private Task OpenDanfeAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;

        var fiscal = services.GetRequiredService<IFiscalService>();
        var result = await fiscal.GetDanfePdfAsync(Selected.Id);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), "ALMOXPRO", "Danfe");
        Directory.CreateDirectory(path);
        var file = Path.Combine(path, $"danfe_{Selected.AccessKey}.pdf");
        await File.WriteAllBytesAsync(file, result.Value);
        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
    });

    /// <summary>Salva o XML da nota recebida (única saída para NFC-e, que não tem DANFE).</summary>
    [RelayCommand]
    private Task SaveReceivedXmlAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;

        var fiscal = services.GetRequiredService<IFiscalService>();
        var result = await fiscal.GetXmlAsync(Selected.Id);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }

        var target = Dialog.SaveFile($"{Selected.AccessKey}-NFe.xml", "XML da NF-e (*.xml)|*.xml");
        if (target is null)
            return;
        await File.WriteAllTextAsync(target, result.Value);
        Dialog.Notify("XML salvo.");
    });

    [RelayCommand]
    private Task CienciaAsync() => ManifestAsync(ManifestationType.Ciencia,
        "Registrar CIÊNCIA da operação?\nA SEFAZ libera o download do XML completo após a ciência.");

    [RelayCommand]
    private Task ConfirmOperationAsync() => ManifestAsync(ManifestationType.Confirmacao,
        "CONFIRMAR a operação?\nUse quando a mercadoria foi recebida corretamente. Esta manifestação é definitiva.");

    [RelayCommand]
    private Task DesconhecerAsync() => ManifestAsync(ManifestationType.Desconhecimento,
        "Registrar DESCONHECIMENTO da operação?\nUse quando a empresa não reconhece esta compra. Esta manifestação é definitiva.");

    private Task ManifestAsync(ManifestationType type, string confirmMessage) => RunAsync(async services =>
    {
        if (Selected is null)
            return;
        if (!Dialog.Confirm($"{confirmMessage}\n\nNota: {Selected.EmitterName}\nChave: {Selected.AccessKey}",
                "Manifestação do destinatário"))
            return;

        var fiscal = services.GetRequiredService<IFiscalService>();
        var result = await fiscal.ManifestAsync(Selected.Id, type, null);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }

        Dialog.Notify("Manifestação registrada na SEFAZ.");
        await LoadIntoAsync(services);
    });

    [RelayCommand]
    private void OpenRefuse()
    {
        if (Selected is null)
            return;
        RefuseJustification = string.Empty;
        IsRefuseOpen = true;
    }

    [RelayCommand]
    private void CloseRefuse() => IsRefuseOpen = false;

    [RelayCommand]
    private Task ConfirmRefuseAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;
        if (RefuseJustification.Trim().Length < 15)
        {
            Dialog.ShowError("A justificativa da recusa deve ter ao menos 15 caracteres.");
            return;
        }
        if (!Dialog.Confirm(
                $"RECUSAR a nota (Operação não Realizada)?\n\nNota: {Selected.EmitterName}\n" +
                "Esta manifestação é definitiva e fica registrada na SEFAZ.",
                "Recusar nota fiscal"))
            return;

        var fiscal = services.GetRequiredService<IFiscalService>();
        var result = await fiscal.ManifestAsync(Selected.Id,
            ManifestationType.OperacaoNaoRealizada, RefuseJustification.Trim());
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }

        Dialog.Notify("Recusa (Operação não Realizada) registrada na SEFAZ.");
        IsRefuseOpen = false;
        await LoadIntoAsync(services);
    });

    // ===== Comandos das NF-e emitidas =====

    [RelayCommand]
    private Task SearchIssuedAsync() => RunAsync(LoadIssuedIntoAsync);

    [RelayCommand]
    private async Task IssuedNextPageAsync()
    {
        if (IssuedPage < IssuedTotalPages) { IssuedPage++; await SearchIssuedAsync(); }
    }

    [RelayCommand]
    private async Task IssuedPreviousPageAsync()
    {
        if (IssuedPage > 1) { IssuedPage--; await SearchIssuedAsync(); }
    }

    [RelayCommand]
    private void OpenIssueForm()
    {
        if (IssueItems.Count == 0)
            IssueItems.Add(new IssueNfeItemRow());
        IsIssueOpen = true;
    }

    [RelayCommand]
    private void CloseIssueForm() => IsIssueOpen = false;

    /// <summary>
    /// Importa o TXT gerado pelo PMS (leiaute do emissor gratuito/UniNFe) e
    /// preenche o formulário para revisão — nada é enviado sem conferência.
    /// </summary>
    [RelayCommand]
    private Task ImportTxtAsync() => RunAsync(async services =>
    {
        var path = Dialog.OpenFile("Arquivo TXT da NF-e (*.txt)|*.txt|Todos os arquivos (*.*)|*.*");
        if (path is null)
            return;

        // PMS antigos exportam em ANSI (Latin-1); tenta UTF-8 e recua se houver
        // caracteres inválidos (U+FFFD).
        var content = await File.ReadAllTextAsync(path, System.Text.Encoding.UTF8);
        if (content.Contains('�'))
            content = await File.ReadAllTextAsync(path, System.Text.Encoding.Latin1);

        if (!NfeTxtParser.TryParse(content, out var note, out var errors))
        {
            Dialog.ShowError("Não foi possível importar o TXT:\n\n" + string.Join("\n", errors));
            return;
        }

        var warnings = new List<string>(note.Warnings);
        var settings = services.GetRequiredService<ISettingsService>();
        var configuredCnpj = await settings.GetAsync(Domain.Entities.Configuration.SettingKeys.FiscalCnpj);
        if (!string.IsNullOrEmpty(note.EmitterCnpj) && !string.IsNullOrEmpty(configuredCnpj)
            && note.EmitterCnpj != configuredCnpj)
            warnings.Add($"O CNPJ emitente do arquivo ({note.EmitterCnpj}) difere do configurado ({configuredCnpj}) — a nota sairá pelo CNPJ configurado.");
        warnings.Add("A numeração/série será a do ALMOX PRO, não a do PMS.");

        IssueOperationKind = note.LooksLikeTaxedSale ? OperationVenda : OperationSemImposto;
        IssueNatOp = string.IsNullOrWhiteSpace(note.NatOp) ? IssueNatOp : note.NatOp;
        IssueIsDevolution = note.Finality == 4 && !note.LooksLikeTaxedSale;
        IssueReferencedKey = note.ReferencedKey ?? string.Empty;
        IssueRecipientDoc = note.RecipientCnpjCpf;
        IssueRecipientName = note.RecipientName;
        IssueRecipientIeOption = note.RecipientIeIndicator switch
        {
            1 => "Contribuinte ICMS",
            2 => "Contribuinte isento",
            _ => "Não contribuinte"
        };
        IssueRecipientIe = note.RecipientIe ?? string.Empty;
        IssueStreet = note.Street;
        IssueNumber = note.Number;
        IssueDistrict = note.District;
        IssueCityCode = note.CityCode;
        IssueCityName = note.CityName;
        IssueUf = string.IsNullOrWhiteSpace(note.Uf) ? IssueUf : note.Uf.ToUpperInvariant();
        IssueCep = note.Cep;
        IssuePaymentMethod = note.PaymentMethod switch
        {
            17 => "PIX",
            3 => "Cartão de crédito",
            4 => "Cartão de débito",
            15 => "Boleto",
            null or 1 => "Dinheiro",
            _ => "Outros"
        };
        IssuePisCofinsMode = note.UsesPisCofinsOutras ? PisOutras : PisAliquota;
        IssueAdditionalInfo = note.AdditionalInfo ?? string.Empty;

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        IssueItems.Clear();
        foreach (var item in note.Items)
            IssueItems.Add(new IssueNfeItemRow
            {
                Code = item.Code,
                Description = item.Description,
                Ncm = item.Ncm,
                Cfop = item.Cfop,
                Unit = item.Unit,
                Quantity = item.Quantity.ToString("0.####", inv),
                UnitValue = item.UnitValue.ToString("0.####", inv),
                IcmsCst = item.IcmsCst ?? string.Empty,
                IcmsRate = item.IcmsRate?.ToString("0.##", inv) ?? string.Empty,
                IcmsBaseReduction = item.IcmsBaseReductionPct?.ToString("0.##", inv) ?? string.Empty
            });

        IsIssueOpen = true;
        Dialog.ShowInfo($"TXT importado: {note.Items.Count} item(ns) para {note.RecipientName}.\n\n"
            + "Revise os dados (em especial CST e alíquotas) e clique em EMITIR.\n\n"
            + string.Join("\n", warnings.Select(w => "• " + w)));
    });

    [RelayCommand]
    private void AddIssueItem() => IssueItems.Add(new IssueNfeItemRow());

    [RelayCommand]
    private void RemoveIssueItem(IssueNfeItemRow? row)
    {
        if (row is not null)
            IssueItems.Remove(row);
    }

    [RelayCommand]
    private Task ConfirmIssueAsync() => RunAsync(async services =>
    {
        var items = new List<IssueNfeItemInput>();
        foreach (var row in IssueItems)
        {
            if (!row.TryBuild(out var item, out var error))
            {
                Dialog.ShowError(error);
                return;
            }
            items.Add(item);
        }

        if (!Dialog.Confirm(
                $"Emitir a NF-e para {IssueRecipientName}?\n\n{items.Count} item(ns), " +
                $"total {items.Sum(i => i.Quantity * i.UnitValue):C2}.\n" +
                "A nota será assinada e enviada à SEFAZ.", "Emitir NF-e"))
            return;

        var input = BuildIssueInput(items);

        var emission = services.GetRequiredService<IFiscalEmissionService>();
        var result = await emission.IssueAsync(input);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }

        Dialog.Notify($"NF-e {result.Value.DisplayNumber} autorizada (protocolo {result.Value.Protocol}).");
        IsIssueOpen = false;
        ClearIssueForm();
        await LoadIssuedIntoAsync(services);
    });

    [RelayCommand]
    private Task OpenIssuedDanfeAsync() => RunAsync(async services =>
    {
        if (SelectedIssued is null)
            return;

        var emission = services.GetRequiredService<IFiscalEmissionService>();
        var result = await emission.GetDanfePdfAsync(SelectedIssued.Id);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), "ALMOXPRO", "Danfe");
        Directory.CreateDirectory(path);
        var file = Path.Combine(path, $"danfe_{SelectedIssued.AccessKey}.pdf");
        await File.WriteAllBytesAsync(file, result.Value);
        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
    });

    [RelayCommand]
    private Task SaveIssuedXmlAsync() => RunAsync(async services =>
    {
        if (SelectedIssued is null)
            return;

        var emission = services.GetRequiredService<IFiscalEmissionService>();
        var result = await emission.GetXmlAsync(SelectedIssued.Id);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }

        var target = Dialog.SaveFile($"{SelectedIssued.AccessKey}-procNFe.xml", "XML da NF-e (*.xml)|*.xml");
        if (target is null)
            return;
        await File.WriteAllTextAsync(target, result.Value);
        Dialog.Notify("XML salvo.");
    });

    [RelayCommand]
    private void OpenCancelIssued()
    {
        if (SelectedIssued is null)
            return;
        CancelIssuedJustification = string.Empty;
        IsCancelIssuedOpen = true;
    }

    [RelayCommand]
    private void CloseCancelIssued() => IsCancelIssuedOpen = false;

    [RelayCommand]
    private Task ConfirmCancelIssuedAsync() => RunAsync(async services =>
    {
        if (SelectedIssued is null)
            return;
        if (CancelIssuedJustification.Trim().Length < 15)
        {
            Dialog.ShowError("A justificativa do cancelamento deve ter ao menos 15 caracteres.");
            return;
        }
        if (!Dialog.Confirm(
                $"CANCELAR a NF-e {SelectedIssued.DisplayNumber}?\n\n" +
                "O evento de cancelamento é enviado à SEFAZ e é definitivo.",
                "Cancelar NF-e emitida"))
            return;

        var emission = services.GetRequiredService<IFiscalEmissionService>();
        var result = await emission.CancelAsync(SelectedIssued.Id, CancelIssuedJustification.Trim());
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }

        Dialog.Notify("Cancelamento registrado na SEFAZ.");
        IsCancelIssuedOpen = false;
        await LoadIssuedIntoAsync(services);
    });

    private void ClearIssueForm()
    {
        IssueOperationKind = OperationSemImposto;
        IssuePaymentMethod = "Dinheiro";
        IssuePisCofinsMode = PisAliquota;
        IssueNatOp = "Remessa de material";
        IssueIsDevolution = false;
        IssueReferencedKey = string.Empty;
        IssueRecipientDoc = string.Empty;
        IssueRecipientName = string.Empty;
        IssueRecipientIeOption = "Não contribuinte";
        IssueRecipientIe = string.Empty;
        IssueStreet = string.Empty;
        IssueNumber = string.Empty;
        IssueDistrict = string.Empty;
        IssueCityCode = string.Empty;
        IssueCityName = string.Empty;
        IssueUf = "SP";
        IssueCep = string.Empty;
        IssueAdditionalInfo = string.Empty;
        IssueItems.Clear();
    }

    private IssueNfeInput BuildIssueInput(List<IssueNfeItemInput> items) => new(
        NatureOfOperation: IssueNatOp,
        IsTaxedSale: IsTaxedSaleSelected,
        PaymentMethod: IssuePaymentMethod switch
        {
            "PIX" => 17,
            "Cartão de crédito" => 3,
            "Cartão de débito" => 4,
            "Boleto" => 15,
            "Outros" => 99,
            _ => 1
        },
        IsDevolution: IssueIsDevolution,
        ReferencedAccessKey: IssueIsDevolution ? IssueReferencedKey : null,
        RecipientCnpjCpf: IssueRecipientDoc,
        RecipientName: IssueRecipientName,
        RecipientIeIndicator: IssueRecipientIeOption switch
        {
            "Contribuinte ICMS" => 1,
            "Contribuinte isento" => 2,
            _ => 9
        },
        RecipientIe: IssueRecipientIe,
        RecipientStreet: IssueStreet,
        RecipientNumber: IssueNumber,
        RecipientDistrict: IssueDistrict,
        RecipientCityCode: IssueCityCode,
        RecipientCityName: IssueCityName,
        RecipientUf: IssueUf,
        RecipientCep: IssueCep,
        AdditionalInfo: IssueAdditionalInfo,
        PisCofinsOutras: IssuePisCofinsMode == PisOutras,
        Items: items);

    /// <summary>DANFE de conferência do rascunho — nada é enviado à SEFAZ.</summary>
    [RelayCommand]
    private Task PreviewIssueDanfeAsync() => RunAsync(async services =>
    {
        var items = new List<IssueNfeItemInput>();
        foreach (var row in IssueItems)
        {
            if (!row.TryBuild(out var item, out var error))
            {
                Dialog.ShowError(error);
                return;
            }
            items.Add(item);
        }

        var emission = services.GetRequiredService<IFiscalEmissionService>();
        var result = await emission.PreviewDanfePdfAsync(BuildIssueInput(items));
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), "ALMOXPRO", "Danfe");
        Directory.CreateDirectory(path);
        var file = Path.Combine(path, $"preview_{DateTime.Now:yyyyMMddHHmmss}.pdf");
        await File.WriteAllBytesAsync(file, result.Value);
        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
    });

    [RelayCommand]
    private Task ExportReceivedXmlZipAsync() => RunAsync(async services =>
    {
        var fiscal = services.GetRequiredService<IFiscalService>();
        var result = await fiscal.ExportXmlZipAsync(CurrentStatusFilter(), FilterFrom, FilterTo,
            ParseNumber(FilterNumber), Search);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }

        var target = Dialog.SaveFile($"notas-recebidas-{DateTime.Now:yyyyMMdd}.zip", "Arquivo ZIP (*.zip)|*.zip");
        if (target is null)
            return;
        await File.WriteAllBytesAsync(target, result.Value);
        Dialog.Notify("XMLs exportados.");
    });

    [RelayCommand]
    private Task ExportIssuedXmlZipAsync() => RunAsync(async services =>
    {
        var emission = services.GetRequiredService<IFiscalEmissionService>();
        var result = await emission.ExportXmlZipAsync(IssuedFilterFrom, IssuedFilterTo,
            ParseNumber(IssuedFilterNumber), IssuedSearch);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }

        var target = Dialog.SaveFile($"notas-emitidas-{DateTime.Now:yyyyMMdd}.zip", "Arquivo ZIP (*.zip)|*.zip");
        if (target is null)
            return;
        await File.WriteAllBytesAsync(target, result.Value);
        Dialog.Notify("XMLs exportados.");
    });

    private async Task LoadSummaryAsync(IServiceProvider services)
    {
        var fiscal = services.GetRequiredService<IFiscalService>();
        var summary = await fiscal.GetSummaryAsync();

        SummaryTotal = summary.TotalStored;
        SummaryReceivedMonth = summary.ReceivedThisMonth;
        SummaryIssuedMonth = summary.IssuedThisMonth;
        SummaryIssuedMonthValue = summary.IssuedThisMonthValue;

        // Barras normalizadas para a altura máxima do mini gráfico (72px).
        var max = Math.Max(1, summary.LastDays.Max(d => Math.Max(d.Received, d.Issued)));
        SummaryDays.Clear();
        foreach (var day in summary.LastDays)
            SummaryDays.Add(new FiscalDaySummaryRow(
                day.Date.ToString("dd/MM"),
                day.Received,
                day.Issued,
                Math.Max(2, 72.0 * day.Received / max),
                Math.Max(2, 72.0 * day.Issued / max)));
    }

    private static int? ParseNumber(string value) =>
        int.TryParse(new string([.. value.Where(char.IsDigit)]), out var n) && n > 0 ? n : null;

    private FiscalDocumentStatus? CurrentStatusFilter() => StatusFilter switch
    {
        "Recebida" => FiscalDocumentStatus.Recebida,
        "Ciência" => FiscalDocumentStatus.Ciencia,
        "Confirmada" => FiscalDocumentStatus.Confirmada,
        "Desconhecida" => FiscalDocumentStatus.Desconhecida,
        "Recusada" => FiscalDocumentStatus.OperacaoNaoRealizada,
        _ => null
    };

    private async Task LoadIssuedIntoAsync(IServiceProvider services)
    {
        var emission = services.GetRequiredService<IFiscalEmissionService>();
        var result = await emission.SearchAsync(
            new PagedQuery { Page = IssuedPage, PageSize = 25, Search = IssuedSearch },
            IssuedFilterFrom, IssuedFilterTo, ParseNumber(IssuedFilterNumber));
        IssuedTotalPages = result.TotalPages;
        IssuedItems.Clear();
        foreach (var item in result.Items)
            IssuedItems.Add(item);
    }

    private async Task LoadIntoAsync(IServiceProvider services)
    {
        var fiscal = services.GetRequiredService<IFiscalService>();
        var result = await fiscal.SearchAsync(
            new PagedQuery { Page = Page, PageSize = 25, Search = Search }, CurrentStatusFilter(),
            FilterFrom, FilterTo, ParseNumber(FilterNumber));
        TotalPages = result.TotalPages;
        Items.Clear();
        foreach (var item in result.Items)
            Items.Add(item);
    }
}

/// <summary>Linha do mini gráfico entrada × saída (alturas já normalizadas em px).</summary>
public record FiscalDaySummaryRow(string Label, int Received, int Issued, double ReceivedBar, double IssuedBar);
