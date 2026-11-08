using ALMOXPRO.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ALMOXPRO.Infrastructure.Email;

/// <summary>
/// Serviço de e-mail. Sem servidor SMTP configurado, registra a mensagem no log
/// (útil em ambientes internos onde a recuperação é feita pelo administrador).
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(ILogger<SmtpEmailService> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        // Integração SMTP real pode ser configurada futuramente via app_settings.
        _logger.LogInformation("E-mail para {To} | {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
