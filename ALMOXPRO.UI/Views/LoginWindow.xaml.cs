using ALMOXPRO.UI.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace ALMOXPRO.UI.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is LoginViewModel viewModel)
                viewModel.LoginSucceeded += (_, _) => Close();
        };
    }

    private void OnDragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is LoginViewModel viewModel)
            viewModel.SignInCommand.Execute(PasswordInput);
    }
}
