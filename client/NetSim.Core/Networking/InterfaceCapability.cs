namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Which kinds of cable an interface can physically take.
// [Flags] = one value can hold SEVERAL options together (e.g. Ethernet | Fiber),
// because one port can accept more than one kind of cable.
// Two ports can be cabled only when they share at least one option.
[Flags]
public enum InterfaceCapability
{
    None = 0,             // no option at all
    Ethernet = 1 << 0,    // copper Ethernet. "1 << 0" = binary 00001 = 1
    Serial = 1 << 1,      // serial line.     "1 << 1" = binary 00010 = 2
    Console = 1 << 2,     // console cable.   "1 << 2" = binary 00100 = 4
    Fiber = 1 << 3,       // fiber optic.     "1 << 3" = binary 01000 = 8
    Wireless = 1 << 4,    // wireless.        "1 << 4" = binary 10000 = 16
}
