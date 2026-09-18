namespace NetSim.Application.Canvas;

/// <summary>
/// A width/height pair in world units, used for a <see cref="CanvasItem"/>'s footprint on the
/// canvas. Kept distinct from <see cref="CanvasPoint"/> (a position) the same way Avalonia
/// separates <c>Size</c> from <c>Point</c>, even though both are just two doubles.
/// </summary>
public readonly record struct CanvasSize(double Width, double Height)
{
    public static readonly CanvasSize Zero = new(0, 0);
}
