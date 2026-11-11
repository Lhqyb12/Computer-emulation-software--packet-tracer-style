using NetSim.Core.Networking;   // InterfaceType

namespace NetSim.Core.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// A "plan" for one port a new device should have: just its name and its kind.
// The real port (NetworkInterface) is made from it later by NetworkDevice.AddInterface.
// "record" = a small data holder; the line in brackets makes the two properties Name and InterfaceType
public sealed record InterfaceDefinition(string Name, InterfaceType InterfaceType);
