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

    public override string Title => "Notas Fiscais";

    public FiscalViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog, ISessionService session)
        : base(scopeFactory, dialog)
    {
        _session = session;
    }

    public override Task LoadAsync() => SearchDocumentsAsync();

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

    private async Task LoadIntoAsync(IServiceProvider services)
    {
        FiscalDocumentStatus? status = StatusFilter switch
        {
            "Recebida" => FiscalDocumentStatus.Recebida,
            "Ciência" => FiscalDocumentStatus.Ciencia,
            "Confirmada" => FiscalDocumentStatus.Confirmada,
            "Desconhecida" => FiscalDocumentStatus.Desconhecida,
            "Recusada" => FiscalDocumentStatus.OperacaoNaoRealizada,
            _ => null
        };

        var fiscal = services.GetRequiredService<IFiscalService>();
        var result = await fiscal.SearchAsync(
            new PagedQuery { Page = Page, PageSize = 25, Search = Search }, status);
        TotalPages = result.TotalPages;
        Items.Clear();
        foreach (var item in result.Items)
            Items.Add(item);
    }
}
