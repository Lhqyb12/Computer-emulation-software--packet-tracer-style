namespace NetSim.Application.Canvas;   // the "address" of this file: it belongs to the Canvas part of the Application layer. Other files reach it with "using NetSim.Application.Canvas"

// One point on the canvas: X (left -> right) and Y (top -> bottom).
// It is a "world" point - where a device really lives on the canvas, no matter how the user zooms or
// scrolls (zoom and scroll, step 5, only change how the world is SHOWN, through the camera).
// We do not use Avalonia's Point here, because the Application layer must not know Avalonia (the screen
// library) - that keeps the rules testable without a screen, and lets the screen library change one day.
// "public"   = every part of the project may use it.
// "readonly" = after a point is built its X and Y can never change; a different place = a new point.
// "record"   = two points with the same X and Y count as EQUAL (compared by value, not "is it the same object").
// "struct"   = a small, light value (like an int), copied when passed around - not a heavy object.
// "(double X, double Y)" = a "primary constructor": "new CanvasPoint(10, 20)" builds a point with X = 10, Y = 20.
//   "double" = a number that can have digits after the point (e.g. 10.5).
public readonly record struct CanvasPoint(double X, double Y)
{
    // The point (0, 0) - the top-left corner of the world.
    // "static"   = one shared copy for the whole program, not one per point.
    // "readonly" = it can never be replaced.
    // "new(0, 0)" = build a point; C# knows the type (CanvasPoint) from the left side, so it is not written again
    public static readonly CanvasPoint Zero = new(0, 0);
}
