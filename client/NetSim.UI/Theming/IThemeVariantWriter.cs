using Avalonia.Styling;

namespace NetSim.UI.Theming;

/// <summary>
/// The single piece of Avalonia state theme switching needs to write to
/// (Application.Current.RequestedThemeVariant). Isolated behind this interface so
/// <see cref="ThemeService"/> can be unit tested with a fake host instead of requiring a live
/// Avalonia <c>Application</c> instance.
/// </summary>
public interface IThemeVariantWriter
{
    ThemeVariant RequestedThemeVariant { set; }
}
