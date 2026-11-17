namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// What the mouse is in the middle of doing right now (CanvasInteractionController keeps it)
public enum CanvasInteractionMode
{
    Idle,            // nothing - the button is not held, or it was just pressed and has not moved far yet
    Panning,         // dragging the canvas itself (step 5)
    DraggingItems,   // dragging the selected devices (step 4)
    SelectingRect,   // drawing a selection rectangle on empty canvas (step 4)
}
