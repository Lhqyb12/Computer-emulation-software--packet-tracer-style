namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Who a MAC address is sent to. A switch (later, in Part 4) decides what to do with a frame by this.
// Exactly one option per address: the broadcast address counts as Broadcast, not as Multicast.
public enum MacAddressKind
{
    Unicast,     // one single port. The lowest bit of the first byte is 0
    Multicast,   // a group of ports. The lowest bit of the first byte is 1 (and it is not the broadcast address)
    Broadcast,   // every port on the network - FF:FF:FF:FF:FF:FF
}
