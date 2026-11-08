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

    /// <summary>Exigido por bancos em nuvem (Neon, Supabase, RDS...).</summary>
    [ObservableProperty]
    private bool _useSsl;

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
                UseSsl = builder.SslMode is SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull;
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
        Password = Password,
        SslMode = UseSsl ? SslMode.Require : SslMode.Prefer,
        // Bancos em nuvem podem "acordar" na primeira conexão (cold start).
        Timeout = 30
    }.ConnectionString;

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        try
        {
            var databaseExists = await OpenConnectionAsync();
            StatusMessage = databaseExists
                ? "Conexão estabelecida com sucesso."
                : $"Conexão OK. O banco \"{Database.Trim()}\" ainda não existe e será criado automaticamente ao continuar.";
        }
        catch (Exception ex)
        {
            StatusMessage = FriendlyMessage(ex);
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
            StatusMessage = FriendlyMessage(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Abre a conexão com o banco configurado. Se o banco ainda não existir,
    /// valida servidor e credenciais no banco de manutenção "postgres" — o
    /// EF Core cria o banco na primeira inicialização. Retorna true quando o
    /// banco configurado já existe.
    /// </summary>
    private async Task<bool> OpenConnectionAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(BuildConnectionString());
            await connection.OpenAsync();
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            var builder = new NpgsqlConnectionStringBuilder(BuildConnectionString()) { Database = "postgres" };
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            return false;
        }
    }

    /// <summary>
    /// Mensagens em português para os erros mais comuns. O servidor envia as
    /// mensagens localizadas em codificação incompatível antes da negociação
    /// (aparecem com "�"), então os casos conhecidos são traduzidos aqui.
    /// </summary>
    private string FriendlyMessage(Exception ex) => ex switch
    {
        PostgresException pg when pg.SqlState == PostgresErrorCodes.InvalidPassword =>
            $"Senha incorreta para o usuário \"{Username.Trim()}\".",
        PostgresException pg when pg.SqlState == PostgresErrorCodes.InvalidAuthorizationSpecification =>
            $"Acesso negado para o usuário \"{Username.Trim()}\". Verifique o usuário e o pg_hba.conf.",
        PostgresException pg when pg.SqlState == PostgresErrorCodes.InsufficientPrivilege =>
            $"O usuário \"{Username.Trim()}\" não tem permissão para acessar o banco \"{Database.Trim()}\".",
        PostgresException pg => $"Erro do PostgreSQL ({pg.SqlState}): {pg.MessageText}",
        NpgsqlException => $"Servidor inacessível em {Host.Trim()}:{Port.Trim()}. " +
                           "Verifique se o PostgreSQL está em execução e se o host e a porta estão corretos.",
        _ => $"Falha na conexão: {ex.Message}"
    };
}
