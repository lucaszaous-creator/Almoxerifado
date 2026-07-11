using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;
using System.IO;
using System.Text.Json;

namespace ALMOXPRO.UI.ViewModels;

/// <summary>
/// Configuração da conexão com o PostgreSQL, exibida quando o banco não
/// está acessível na inicialização. Salva em ProgramData para não exigir
/// permissão de administrador nem ser sobrescrita por atualizações.
/// </summary>
public partial class DatabaseConfigViewModel : ObservableObject
{
    public static string OverridePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ALMOXPRO", "appsettings.json");

    [ObservableProperty]
    private string _host = "localhost";

    [ObservableProperty]
    private string _port = "5432";

    [ObservableProperty]
    private string _database = "almoxpro";

    [ObservableProperty]
    private string _username = "postgres";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public event EventHandler? Saved;
    public event EventHandler? Cancelled;

    public DatabaseConfigViewModel(string? currentConnectionString, string? initialError = null)
    {
        if (!string.IsNullOrWhiteSpace(currentConnectionString))
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(currentConnectionString);
                Host = builder.Host ?? Host;
                Port = builder.Port.ToString();
                Database = builder.Database ?? Database;
                Username = builder.Username ?? Username;
                Password = builder.Password ?? string.Empty;
            }
            catch (Exception)
            {
                // Connection string inválida: mantém os valores padrão.
            }
        }

        StatusMessage = initialError ?? string.Empty;
    }

    public string BuildConnectionString() => new NpgsqlConnectionStringBuilder
    {
        Host = Host.Trim(),
        Port = int.TryParse(Port, out var port) ? port : 5432,
        Database = Database.Trim(),
        Username = Username.Trim(),
        Password = Password
    }.ConnectionString;

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        try
        {
            await OpenConnectionAsync();
            StatusMessage = "Conexão estabelecida com sucesso.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Falha na conexão: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            await OpenConnectionAsync();

            Directory.CreateDirectory(Path.GetDirectoryName(OverridePath)!);
            var json = JsonSerializer.Serialize(
                new { ConnectionStrings = new { Default = BuildConnectionString() } },
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(OverridePath, json);

            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Falha na conexão: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    private async Task OpenConnectionAsync()
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString());
        await connection.OpenAsync();
    }
}
