using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ALMOXPRO.UI.ViewModels;

/// <summary>
/// Base dos ViewModels. Cada operação de dados roda em um escopo de DI próprio,
/// garantindo um DbContext novo por operação.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    protected readonly IServiceScopeFactory ScopeFactory;
    protected readonly IDialogService Dialog;

    [ObservableProperty]
    private bool _isBusy;

    protected ViewModelBase(IServiceScopeFactory scopeFactory, IDialogService dialog)
    {
        ScopeFactory = scopeFactory;
        Dialog = dialog;
    }

    /// <summary>Título mostrado no breadcrumb.</summary>
    public abstract string Title { get; }

    public virtual Task LoadAsync() => Task.CompletedTask;

    protected async Task RunAsync(Func<IServiceProvider, Task> operation)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            using var scope = ScopeFactory.CreateScope();
            await operation(scope.ServiceProvider);
        }
        catch (Domain.Exceptions.DomainException ex)
        {
            Dialog.ShowError(ex.Message);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            Dialog.ShowError(
                "O estoque foi alterado por outro usuário neste instante.\n" +
                "Atualize a tela e repita a operação.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro na operação do ViewModel {ViewModel}", GetType().Name);
            Dialog.ShowError($"Ocorreu um erro inesperado:\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
