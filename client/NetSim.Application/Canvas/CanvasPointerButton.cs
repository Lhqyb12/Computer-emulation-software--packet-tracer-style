namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Which mouse button was used. Our own copy of the idea, because the Application layer must not know
// Avalonia (which has its own MouseButton) - the canvas control translates between the two
public enum CanvasPointerButton
{
    Left,     // select / drag / place / connect
    Middle,   // the wheel button - drags the canvas (pan) whatever tool is picked
    Right,    // not used yet
}
