using ALMOXPRO.UI.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace ALMOXPRO.UI.Views;

public partial class DatabaseConfigWindow : Window
{
    public DatabaseConfigWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not DatabaseConfigViewModel viewModel)
                return;
            PasswordInput.Password = viewModel.Password;
            viewModel.Saved += (_, _) => DialogResult = true;
            viewModel.Cancelled += (_, _) => DialogResult = false;
        };
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is DatabaseConfigViewModel viewModel)
            viewModel.Password = PasswordInput.Password;
    }

    private void OnDragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
