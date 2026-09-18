namespace NetSim.Application.Canvas;

/// <summary>
/// The size of the virtual world the Network Canvas represents, centered on world (0,0). This is
/// deliberately far larger than any screen, so a large future topology never runs out of room -
/// see docs/architecture/network-canvas.md, "World size". Not a hard clipping rule (nothing stops
/// a device outside these bounds today) - it exists so a later phase deciding on scroll
/// limits/minimap extents has a single source of truth to read instead of inventing its own.
/// </summary>
public readonly record struct CanvasWorldBounds(double Width, double Height)
{
    public static readonly CanvasWorldBounds Default = new(20_000, 20_000);
}
