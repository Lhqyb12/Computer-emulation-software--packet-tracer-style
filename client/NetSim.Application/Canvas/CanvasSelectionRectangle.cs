namespace NetSim.Application.Canvas;

/// <summary>
/// The marquee/drag-selection rectangle currently being drawn, in world coordinates.
/// <see cref="Min"/>/<see cref="Max"/> are already normalized (Min &lt;= Max on both axes)
/// regardless of which corner the user started dragging from.
/// </summary>
public sealed record CanvasSelectionRectangle(CanvasPoint Min, CanvasPoint Max);
