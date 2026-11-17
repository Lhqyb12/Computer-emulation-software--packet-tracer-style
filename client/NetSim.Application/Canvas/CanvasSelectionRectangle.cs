namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The selection rectangle the user is drawing (drag on empty canvas), in world points.
// Min = its top-left corner, Max = its bottom-right corner - whichever way the mouse was dragged.
// "record" = two rectangles with the same corners count as equal; "sealed" = nothing inherits from it
public sealed record CanvasSelectionRectangle(CanvasPoint Min, CanvasPoint Max);
