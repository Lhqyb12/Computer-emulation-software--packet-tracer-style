using System;   // brings in Math (Clamp, Max, Min) and double.IsNaN

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// The "camera" over the canvas world (step 5): which part of the world is seen, and how big.
//   Origin = the world point shown at the top-left corner of the canvas.
//   Zoom   = how many screen units one world unit takes (1 = normal, 2 = twice as big, 0.5 = half).
// A device always keeps its WORLD position; zooming and scrolling only change this camera.
// The two conversions:
//   screen = (world - Origin) * Zoom          (WorldToScreen - used for drawing)
//   world  = screen / Zoom + Origin           (ScreenToWorld - used to understand where the user clicked)
// "sealed" = nothing inherits from it.
// "record" = two cameras with the same Origin and Zoom count as EQUAL - so "did the camera change?" is a simple ==.
// It never changes after it is built - every change makes a NEW camera (WithOrigin, WithZoom...)
public sealed record CanvasViewport
{
    // "const" = a fixed number written into the program; it can never change
    public const double MinZoom = 0.25;     // the smallest zoom allowed: 25%
    public const double MaxZoom = 4.0;      // the biggest zoom allowed: 400%
    public const double DefaultZoom = 1.0;  // 100% - one world unit = one screen unit

    // The starting camera: world (0, 0) at the top-left corner, 100%.
    // That is exactly what step 3 used (world point = screen point), so devices placed before zooming stay where they were.
    // "static readonly" = one shared copy that can never be replaced
    public static readonly CanvasViewport Default = new(CanvasPoint.Zero, DefaultZoom);

    // Purpose:       Builds a camera.
    // Precondition:  receives origin - the world point to show at the top-left corner; zoom - the wanted zoom
    //                (a zoom outside 25%-400% is pulled back into that range).
    // Postcondition: returns nothing (a constructor) - the camera has this Origin and the zoom, kept in range.
    public CanvasViewport(CanvasPoint origin, double zoom)
    {
        Origin = origin;            // keep the world point that sits at the top-left corner
        Zoom = ClampZoom(zoom);     // keep the zoom, pulled into the allowed range (ClampZoom, below)
    }

    // The world point shown at the canvas's top-left corner.
    // "init" = it may be set only while the camera is being built, never afterwards
    public CanvasPoint Origin { get; init; }

    // How many screen units one world unit takes. Set only while building
    public double Zoom { get; init; }

    // Purpose:       Keeps a zoom value inside the allowed range.
    // Precondition:  receives zoom - any number.
    // Postcondition: returns a zoom between MinZoom and MaxZoom; a "not a number" value (NaN) becomes DefaultZoom.
    // "a ? b : c" = if a then b, otherwise c. Math.Clamp(x, min, max) = "no lower than min, no higher than max"
    public static double ClampZoom(double zoom) =>
        double.IsNaN(zoom) ? DefaultZoom : Math.Clamp(zoom, MinZoom, MaxZoom);

    // Purpose:       Makes a copy of this camera looking at another place.
    // Precondition:  receives origin - the new world point for the top-left corner.
    // Postcondition: returns a NEW camera with that Origin and the same Zoom (this one is not changed).
    public CanvasViewport WithOrigin(CanvasPoint origin) => new(origin, Zoom);

    // Purpose:       Makes a copy of this camera with another zoom (the top-left corner stays put).
    // Precondition:  receives zoom - the new zoom.
    // Postcondition: returns a NEW camera with the same Origin and the new zoom (kept in range).
    public CanvasViewport WithZoom(double zoom) => new(Origin, zoom);

    // Purpose:       Turns a world point into the screen point where it is drawn.
    // Precondition:  receives world - a point in the world.
    // Postcondition: returns the screen point: (world - Origin) * Zoom, measured from the canvas's top-left corner.
    // Example: Origin (100, 0), Zoom 2, world (150, 50) -> ((150-100)*2, (50-0)*2) = (100, 100)
    public CanvasPoint WorldToScreen(CanvasPoint world) =>
        new((world.X - Origin.X) * Zoom, (world.Y - Origin.Y) * Zoom);   // first move by the origin, then grow by the zoom

    // Purpose:       Turns a screen point (e.g. the mouse) into the world point under it - the opposite of WorldToScreen.
    // Precondition:  receives screen - a point measured from the canvas's top-left corner.
    // Postcondition: returns the world point: screen / Zoom + Origin.
    public CanvasPoint ScreenToWorld(CanvasPoint screen) =>
        new(screen.X / Zoom + Origin.X, screen.Y / Zoom + Origin.Y);     // first shrink by the zoom, then move by the origin

    // Purpose:       Zooms so the world point under the mouse STAYS under the mouse (mouse-wheel zoom, like a map).
    // Precondition:  receives screenPoint - the mouse, measured from the canvas's top-left; newZoom - the wanted zoom.
    // Postcondition: returns a NEW camera with the new zoom (kept in range), in which the same world point is still
    //                drawn at screenPoint.
    public CanvasViewport ZoomAround(CanvasPoint screenPoint, double newZoom)
    {
        var worldUnderMouse = ScreenToWorld(screenPoint);   // which world point is under the mouse RIGHT NOW (with the old camera)
        var clampedZoom = ClampZoom(newZoom);               // the new zoom, kept in the allowed range

        // Choose the new origin so that worldUnderMouse lands on screenPoint again.
        // From screen = (world - origin) * zoom  ->  origin = world - screen / zoom
        var newOrigin = new CanvasPoint(
            worldUnderMouse.X - screenPoint.X / clampedZoom,   // the new origin's X
            worldUnderMouse.Y - screenPoint.Y / clampedZoom);  // the new origin's Y

        return new CanvasViewport(newOrigin, clampedZoom);  // hand back the new camera
    }

    // Purpose:       Builds a camera that shows a whole rectangle of the world, with some room around it ("Fit to View").
    // Precondition:  receives contentMin / contentMax - the top-left / bottom-right corners of what must be seen (world);
    //                viewportWidth / viewportHeight - the size of the canvas on the screen.
    // Postcondition: returns a camera centered on the content and zoomed so all of it fits (zoom kept in range).
    public static CanvasViewport Fit(CanvasPoint contentMin, CanvasPoint contentMax, double viewportWidth, double viewportHeight)
    {
        const double paddingRatio = 0.1;   // 10% empty room on every side, so nothing touches the edge

        // The content's width and height. Math.Max(..., 1) = at least 1, so we never divide by 0 below
        var contentWidth = Math.Max(contentMax.X - contentMin.X, 1);
        var contentHeight = Math.Max(contentMax.Y - contentMin.Y, 1);

        // The size with the empty room added on BOTH sides (that is why it is "* 2")
        var paddedWidth = contentWidth * (1 + paddingRatio * 2);
        var paddedHeight = contentHeight * (1 + paddingRatio * 2);

        // The biggest zoom at which BOTH the width and the height still fit = the smaller of the two ratios.
        // When the canvas size is not known yet (0), use the default zoom
        var zoom = viewportWidth > 0 && viewportHeight > 0
            ? Math.Min(viewportWidth / paddedWidth, viewportHeight / paddedHeight)
            : DefaultZoom;
        var clampedZoom = ClampZoom(zoom);   // kept in the allowed range

        // The middle of the content (halfway between its corners)
        var centerX = (contentMin.X + contentMax.X) / 2;
        var centerY = (contentMin.Y + contentMax.Y) / 2;

        // The origin that puts that middle in the middle of the canvas:
        // the canvas shows "viewportWidth / zoom" world units across, so go back by half of that from the middle
        var origin = new CanvasPoint(
            centerX - viewportWidth / (2 * clampedZoom),
            centerY - viewportHeight / (2 * clampedZoom));

        return new CanvasViewport(origin, clampedZoom);   // hand back the new camera
    }
}
