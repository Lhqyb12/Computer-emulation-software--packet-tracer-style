namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// One point on the canvas: X (left -> right) and Y (top -> bottom).
// It is a "world" point - where a device really lives on the canvas, no matter how the user later
// zooms or scrolls (zoom and scroll come in step 5 and only change how the world is SHOWN).
// We do not use Avalonia's Point here, because the Application layer must not know Avalonia (the UI library).
// "readonly record struct" = a small value that never changes after it is built, and two points
// with the same X and Y count as equal
public readonly record struct CanvasPoint(double X, double Y)
{
    // The point (0, 0) - the top-left corner of the world. "static readonly" = one shared copy that never changes
    public static readonly CanvasPoint Zero = new(0, 0);
}
