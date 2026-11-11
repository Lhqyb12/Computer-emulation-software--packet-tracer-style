namespace NetSim.Core.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Is a device switched on. Every new device starts switched off.
public enum DeviceOperationalState
{
    PoweredOff,   // switched off
    PoweredOn,    // switched on
}
