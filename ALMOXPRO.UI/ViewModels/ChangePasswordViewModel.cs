using ALMOXPRO.Application.Services;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace ALMOXPRO.UI.ViewModels;

public partial class ChangePasswordViewModel : ViewModelBase
{
    private readonly ISessionService _session;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>Quando true, a troca é obrigatória (primeiro acesso).</summary>
    public bool Forced { get; set; }

    public override string Title => "Alterar Senha";

    public ChangePasswordViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog, ISessionService session)
        : base(scopeFactory, dialog)
    {
        _session = session;
    }

    [RelayCommand]
    private async Task ConfirmAsync(object? parameter)
    {
        if (parameter is not object[] boxes || boxes.Length != 3
            || boxes[0] is not PasswordBox current
            || boxes[1] is not PasswordBox next
            || boxes[2] is not PasswordBox confirmation)
            return;

        ErrorMessage = string.Empty;

        if (next.Password != confirmation.Password)
        {
            ErrorMessage = "A confirmação não confere com a nova senha.";
            return;
        }

        await RunAsync(async services =>
        {
            var auth = services.GetRequiredService<IAuthService>();
            var result = await auth.ChangePasswordAsync(_session.UserId ?? 0, current.Password, next.Password);
            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                return;
            }

            Dialog.ShowInfo("Senha alterada com sucesso.");
            CloseWindow(current, true);
        });
    }

    [RelayCommand]
    private void Cancel(object? parameter)
    {
        if (parameter is PasswordBox box)
            CloseWindow(box, false);
    }

    private static void CloseWindow(DependencyObject child, bool result)
    {
        var window = Window.GetWindow(child);
        if (window is not null)
        {
            window.DialogResult = result;
            window.Close();
        }
    }
}
