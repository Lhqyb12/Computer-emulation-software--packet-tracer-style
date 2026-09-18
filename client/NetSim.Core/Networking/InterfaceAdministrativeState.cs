namespace NetSim.Core.Networking;

/// <summary>
/// Whether an operator has administratively enabled a <see cref="NetworkInterface"/>. This is
/// distinct from <see cref="InterfaceOperationalState"/>: an interface can be administratively
/// <see cref="Enabled"/> yet still operationally <see cref="InterfaceOperationalState.Down"/>
/// (enabled but no active link), and a <see cref="Disabled"/> interface is never operational
/// regardless of what is cabled to it. The CLI equivalents ("shutdown" / "no shutdown") arrive
/// in a later phase - this phase only models the state and the Enable/Disable operations.
/// </summary>
public enum InterfaceAdministrativeState
{
    Enabled,
    Disabled,
}
