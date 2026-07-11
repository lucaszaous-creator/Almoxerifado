using ALMOXPRO.UI.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace ALMOXPRO.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Alimenta o monitor de inatividade da sessão.
        PreviewMouseMove += OnUserActivity;
        PreviewMouseDown += OnUserActivity;
        PreviewKeyDown += OnUserActivity;
    }

    private void OnUserActivity(object sender, InputEventArgs e) =>
        (DataContext as MainViewModel)?.RegisterActivity();
}
