using System;

namespace NetSim.Application.Canvas;

/// <summary>
/// Pure snap-to-grid math, kept separate from <see cref="CanvasInteractionController"/> so it is
/// trivially unit-testable on its own - see docs/architecture/canvas-interaction.md.
/// </summary>
public static class CanvasGridSnap
{
    /// <summary>Rounds a world point to the nearest multiple of <paramref name="gridSpacing"/> on each axis.</summary>
    public static CanvasPoint Snap(CanvasPoint point, double gridSpacing)
    {
        if (gridSpacing <= 0)
        {
            return point;
        }

        return new CanvasPoint(
            Math.Round(point.X / gridSpacing) * gridSpacing,
            Math.Round(point.Y / gridSpacing) * gridSpacing);
    }
}
