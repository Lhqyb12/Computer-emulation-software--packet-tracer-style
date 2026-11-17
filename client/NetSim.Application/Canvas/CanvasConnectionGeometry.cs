using System;                      // Math
using System.Collections.Generic;  // List<T>, IReadOnlyList<T>
using NetSim.Core.Common;          // EntityId
using NetSim.Core.Topology;        // Network

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Where the cables are on the canvas (step 6): builds a drawable line for every cable of the network,
// and finds the cable under the mouse (step 4 - so a cable can be selected and deleted).
// New - in the reference project this lived inside the screen's view model; here it is in the
// Application layer, so the mouse logic (CanvasInteractionController) can use it too.
// "static class" = no objects, just call CanvasConnectionGeometry.Something(...)
public static class CanvasConnectionGeometry
{
    // Purpose:       Builds one line for every cable whose two devices are both on the canvas.
    // Precondition:  none (network may be null when no project is open).
    // Postcondition: returns the lines, from device middle to device middle (empty when there is no network).
    //                Nothing is changed.
    public static IReadOnlyList<CanvasConnectionView> Build(Network? network, ICanvasItemsState items)
    {
        // No network, or no cables - no lines. "[]" = an empty list
        if (network is null || network.Connections.Count == 0)
        {
            return [];
        }

        // The lines we build
        var views = new List<CanvasConnectionView>();

        // One line per cable
        foreach (var connection in network.Connections)
        {
            // The canvas items of the two devices at the ends of the cable (null = not on the canvas)
            var itemA = items.GetItem(connection.EndpointA.Device.Id);
            var itemB = items.GetItem(connection.EndpointB.Device.Id);

            // Both devices must be drawn, otherwise there is nothing to draw the line between
            if (itemA is null || itemB is null)
            {
                continue;   // skip this cable, go on to the next one
            }

            // A line from the middle of one device to the middle of the other
            views.Add(new CanvasConnectionView(connection.Id, itemA.Center, itemB.Center, connection.ConnectionType, connection.State));
        }

        // Hand the lines back
        return views;
    }

    // Purpose:       Finds the cable closest to a world point, if it is close enough (a click on a cable).
    // Precondition:  cables were built by Build(); radius = how far from the line still counts, in world units.
    // Postcondition: returns the id of the closest cable within radius, or null. Nothing is changed.
    public static EntityId? HitTest(IReadOnlyList<CanvasConnectionView> cables, CanvasPoint worldPoint, double radius)
    {
        EntityId? best = null;       // the closest cable so far (none yet)
        var bestDistance = radius;   // only cables closer than this count

        // Check every cable
        foreach (var cable in cables)
        {
            // How far the point is from this line
            var distance = DistanceToSegment(worldPoint, cable.A, cable.B);

            // Closer than the best so far - this one wins (for now)
            if (distance <= bestDistance)
            {
                bestDistance = distance;   // the new best distance
                best = cable.Id;           // the new best cable
            }
        }

        // The winner, or null when no cable was close enough
        return best;
    }

    // Purpose:       Measures the shortest distance from a point to a line segment (from A to B).
    // Precondition:  none.
    // Postcondition: returns the distance (0 or more). Nothing is changed.
    private static double DistanceToSegment(CanvasPoint p, CanvasPoint a, CanvasPoint b)
    {
        // The segment as a direction: how far B is from A, on X and on Y
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;

        // The segment's length, squared (no square root needed for the next steps)
        var lengthSquared = dx * dx + dy * dy;

        // A and B are the same point - the distance is simply from p to A
        if (lengthSquared <= double.Epsilon)
        {
            return Distance(p, a);
        }

        // t = how far along the segment (0 = at A, 1 = at B) the point nearest to p is.
        // This is the "projection" of p onto the line; Clamp keeps it on the segment, not beyond its ends
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0.0, 1.0);

        // That nearest point itself
        var nearest = new CanvasPoint(a.X + t * dx, a.Y + t * dy);

        // The distance from p to it
        return Distance(p, nearest);
    }

    // Purpose:       Measures the straight distance between two points (Pythagoras).
    // Precondition:  none.
    // Postcondition: returns the distance (0 or more).
    private static double Distance(CanvasPoint p, CanvasPoint q) =>
        Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y));   // square root of (dx² + dy²)
}
