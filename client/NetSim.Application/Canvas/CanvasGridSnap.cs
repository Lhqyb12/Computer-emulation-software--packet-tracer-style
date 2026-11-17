using System;   // Math

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// "Snap to grid" (step 5): moves a point to the nearest grid crossing, so devices line up neatly.
// "static class" = no objects, just call CanvasGridSnap.Snap(...)
public static class CanvasGridSnap
{
    // Purpose:       Moves a point to the nearest crossing of a grid with the given spacing.
    // Precondition:  none (a spacing of 0 or less means "no grid").
    // Postcondition: returns the nearest grid crossing, or the same point when gridSpacing <= 0.
    //                Example: spacing 40, point (55, 18) -> (40, 0); point (61, 21) -> (80, 40).
    public static CanvasPoint Snap(CanvasPoint point, double gridSpacing)
    {
        // No real grid - leave the point as it is
        if (gridSpacing <= 0)
        {
            return point;   // stop here - nothing to snap to
        }

        // X / spacing = how many grid squares from 0 (e.g. 55 / 40 = 1.375); Round = the nearest whole
        // number (1); times the spacing again = the grid line (40). The same for Y
        return new CanvasPoint(
            Math.Round(point.X / gridSpacing) * gridSpacing,    // nearest vertical grid line
            Math.Round(point.Y / gridSpacing) * gridSpacing);   // nearest horizontal grid line
    }
}
