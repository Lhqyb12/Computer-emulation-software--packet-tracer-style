using System;
using Avalonia.Styling;

namespace NetSim.UI.Theming;

/// <summary>Default <see cref="IThemeService"/> implementation. Holds the current selection in
/// memory and maps it onto Avalonia's <see cref="ThemeVariant"/> through <see cref="IThemeVariantWriter"/>,
/// which is what actually re-resolves every DynamicResource in the Light/Dark theme dictionaries.</summary>
public sealed class ThemeService : IThemeService
{
    private readonly IThemeVariantWriter _host;

    public ThemeService(IThemeVariantWriter host)
    {
        _host = host;
        ApplyTheme(AppTheme.Dark);
    }

    public AppTheme CurrentTheme { get; private set; }

    public event EventHandler<AppTheme>? ThemeChanged;

    public void ApplyTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        _host.RequestedThemeVariant = MapToThemeVariant(theme);
        ThemeChanged?.Invoke(this, theme);
    }

    public static ThemeVariant MapToThemeVariant(AppTheme theme) => theme switch
    {
        AppTheme.Dark => ThemeVariant.Dark,
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.System => ThemeVariant.Default,
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, message: null)
    };
}
