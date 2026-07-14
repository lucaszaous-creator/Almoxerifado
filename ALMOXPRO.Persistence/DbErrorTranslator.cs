using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ALMOXPRO.Persistence;

/// <summary>
/// Converte exceções de persistência (principalmente <see cref="DbUpdateException"/>
/// envolvendo uma <see cref="PostgresException"/>) em mensagens compreensíveis para o
/// usuário final. O EF Core, por padrão, apenas informa
/// "An error occurred while saving the entity changes. See the inner exception for details.",
/// escondendo a causa real — que fica na inner exception do PostgreSQL.
/// </summary>
public static class DbErrorTranslator
{
    /// <summary>
    /// Produz uma mensagem amigável para exibição. Quando encontra uma
    /// <see cref="PostgresException"/> na cadeia de inner exceptions, traduz os códigos
    /// SQLSTATE mais comuns; caso contrário, devolve a mensagem mais interna disponível,
    /// garantindo que a causa real nunca fique oculta.
    /// </summary>
    public static string ToFriendlyMessage(Exception exception)
    {
        var pg = FindPostgresException(exception);
        if (pg is not null)
            return FromPostgres(pg);

        return Innermost(exception).Message;
    }

    /// <summary>Indica se a exceção (ou alguma inner) tem origem no banco de dados.</summary>
    public static bool IsDatabaseError(Exception exception) =>
        FindPostgresException(exception) is not null;

    private static string FromPostgres(PostgresException pg) => pg.SqlState switch
    {
        // 23505 - unique_violation
        PostgresErrorCodes.UniqueViolation =>
            "Já existe um registro com esses dados. Verifique os campos únicos "
            + "(código, documento, e-mail, etc.) e tente novamente.",

        // 23503 - foreign_key_violation
        PostgresErrorCodes.ForeignKeyViolation =>
            "A operação não pôde ser concluída porque este registro está vinculado "
            + "a outros dados no sistema. Remova ou ajuste os vínculos antes de continuar.",

        // 23502 - not_null_violation
        PostgresErrorCodes.NotNullViolation =>
            $"O campo obrigatório '{pg.ColumnName}' não foi preenchido.",

        // 23514 - check_violation
        PostgresErrorCodes.CheckViolation =>
            "Um dos valores informados não atende às regras de validação do sistema.",

        // 22001 - string_data_right_truncation
        PostgresErrorCodes.StringDataRightTruncation =>
            "Um dos textos informados excede o tamanho máximo permitido.",

        _ => string.IsNullOrWhiteSpace(pg.MessageText)
            ? "Não foi possível salvar as alterações no banco de dados."
            : $"Não foi possível salvar as alterações: {pg.MessageText}",
    };

    private static PostgresException? FindPostgresException(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException pg)
                return pg;
        }
        return null;
    }

    private static Exception Innermost(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
            current = current.InnerException;
        return current;
    }
}
