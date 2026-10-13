using Avalonia.Data.Converters;  // IValueConverter, FuncValueConverter

namespace NetSim.App;

/// <summary>
/// A tiny binding helper. XAML can't do "if width &gt;= 700" on its own, so this
/// converter takes the window width (a number) and returns true / false.
/// Used to hide the branding panel when the window is narrow.
/// </summary>
public static class WidthToBool
{
    // true when the width is 700 or more. A converter is an object a binding passes its value through;
    // "width => width >= 700" is the small function it runs (a double goes in, a bool comes out)
    public static readonly IValueConverter AtLeast700 =
        new FuncValueConverter<double, bool>(width => width >= 700);

    // The opposite: true when the width is less than 700
    public static readonly IValueConverter Below700 =
        new FuncValueConverter<double, bool>(width => width < 700);
}
