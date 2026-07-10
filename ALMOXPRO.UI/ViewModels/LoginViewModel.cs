using ALMOXPRO.Application.Services;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace ALMOXPRO.UI.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly ISessionService _session;

    [ObservableProperty]
    private string _login = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // Recuperação de senha
    [ObservableProperty]
    private bool _isRecoveryMode;

    [ObservableProperty]
    private string _recoveryLoginOrEmail = string.Empty;

    [ObservableProperty]
    private string _recoveryToken = string.Empty;

    public override string Title => "Login";

    /// <summary>Fecha a janela de login informando sucesso.</summary>
    public event EventHandler? LoginSucceeded;

    public LoginViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog, ISessionService session)
        : base(scopeFactory, dialog)
    {
        _session = session;
    }

    [RelayCommand]
    private async Task SignInAsync(object? passwordBox)
    {
        ErrorMessage = string.Empty;
        var password = (passwordBox as System.Windows.Controls.PasswordBox)?.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrEmpty(password))
        {
            ErrorMessage = "Informe o usuário e a senha.";
            return;
        }

        await RunAsync(async services =>
        {
            var auth = services.GetRequiredService<IAuthService>();
            var result = await auth.LoginAsync(Login, password);

            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                return;
            }

            _session.Start(result.Value);

            if (result.Value.MustChangePassword)
            {
                var changePassword = App.Services.GetRequiredService<ChangePasswordViewModel>();
                var window = new Views.ChangePasswordWindow { DataContext = changePassword, Owner = null };
                changePassword.Forced = true;
                if (window.ShowDialog() != true)
                {
                    _session.End();
                    ErrorMessage = "É obrigatório alterar a senha no primeiro acesso.";
                    return;
                }
            }

            LoginSucceeded?.Invoke(this, EventArgs.Empty);
            ((App)System.Windows.Application.Current).ShowMain();
        });
    }

    [RelayCommand]
    private void ToggleRecovery() => IsRecoveryMode = !IsRecoveryMode;

    [RelayCommand]
    private async Task RequestRecoveryAsync()
    {
        if (string.IsNullOrWhiteSpace(RecoveryLoginOrEmail))
        {
            ErrorMessage = "Informe o login ou e-mail.";
            return;
        }

        await RunAsync(async services =>
        {
            var auth = services.GetRequiredService<IAuthService>();
            await auth.RequestPasswordResetAsync(RecoveryLoginOrEmail);
            Dialog.ShowInfo("Se o usuário existir, um código de recuperação foi gerado.\n" +
                            "Solicite o código ao administrador (registrado no log do sistema).");
        });
    }

    [RelayCommand]
    private async Task ResetPasswordAsync(object? passwordBox)
    {
        var newPassword = (passwordBox as System.Windows.Controls.PasswordBox)?.Password ?? string.Empty;
        if (string.IsNullOrWhiteSpace(RecoveryToken) || string.IsNullOrEmpty(newPassword))
        {
            ErrorMessage = "Informe o código de recuperação e a nova senha.";
            return;
        }

        await RunAsync(async services =>
        {
            var auth = services.GetRequiredService<IAuthService>();
            var result = await auth.ResetPasswordAsync(RecoveryToken, newPassword);
            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                return;
            }

            Dialog.ShowInfo("Senha redefinida com sucesso. Faça login com a nova senha.");
            IsRecoveryMode = false;
            RecoveryToken = string.Empty;
            RecoveryLoginOrEmail = string.Empty;
            ErrorMessage = string.Empty;
        });
    }

    [RelayCommand]
    private void Exit() => System.Windows.Application.Current.Shutdown();
}
