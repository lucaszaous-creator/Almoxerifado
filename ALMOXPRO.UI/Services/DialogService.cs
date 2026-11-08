using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Windows;

namespace ALMOXPRO.UI.Services;

public interface IDialogService
{
    void ShowError(string message);
    void ShowInfo(string message);

    /// <summary>Notificação discreta (snackbar), para confirmações de sucesso que não exigem clique.</summary>
    void Notify(string message);

    bool Confirm(string message, string title = "Confirmação");
    string? SaveFile(string suggestedName, string filter);
    string? OpenFile(string filter);
}

public class DialogService : IDialogService
{
    /// <summary>Fila do snackbar exibido no MainWindow.</summary>
    public static SnackbarMessageQueue MessageQueue { get; } = new(TimeSpan.FromSeconds(3));

    public void ShowError(string message) =>
        MessageBox.Show(message, "ALMOX PRO", MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string message) =>
        MessageBox.Show(message, "ALMOX PRO", MessageBoxButton.OK, MessageBoxImage.Information);

    public void Notify(string message) => MessageQueue.Enqueue(message);

    public bool Confirm(string message, string title = "Confirmação") =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? SaveFile(string suggestedName, string filter)
    {
        var dialog = new SaveFileDialog { FileName = suggestedName, Filter = filter };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? OpenFile(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
