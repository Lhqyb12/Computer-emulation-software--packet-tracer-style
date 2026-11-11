namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The kind of a network interface (a port on a device).
// "enum" = a closed list of named options; a variable of this type can only hold one of them
public enum InterfaceType
{
    Ethernet,          // old Ethernet - 10 Mbps
    FastEthernet,      // Fast Ethernet - 100 Mbps (most PC and switch ports)
    GigabitEthernet,   // Gigabit Ethernet - 1 Gbps (copper or fiber)
    Serial,            // a serial port - a WAN line between two routers
    Console,           // the management port - a cable from a PC to configure a router/switch
}
