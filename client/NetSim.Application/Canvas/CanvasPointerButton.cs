namespace NetSim.Application.Canvas;

/// <summary>
/// The pointer buttons <see cref="CanvasInteractionController"/> cares about. A UI-framework-
/// neutral stand-in for Avalonia's <c>MouseButton</c>, so the interaction engine stays free of
/// any Avalonia dependency - see docs/architecture/canvas-interaction.md.
/// </summary>
public enum CanvasPointerButton
{
    Left,
    Middle,
    Right,
}
