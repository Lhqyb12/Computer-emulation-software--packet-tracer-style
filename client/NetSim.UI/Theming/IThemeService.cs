using System;

namespace NetSim.UI.Theming;

/// <summary>
/// Runtime theme switching for the application shell. ViewModels depend on this abstraction
/// rather than touching Avalonia's Application directly, so theme selection stays testable and
/// so Views only ever need to react to <see cref="ThemeChanged"/> via bindings/resources.
/// </summary>
public interface IThemeService
{
    AppTheme CurrentTheme { get; }

    event EventHandler<AppTheme>? ThemeChanged;

    void ApplyTheme(AppTheme theme);
}
