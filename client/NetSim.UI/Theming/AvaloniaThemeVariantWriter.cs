using Avalonia.Styling;

namespace NetSim.UI.Theming;

/// <summary>
/// Default <see cref="IThemeVariantWriter"/> backed by the live Avalonia Application.
/// Avalonia.Application is referenced fully-qualified here because this project's namespace
/// (NetSim.UI.Theming, nested under NetworkSimulator) makes the sibling
/// NetSim.Application project namespace shadow the unqualified name "Application" -
/// enclosing-namespace lookup wins over a "using Avalonia;" directive in C#.
/// </summary>
public sealed class AvaloniaThemeVariantWriter : IThemeVariantWriter
{
    public ThemeVariant RequestedThemeVariant
    {
        set => global::Avalonia.Application.Current!.RequestedThemeVariant = value;
    }
}
