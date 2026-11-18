namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// One line in the "choose a port" list the Connect tool opens on a device (step 7).
// "record" = holds data only; "sealed" = nothing inherits from it
public sealed record PortOption(
    string Name,          // the port's full name, e.g. "FastEthernet0/1" - used to find the port again
    string ShortName,     // the short name shown in bold, e.g. "Fa0/1"
    bool IsSelectable,    // can this port take the chosen cable right now (free, turned on, the right plug)
    string Status);       // why / why not, shown next to it, e.g. "free", "connected", "can't take Fiber"
