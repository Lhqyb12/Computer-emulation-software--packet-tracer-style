namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// What a cable is made of. Chosen when the cable is created.
public enum ConnectionType
{
    Copper,     // a normal network cable (copper Ethernet)
    Fiber,      // fiber optic
    Serial,     // a serial line between two routers
    Wireless,   // wireless (later)
}
