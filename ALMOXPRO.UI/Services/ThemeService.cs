using MaterialDesignThemes.Wpf;

namespace ALMOXPRO.UI.Services;

public interface IThemeService
{
    bool IsDark { get; }
    void Apply(bool dark);
    void Toggle();
}

public class ThemeService : IThemeService
{
    private readonly PaletteHelper _paletteHelper = new();

    public bool IsDark { get; private set; }

    public void Apply(bool dark)
    {
        var theme = _paletteHelper.GetTheme();
        theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
        _paletteHelper.SetTheme(theme);
        IsDark = dark;
    }

    public void Toggle() => Apply(!IsDark);
}
