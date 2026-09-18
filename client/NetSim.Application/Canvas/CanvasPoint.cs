namespace NetSim.Application.Canvas;

/// <summary>
/// A 2D point used by the Network Canvas. Depending on context it is either a canvas/world
/// coordinate (where a device "lives", independent of zoom/pan) or a screen coordinate (where
/// something is drawn right now) - see docs/architecture/network-canvas.md, "Coordinate system".
/// Deliberately not Avalonia's Point: this type is used by NetSim.Application, which
/// must not depend on Avalonia.
/// </summary>
public readonly record struct CanvasPoint(double X, double Y)
{
    public static readonly CanvasPoint Zero = new(0, 0);
}
