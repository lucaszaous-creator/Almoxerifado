using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;

namespace ALMOXPRO.UI.Services;

/// <summary>Sessão do usuário autenticado na aplicação desktop.</summary>
public interface ISessionService : ICurrentSession
{
    UserSessionDto? Current { get; }
    void Start(UserSessionDto session);
    void End();
    event EventHandler? SessionChanged;
}

public class SessionService : ISessionService
{
    private static readonly IReadOnlySet<string> Empty = new HashSet<string>();

    public UserSessionDto? Current { get; private set; }

    public int? UserId => Current?.UserId;
    public string UserName => Current?.Name ?? string.Empty;
    public bool IsAuthenticated => Current is not null;
    public IReadOnlySet<string> Permissions => Current?.Permissions ?? Empty;

    public event EventHandler? SessionChanged;

    public bool HasPermission(string permissionCode) =>
        Current is not null && Current.Permissions.Contains(permissionCode);

    public void Start(UserSessionDto session)
    {
        Current = session;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void End()
    {
        Current = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
