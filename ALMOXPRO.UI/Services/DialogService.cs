using Microsoft.Win32;
using System.Windows;

namespace ALMOXPRO.UI.Services;

public interface IDialogService
{
    void ShowError(string message);
    void ShowInfo(string message);
    bool Confirm(string message, string title = "Confirmação");
    string? SaveFile(string suggestedName, string filter);
    string? OpenFile(string filter);
}

public class DialogService : IDialogService
{
    public void ShowError(string message) =>
        MessageBox.Show(message, "ALMOX PRO", MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string message) =>
        MessageBox.Show(message, "ALMOX PRO", MessageBoxButton.OK, MessageBoxImage.Information);

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
