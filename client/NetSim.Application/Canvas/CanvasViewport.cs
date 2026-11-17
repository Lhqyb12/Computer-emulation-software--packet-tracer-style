using System;   // Math, double.IsNaN

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The "camera" over the canvas world (step 5): which part of the world is seen, and how big.
//   Origin = the world point shown at the top-left corner of the canvas.
//   Zoom   = how many screen units one world unit takes (1 = normal, 2 = twice as big, 0.5 = half).
// A device always keeps its WORLD position; zooming and scrolling only change this camera.
// "record" = two viewports with the same Origin and Zoom count as equal; it never changes after it is
// built - every change makes a new one (WithOrigin, WithZoom...), which makes "did it change?" a simple ==
public sealed record CanvasViewport
{
    public const double MinZoom = 0.25;     // the smallest zoom: 25%
    public const double MaxZoom = 4.0;      // the biggest zoom: 400%
    public const double DefaultZoom = 1.0;  // 100%

    // The starting camera: world (0, 0) at the top-left corner, 100%.
    // That is exactly what step 3 used, so devices placed before zooming stay where they were
    public static readonly CanvasViewport Default = new(CanvasPoint.Zero, DefaultZoom);

    // Purpose:       Builds a camera.
    // Precondition:  none (a zoom outside 25%-400% is pulled back into that range).
    // Postcondition: Origin is the given point; Zoom is the given zoom, kept between MinZoom and MaxZoom.
    public CanvasViewport(CanvasPoint origin, double zoom)
    {
        Origin = origin;            // the world point at the top-left corner
        Zoom = ClampZoom(zoom);     // the zoom, kept inside the allowed range
    }

    // The world point shown at the canvas's top-left corner. "init" = may be set only while building
    public CanvasPoint Origin { get; init; }

    // How many screen units one world unit takes
    public double Zoom { get; init; }

    // Purpose:       Keeps a zoom value inside the allowed range.
    // Precondition:  none.
    // Postcondition: returns MinZoom..MaxZoom; a "not a number" value (NaN) becomes DefaultZoom.
    public static double ClampZoom(double zoom) =>
        double.IsNaN(zoom) ? DefaultZoom : Math.Clamp(zoom, MinZoom, MaxZoom);   // Clamp = "no lower than / no higher than"

    // Purpose:       Makes a copy of this camera looking at another place.
    // Precondition:  none.
    // Postcondition: returns a new camera with the given Origin and the same Zoom.
    public CanvasViewport WithOrigin(CanvasPoint origin) => new(origin, Zoom);

    // Purpose:       Makes a copy of this camera with another zoom (around the top-left corner).
    // Precondition:  none.
    // Postcondition: returns a new camera with the same Origin and the given zoom (kept in range).
    public CanvasViewport WithZoom(double zoom) => new(Origin, zoom);

    // Purpose:       Turns a world point into the screen point where it is drawn.
    // Precondition:  world is a world point.
    // Postcondition: returns (world - Origin) * Zoom. Nothing is changed.
    public CanvasPoint WorldToScreen(CanvasPoint world) =>
        new((world.X - Origin.X) * Zoom, (world.Y - Origin.Y) * Zoom);   // move by the origin, then grow by the zoom

    // Purpose:       Turns a screen point (e.g. the mouse) into the world point under it - the opposite of WorldToScreen.
    // Precondition:  screen is measured from the canvas's top-left corner.
    // Postcondition: returns screen / Zoom + Origin. Nothing is changed.
    public CanvasPoint ScreenToWorld(CanvasPoint screen) =>
        new(screen.X / Zoom + Origin.X, screen.Y / Zoom + Origin.Y);     // shrink by the zoom, then move by the origin

    // Purpose:       Zooms so that the world point under the mouse stays under the mouse (mouse-wheel zoom).
    // Precondition:  screenPoint is the mouse, measured from the canvas's top-left corner.
    // Postcondition: returns a new camera with the new zoom (kept in range), in which the same world point
    //                is still drawn at screenPoint.
    public CanvasViewport ZoomAround(CanvasPoint screenPoint, double newZoom)
    {
        var worldUnderMouse = ScreenToWorld(screenPoint);   // what is under the mouse now
        var clampedZoom = ClampZoom(newZoom);               // the new zoom, kept in range

        // Choose the new origin so that worldUnderMouse lands on screenPoint again:
        // screen = (world - origin) * zoom  ->  origin = world - screen / zoom
        var newOrigin = new CanvasPoint(
            worldUnderMouse.X - screenPoint.X / clampedZoom,
            worldUnderMouse.Y - screenPoint.Y / clampedZoom);

        return new CanvasViewport(newOrigin, clampedZoom);  // the new camera
    }

    // Purpose:       Builds a camera that shows a whole rectangle of the world, with some room around it ("Fit to View").
    // Precondition:  contentMin / contentMax = the top-left / bottom-right of what must be seen;
    //                viewportWidth / viewportHeight = the size of the canvas on the screen.
    // Postcondition: returns a camera centered on the content, zoomed so all of it fits (zoom kept in range).
    public static CanvasViewport Fit(CanvasPoint contentMin, CanvasPoint contentMax, double viewportWidth, double viewportHeight)
    {
        const double paddingRatio = 0.1;   // 10% empty room on every side

        // The content's size (at least 1, so we never divide by 0)
        var contentWidth = Math.Max(contentMax.X - contentMin.X, 1);
        var contentHeight = Math.Max(contentMax.Y - contentMin.Y, 1);

        // The size with the empty room added on both sides
        var paddedWidth = contentWidth * (1 + paddingRatio * 2);
        var paddedHeight = contentHeight * (1 + paddingRatio * 2);

        // The biggest zoom at which both the width and the height still fit (the smaller of the two)
        var zoom = viewportWidth > 0 && viewportHeight > 0
            ? Math.Min(viewportWidth / paddedWidth, viewportHeight / paddedHeight)
            : DefaultZoom;
        var clampedZoom = ClampZoom(zoom);   // kept in range

        // The middle of the content
        var centerX = (contentMin.X + contentMax.X) / 2;
        var centerY = (contentMin.Y + contentMax.Y) / 2;

        // The origin that puts that middle in the middle of the canvas
        var origin = new CanvasPoint(
            centerX - viewportWidth / (2 * clampedZoom),
            centerY - viewportHeight / (2 * clampedZoom));

        return new CanvasViewport(origin, clampedZoom);   // the new camera
    }
}
