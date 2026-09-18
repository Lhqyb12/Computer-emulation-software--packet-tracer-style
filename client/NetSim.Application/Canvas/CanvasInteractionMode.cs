namespace NetSim.Application.Canvas;

/// <summary>
/// What <see cref="CanvasInteractionController"/> is currently doing with the pointer. Exposed so
/// the UI layer can drive cursor/visual feedback (see NetworkCanvasControl) without re-deriving
/// it from raw pointer state.
/// </summary>
public enum CanvasInteractionMode
{
    Idle,
    Panning,
    DraggingItems,
    SelectingRect,
}
