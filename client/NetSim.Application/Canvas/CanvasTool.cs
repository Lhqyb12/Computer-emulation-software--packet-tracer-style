namespace NetSim.Application.Canvas;

/// <summary>
/// The active interaction mode for the Network Canvas toolbar. New tools (Connect, AddDevice,
/// ...) extend this enum as later phases introduce them, without any change to how the canvas
/// stores/reports its current tool - see docs/architecture/network-canvas.md, "Tools".
/// </summary>
public enum CanvasTool
{
    /// <summary>Click selects a device/connection; drag on empty canvas will (Phase 9) marquee-select.</summary>
    Select,

    /// <summary>Drag moves the viewport instead of selecting/moving canvas content.</summary>
    Pan,

    /// <summary>Click a device interface anchor, then a second one, to create a network Connection.</summary>
    Connect,
}
