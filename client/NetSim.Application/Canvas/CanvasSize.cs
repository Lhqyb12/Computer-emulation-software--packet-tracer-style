namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// A width and a height on the canvas, in world units - how much room something takes.
// It is kept apart from CanvasPoint (a place) even though both are two numbers, so a size can never
// be passed by mistake where a place is expected (the compiler stops it)
public readonly record struct CanvasSize(double Width, double Height)
{
    // A size of nothing (0 x 0). One shared copy that never changes
    public static readonly CanvasSize Zero = new(0, 0);
}
