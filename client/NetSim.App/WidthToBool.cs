using Avalonia.Data.Converters;

namespace NetSim.App;

/// <summary>
/// A tiny binding helper. XAML can't do "if width &gt;= 700" on its own, so this
/// converter takes the window width (a number) and returns true / false.
/// Used to hide the branding panel when the window is narrow.
/// </summary>
public static class WidthToBool
{
    public static readonly IValueConverter AtLeast700 =
        new FuncValueConverter<double, bool>(width => width >= 700);

    public static readonly IValueConverter Below700 =
        new FuncValueConverter<double, bool>(width => width < 700);
}
