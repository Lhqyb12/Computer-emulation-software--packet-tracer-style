namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Is the port really working right now (up = turned on AND a working cable is plugged in).
// Later this decides the green / red light on the cable end.
public enum InterfaceOperationalState
{
    Down,   // not working (turned off, or no cable)
    Up,     // working
}
