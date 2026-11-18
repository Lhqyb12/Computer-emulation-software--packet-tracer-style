using System;                      // brings in Math (Clamp, Sqrt)
using System.Collections.Generic;  // brings in List<T> and IReadOnlyList<T>
using NetSim.Core.Common;          // brings in EntityId - a cable's unique id
using NetSim.Core.Topology;        // brings in Network - all the devices and cables

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// Where the cables are on the canvas (step 6): builds a drawable line for every cable of the network,
// and finds the cable under the mouse (step 4 - so a cable can be selected and deleted).
// New - in the reference project this lived inside the screen's view model; here it is in the Application
// layer, so the mouse logic (CanvasInteractionController) can use it too.
// "static class" = no objects; call it straight through the class: CanvasConnectionGeometry.Build(...)
public static class CanvasConnectionGeometry
{
    // Purpose:       Builds one line for every cable whose two devices are both on the canvas.
    // Precondition:  receives network - the open network (may be null when no project is open);
    //                items - the canvas positions of the devices.
    // Postcondition: returns the lines (each from one device's middle to the other's), or an empty list when there
    //                is no network or no cable. Nothing is changed.
    public static IReadOnlyList<CanvasConnectionView> Build(Network? network, ICanvasItemsState items)
    {
        // No network, or a network with no cables - no lines. "||" = OR; "[]" = an empty list
        if (network is null || network.Connections.Count == 0)
        {
            return [];
        }

        // The lines we build, one by one
        var views = new List<CanvasConnectionView>();

        // Go over every cable of the network
        foreach (var connection in network.Connections)
        {
            // The canvas items of the two devices at the cable's ends:
            // connection.EndpointA = the first port; .Device = its device; .Id = the device's id -> GetItem finds its item (or null)
            var itemA = items.GetItem(connection.EndpointA.Device.Id);
            var itemB = items.GetItem(connection.EndpointB.Device.Id);

            // Both devices must be drawn, otherwise there is nothing to draw the line between
            if (itemA is null || itemB is null)
            {
                continue;   // skip THIS cable and go on to the next one
            }

            // A line from the middle of one device to the middle of the other, with the cable's id, kind and link state
            views.Add(new CanvasConnectionView(connection.Id, itemA.Center, itemB.Center, connection.ConnectionType, connection.State));
        }

        // Hand back all the lines
        return views;
    }

    // Purpose:       Finds the cable closest to a world point, if it is close enough (a click on a cable).
    // Precondition:  receives cables - the lines built by Build(); worldPoint - the point (e.g. the mouse, in the world);
    //                radius - how far from a line still counts, in world units.
    // Postcondition: returns the id of the closest cable within radius, or null when no cable is that close.
    public static EntityId? HitTest(IReadOnlyList<CanvasConnectionView> cables, CanvasPoint worldPoint, double radius)
    {
        EntityId? best = null;       // the closest cable found so far - none yet
        var bestDistance = radius;   // only cables closer than this count; it shrinks as closer cables are found

        // Check every cable
        foreach (var cable in cables)
        {
            // How far is the point from this cable's line? (DistanceToSegment, below)
            var distance = DistanceToSegment(worldPoint, cable.A, cable.B);

            // Closer than the best so far (and inside the radius) - this one wins, for now
            if (distance <= bestDistance)
            {
                bestDistance = distance;   // the new best distance
                best = cable.Id;           // the new best cable
            }
        }

        // The winner, or null when no cable was close enough
        return best;
    }

    // Purpose:       Measures the shortest distance from a point to a line segment (the piece of line from A to B).
    // Precondition:  receives p - the point; a, b - the two ends of the segment.
    // Postcondition: returns the distance (0 or more).
    private static double DistanceToSegment(CanvasPoint p, CanvasPoint a, CanvasPoint b)
    {
        // The segment as a direction: how far B is from A, on X and on Y
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;

        // The segment's length, squared (dx² + dy²) - we never need the real length, so no square root here
        var lengthSquared = dx * dx + dy * dy;

        // A and B are the same point (length 0) - then the distance is simply from p to A.
        // double.Epsilon = the tiniest number above 0 (comparing to it, not to 0, is safer with decimals)
        if (lengthSquared <= double.Epsilon)
        {
            return Distance(p, a);
        }

        // t = how far ALONG the segment the point closest to p is: 0 = at A, 1 = at B, 0.5 = in the middle.
        // The formula is the "projection" of p onto the line; Math.Clamp(..., 0, 1) keeps t ON the segment
        // (not past its ends) - beyond an end, the closest point is the end itself
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0.0, 1.0);

        // That closest point itself: start at A and walk t of the way towards B
        var nearest = new CanvasPoint(a.X + t * dx, a.Y + t * dy);

        // The distance from p to it
        return Distance(p, nearest);
    }

    // Purpose:       Measures the straight distance between two points.
    // Precondition:  receives p, q - the two points.
    // Postcondition: returns the distance (0 or more), by Pythagoras: the square root of (dx² + dy²).
    private static double Distance(CanvasPoint p, CanvasPoint q) =>
        Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y));
}
