namespace NetSim.Core.Devices;

/// <summary>
/// The power/operational state of a <see cref="NetworkDevice"/>. Deliberately minimal ג€”
/// future phases may introduce transitional states (e.g. booting) once the simulation
/// engine needs them.
/// </summary>
// Every device starts PoweredOff and can be switched PoweredOn/PoweredOff.
// No in-between states yet (like "booting") - kept minimal on purpose.
public enum DeviceOperationalState
{
    PoweredOff,
    PoweredOn,
}
