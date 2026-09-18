using System;

namespace NetSim.Application.Canvas;

/// <summary>
/// Immutable snapshot of the Network Canvas viewport: which world point is anchored at the
/// screen's top-left corner (<see cref="Origin"/>), and the current zoom factor. This is the
/// single place the world-to-screen transform is defined - see
/// docs/architecture/network-canvas.md, "Coordinate system" - so drag/drop, device rendering and
/// hit-testing all convert coordinates the same way.
/// </summary>
public sealed record CanvasViewport
{
    public const double MinZoom = 0.1;
    public const double MaxZoom = 4.0;
    public const double DefaultZoom = 1.0;

    public static readonly CanvasViewport Default = new(CanvasPoint.Zero, DefaultZoom);

    public CanvasViewport(CanvasPoint origin, double zoom)
    {
        Origin = origin;
        Zoom = ClampZoom(zoom);
    }

    /// <summary>The world-space point currently displayed at the screen's top-left corner.</summary>
    public CanvasPoint Origin { get; init; }

    /// <summary>1.0 = 100%. Always within [<see cref="MinZoom"/>, <see cref="MaxZoom"/>].</summary>
    public double Zoom { get; init; }

    public static double ClampZoom(double zoom) =>
        double.IsNaN(zoom) ? DefaultZoom : Math.Clamp(zoom, MinZoom, MaxZoom);

    public CanvasViewport WithOrigin(CanvasPoint origin) => new(origin, Zoom);

    public CanvasViewport WithZoom(double zoom) => new(Origin, zoom);

    /// <summary>Converts a world/canvas coordinate to the screen coordinate it currently renders at.</summary>
    public CanvasPoint WorldToScreen(CanvasPoint world) =>
        new((world.X - Origin.X) * Zoom, (world.Y - Origin.Y) * Zoom);

    /// <summary>Converts a screen coordinate (e.g. a mouse position) back to world/canvas space.</summary>
    public CanvasPoint ScreenToWorld(CanvasPoint screen) =>
        new(screen.X / Zoom + Origin.X, screen.Y / Zoom + Origin.Y);

    /// <summary>
    /// Returns the viewport that has <paramref name="newZoom"/> but keeps the world point
    /// currently under <paramref name="screenPoint"/> fixed at that same screen position - i.e.
    /// "zoom toward the cursor" rather than zooming around the origin. See
    /// docs/architecture/canvas-interaction.md, "Cursor-centered zoom".
    /// </summary>
    public CanvasViewport ZoomAround(CanvasPoint screenPoint, double newZoom)
    {
        var worldAtScreenPoint = ScreenToWorld(screenPoint);
        var clampedZoom = ClampZoom(newZoom);
        var newOrigin = new CanvasPoint(
            worldAtScreenPoint.X - screenPoint.X / clampedZoom,
            worldAtScreenPoint.Y - screenPoint.Y / clampedZoom);

        return new CanvasViewport(newOrigin, clampedZoom);
    }

    /// <summary>
    /// Returns the viewport that centers and zooms to fit the world-space rectangle
    /// [<paramref name="contentMin"/>, <paramref name="contentMax"/>] within a screen of the
    /// given size, with a 10% padding margin. Used by "Fit to View" once there is content to fit
    /// - see docs/architecture/canvas-interaction.md.
    /// </summary>
    public static CanvasViewport Fit(CanvasPoint contentMin, CanvasPoint contentMax, double viewportWidth, double viewportHeight)
    {
        const double paddingRatio = 0.1;

        var contentWidth = Math.Max(contentMax.X - contentMin.X, 1);
        var contentHeight = Math.Max(contentMax.Y - contentMin.Y, 1);
        var paddedWidth = contentWidth * (1 + paddingRatio * 2);
        var paddedHeight = contentHeight * (1 + paddingRatio * 2);

        var zoom = viewportWidth > 0 && viewportHeight > 0
            ? Math.Min(viewportWidth / paddedWidth, viewportHeight / paddedHeight)
            : DefaultZoom;

        var centerX = (contentMin.X + contentMax.X) / 2;
        var centerY = (contentMin.Y + contentMax.Y) / 2;
        var clampedZoom = ClampZoom(zoom);
        var origin = new CanvasPoint(centerX - viewportWidth / (2 * clampedZoom), centerY - viewportHeight / (2 * clampedZoom));

        return new CanvasViewport(origin, clampedZoom);
    }
}
