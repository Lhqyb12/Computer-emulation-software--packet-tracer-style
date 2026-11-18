using NetSim.Core.Common;       // brings in EntityId - the cable's unique id
using NetSim.Core.Networking;   // brings in ConnectionType (the cable's kind) and ConnectionState (its link: up / down)

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// One cable as the canvas draws it (steps 6-8): a straight line from the middle of one device (A) to the middle of
// the other (B), in world points, plus a status light near each end (step 8).
// The devices are drawn ON TOP of the cables, so the part of the line inside each device box is hidden - the line
// seems to come out of the device's edge. The lights sit just OUTSIDE each box, so they are always seen.
// Nothing here is stored: it is worked out again from the network and the canvas every time, so a moved device
// always pulls its cable and its lights with it.
// "record" = holds data only; two with the same values count as equal; "sealed" = nothing inherits from it.
// The values in the brackets are given when it is built, in this order, and can only be read afterwards
public sealed record CanvasConnectionView(
    EntityId Id,                      // the cable's id (the same as the Connection in the network)
    CanvasPoint A,                    // the middle of the first device - where the line starts
    CanvasPoint B,                    // the middle of the second device - where the line ends
    ConnectionType ConnectionType,    // copper straight / copper crossover / fiber / serial - each drawn in its own style (step 7)
    ConnectionState State,            // the whole cable's link: Up or Down (step 7)
    CanvasPoint LightA,               // step 8: where the status light of the FIRST end is drawn (just outside the first device's box)
    CanvasPoint LightB,               // step 8: where the status light of the SECOND end is drawn (just outside the second device's box)
    bool IsEndAUp,                    // step 8: is the port at the first end working? true = green light, false = red light
    bool IsEndBUp);                   // step 8: is the port at the second end working? true = green, false = red
