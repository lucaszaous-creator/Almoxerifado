using ALMOXPRO.UI.ViewModels;
using System.Windows;

namespace ALMOXPRO.UI.Views;

public partial class ChangePasswordWindow : Window
{
    public ChangePasswordWindow()
    {
        InitializeComponent();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel viewModel)
            viewModel.ConfirmCommand.Execute(new object[] { CurrentPassword, NewPassword, ConfirmPassword });
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel { Forced: true })
        {
            DialogResult = false;
            Close();
            return;
        }

        Close();
    }
}
