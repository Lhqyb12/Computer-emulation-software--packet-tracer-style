namespace NetSim.Core.Networking;

/// <summary>
/// Represents the kind of a <see cref="NetworkInterface"/>. Addressing (IP/MAC) and
/// medium-specific behavior are intentionally not modeled yet.
/// </summary>
public enum InterfaceType
{
    Ethernet,
    FastEthernet,
    GigabitEthernet,
    Serial,
    Console,
}
