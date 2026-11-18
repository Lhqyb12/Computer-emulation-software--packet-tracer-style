using System;   // brings in Math - Math.Round rounds a number to the nearest whole number

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// "Snap to grid" (step 5): moves a point to the nearest grid crossing, so devices line up neatly.
// "static class" = there are no CanvasGridSnap objects; you call it straight through the class: CanvasGridSnap.Snap(...)
public static class CanvasGridSnap
{
    // Purpose:       Moves a point to the nearest crossing of a grid.
    // Precondition:  receives point - the point to move (a world point); gridSpacing - the distance between grid lines
    //                in world units (0 or less means "there is no grid").
    // Postcondition: returns the nearest grid crossing, or the same point when gridSpacing is 0 or less.
    //                Example: spacing 40, point (55, 18) -> (40, 0); point (61, 21) -> (80, 40).
    public static CanvasPoint Snap(CanvasPoint point, double gridSpacing)
    {
        // No real grid (0 or a negative distance) - leave the point as it is. (Dividing by 0 below would also break)
        if (gridSpacing <= 0)
        {
            return point;   // stop here and hand back the point unchanged
        }

        // For each of X and Y, three small steps (example: X = 55, spacing = 40):
        //   point.X / gridSpacing  = how many grid squares from 0 we are       -> 55 / 40 = 1.375
        //   Math.Round(...)        = the nearest WHOLE number of squares         -> 1
        //   * gridSpacing          = back to world units = the grid line itself  -> 1 * 40 = 40
        return new CanvasPoint(
            Math.Round(point.X / gridSpacing) * gridSpacing,    // the nearest vertical grid line
            Math.Round(point.Y / gridSpacing) * gridSpacing);   // the nearest horizontal grid line
    }
}
