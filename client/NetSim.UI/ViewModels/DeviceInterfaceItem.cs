using System;                    // brings in ArgumentNullException - the error for "nothing was passed in"
using NetSim.Core.Networking;    // brings in NetworkInterface (a port) and InterfaceTypeInfo (the port kinds' screen names)

namespace NetSim.UI.ViewModels;   // the "address" of this file: the ViewModels part of the UI layer

// One line in the Device Properties panel's port list (step 9): everything the screen shows about ONE port of the
// device, already turned into text.
// Taken from the reference project (there it sat at the bottom of DevicePropertiesViewModel.cs, and also showed VLANs,
// IPv6 and ARP - later steps). Here it lives in its own file and holds only what step 9 needs.
// It is a "snapshot": it copies the port's details when it is built. When a port changes, the panel builds new lines.
// "sealed" = no other class can inherit from it
public sealed class DeviceInterfaceItem
{
    // Purpose:       Builds the line of one port, turning its details into text for the screen.
    // Precondition:  receives networkInterface - the port to describe (not null).
    // Postcondition: returns nothing (a constructor) - every property below holds the port's details as text.
    public DeviceInterfaceItem(NetworkInterface networkInterface)
    {
        // If no port was passed in (null), stop right away with a clear error - it would be a bug in the caller
        ArgumentNullException.ThrowIfNull(networkInterface);

        Name = networkInterface.Name;                                             // the full name, e.g. "FastEthernet0/1"
        ShortName = networkInterface.ShortName;                                   // the short name, e.g. "Fa0/1"
        InterfaceTypeText = InterfaceTypeInfo.DisplayName(networkInterface.InterfaceType);   // the port kind's screen name, e.g. "Fast Ethernet"
        SpeedText = networkInterface.Speed.ToDisplayString();                     // its speed as text, e.g. "100 Mbps"

        // The port's physical (MAC) address as text. "?." = if the port has no MAC address (null), the whole thing is
        // null; "?? "-"" = then show a dash instead
        MacAddressText = networkInterface.MacAddress?.ToString() ?? "-";

        // Is the port working right now? IsOperational = turned on AND its state is Up (set by the Network, step 7)
        IsUp = networkInterface.IsOperational;

        // The status as a word, by priority ("a ? b : c ? d : e" = if a then b, otherwise if c then d, otherwise e):
        //   turned off by the user -> "Turned off"; working -> "Up"; otherwise -> "Down"
        StatusText = !networkInterface.IsEnabled ? "Turned off" : IsUp ? "Up" : "Down";

        // Where the port's cable goes. "is { } connection" = the port has a cable - keep it in "connection"
        if (networkInterface.Connection is { } connection)
        {
            // The port at the OTHER end of the cable (Connection.GetOtherEndpoint, step 1)
            var other = connection.GetOtherEndpoint(networkInterface);

            // e.g. "Switch0 Fa0/1" - the other device's name and the other port's short name
            ConnectedToText = $"{other.Device.Name} {other.ShortName}";
        }
        else
        {
            ConnectedToText = "Not connected";   // no cable in this port
        }
    }

    // The values the screen shows. Only "get" = set once in the constructor, never changed afterwards

    public string Name { get; }               // the port's full name
    public string ShortName { get; }          // the port's short name
    public string InterfaceTypeText { get; }  // the port kind, e.g. "Gigabit Ethernet"
    public string SpeedText { get; }          // the speed, e.g. "1 Gbps"
    public string MacAddressText { get; }     // the physical address, e.g. "02:1A:2B:3C:4D:5E"
    public bool IsUp { get; }                 // is the port working (the screen colors the status by it)
    public string StatusText { get; }         // "Up" / "Down" / "Turned off"
    public string ConnectedToText { get; }    // e.g. "Switch0 Fa0/1", or "Not connected"
}
