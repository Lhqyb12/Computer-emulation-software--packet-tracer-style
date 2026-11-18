namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The kind of a cable. Chosen when the cable is created (step 7: the user picks it in the toolbar,
// or "Automatic" picks the right one).
// Change in step 7: the old single "Copper" became two kinds - like real Ethernet cables and Packet Tracer:
//   straight-through = between DIFFERENT kinds of device (PC - switch, switch - router);
//   crossover        = between devices of the SAME kind (PC - PC, switch - switch, router - router, PC - router).
// Plugging the wrong copper kind is allowed (it fits the port), but the link stays DOWN - see CableRules.
public enum ConnectionType
{
    CopperStraightThrough,   // a normal copper network cable, wires straight through
    CopperCrossover,         // a copper network cable with the send / receive wires crossed
    Fiber,                   // fiber optic (only Gigabit ports take it)
    Serial,                  // a serial line between two routers (only Serial ports take it)
    Wireless,                // wireless (later)
}
