using System;                      // brings in Math (Clamp, Sqrt, Abs, Min)
using System.Collections.Generic;  // brings in List<T> and IReadOnlyList<T>
using NetSim.Core.Common;          // brings in EntityId - a cable's unique id
using NetSim.Core.Topology;        // brings in Network - all the devices and cables

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// Where the cables are on the canvas (steps 6-8): builds a drawable line for every cable of the network - with a
// status light just outside each device (step 8) - and finds the cable under the mouse (step 4).
// New - in the reference project this lived inside the screen's view model; here it is in the Application layer,
// so the mouse logic (CanvasInteractionController) can use it too.
// "static class" = no objects; call it straight through the class: CanvasConnectionGeometry.Build(...)
public static class CanvasConnectionGeometry
{
    // Step 8: how far OUTSIDE the device's box the status light sits, in world units.
    // "const" = a fixed number. 8 = far enough to be clearly outside the box's border, close enough to belong to the device
    private const double LightGap = 8;

    // Purpose:       Builds one line for every cable whose two devices are both on the canvas.
    // Precondition:  receives network - the open network (may be null when no project is open);
    //                items - the canvas positions of the devices.
    // Postcondition: returns the lines (each from one device's middle to the other's, with a status light near each end),
    //                or an empty list when there is no network or no cable. Nothing is changed.
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

            // Step 8: where each status light goes - just outside its own device's box, on the line towards the other device.
            // For the first end: start from A's middle, head towards B's middle, A's box size decides where the edge is
            var lightA = LightPoint(itemA.Center, itemB.Center, itemA.Size);

            // For the second end: the same, the other way round - start from B, head towards A, B's box
            var lightB = LightPoint(itemB.Center, itemA.Center, itemB.Size);

            // Build the line with all nine values, in the order CanvasConnectionView lists them:
            //   the cable's id, the two middles, its kind, its link state, the two light points, and whether each end's
            //   port is working. IsOperational (a port's own property, step 1) = the port is turned on AND its state is Up
            views.Add(new CanvasConnectionView(
                connection.Id,                         // the cable's id
                itemA.Center,                          // where the line starts: the first device's middle
                itemB.Center,                          // where the line ends: the second device's middle
                connection.ConnectionType,             // the cable's kind (for its style)
                connection.State,                      // the whole cable's link: Up / Down
                lightA,                                // step 8: the first end's light point
                lightB,                                // step 8: the second end's light point
                connection.EndpointA.IsOperational,    // step 8: is the first end's port working? (green / red)
                connection.EndpointB.IsOperational));  // step 8: is the second end's port working?
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

    // Purpose:       Step 8 - finds where a cable's status light goes: the point where the line leaves the device's box,
    //                pushed a little further out (LightGap), so the light is never hidden under the device.
    // Precondition:  receives center - the middle of the device this end belongs to; towards - the middle of the device at
    //                the OTHER end (the line heads that way); boxSize - the size of this end's device box.
    // Postcondition: returns the light's point, in the world. When both middles are the same point (no direction), returns center.
    private static CanvasPoint LightPoint(CanvasPoint center, CanvasPoint towards, CanvasSize boxSize)
    {
        // STEP 1 - the direction: how far the other device is from this one, across (dx) and down (dy)
        var dx = towards.X - center.X;
        var dy = towards.Y - center.Y;

        // How long that direction is (Pythagoras: the square root of dx² + dy²)
        var length = Math.Sqrt(dx * dx + dy * dy);

        // Both middles are the same point - there is no direction to walk in, so just use the middle
        if (length <= double.Epsilon)
        {
            return center;
        }

        // STEP 2 - walk from the middle along the direction until the line hits the box's border.
        // "t" = which part of the whole direction (dx, dy) we walk: t = 0.5 means half of it.
        // To reach the LEFT / RIGHT side we must walk half the width across: t = (width / 2) / |dx|.
        // To reach the TOP / BOTTOM side we must walk half the height down: t = (height / 2) / |dy|.
        // Math.Abs = the size of a number without its sign (-30 -> 30). When dx is 0 the line goes straight up / down and
        // never reaches a side, so that t is "endless" (double.PositiveInfinity) - the same for dy = 0
        var tSide = dx == 0 ? double.PositiveInfinity : (boxSize.Width / 2) / Math.Abs(dx);
        var tTopBottom = dy == 0 ? double.PositiveInfinity : (boxSize.Height / 2) / Math.Abs(dy);

        // The line hits whichever border comes FIRST - the smaller of the two
        var tEdge = Math.Min(tSide, tTopBottom);

        // The point on the border: the middle + that part of the direction
        var edgeX = center.X + tEdge * dx;
        var edgeY = center.Y + tEdge * dy;

        // STEP 3 - push LightGap more units outwards along the same direction.
        // dx / length and dy / length = the direction shrunk to exactly 1 unit long, so "* LightGap" moves exactly 8 units
        return new CanvasPoint(
            edgeX + dx / length * LightGap,    // the light's X
            edgeY + dy / length * LightGap);   // the light's Y
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

