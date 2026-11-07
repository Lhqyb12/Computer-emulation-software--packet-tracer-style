namespace NetSim.UI.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The groups the Device Library shows as filter buttons. Only the screen needs groups - the
// network itself doesn't care - so this lives in the UI layer, not in Core.
// Some groups have no devices yet (Wireless, Security, Other); they are shown anyway, so the
// buttons are always the same
public enum DeviceCategory
{
    EndDevices,       // devices a person uses: PC, laptop, server
    NetworkDevices,   // devices that move traffic: router, switch
    Wireless,         // wireless devices (none yet)
    Security,         // security devices, e.g. a firewall (none yet)
    Other,            // anything else
}
