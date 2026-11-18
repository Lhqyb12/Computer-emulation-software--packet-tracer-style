namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer. Other files reach it with "using NetSim.Application.Canvas"

// A width and a height on the canvas, in world units - how much room something takes (e.g. a device: 96 x 72).
// It is kept apart from CanvasPoint (a PLACE) even though both are two numbers, so a size can never be passed
// by mistake where a place is expected: they are different types, so the compiler stops such a mistake.
// "readonly record struct" = a small value that never changes, compared by its values (see CanvasPoint).
// "(double Width, double Height)" = "new CanvasSize(96, 72)" builds a size 96 wide and 72 high
public readonly record struct CanvasSize(double Width, double Height)
{
    // A size of nothing (0 x 0). "static readonly" = one shared copy that can never be replaced
    public static readonly CanvasSize Zero = new(0, 0);
}
