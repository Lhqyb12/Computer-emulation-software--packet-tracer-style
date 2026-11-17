using NetSim.Core.Common;       // EntityId
using NetSim.Core.Networking;   // ConnectionType, ConnectionState

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// One cable as the canvas draws it (step 6): a straight line from the middle of one device (A) to the
// middle of the other (B), in world points. The devices are drawn ON TOP of the cables, so the part of
// the line inside each device box is hidden - the line seems to come out of the device's edge.
// Nothing is stored: it is worked out again from the network and the canvas every time, so a moved
// device always pulls its cables with it.
// "record" = holds data only; two with the same values count as equal
public sealed record CanvasConnectionView(
    EntityId Id,                      // the cable's id (the same as the Connection in the network)
    CanvasPoint A,                    // the middle of the first device
    CanvasPoint B,                    // the middle of the second device
    ConnectionType ConnectionType,    // copper / fiber / serial - later drawn in different styles (step 7)
    ConnectionState State);           // up / down - later shown as green / red lights (step 8)
