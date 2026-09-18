using NetSim.Core.Networking;

namespace NetSim.Core.Dhcp;

/// <summary>
/// How a DHCP server tells two clients apart (brief section 21). An RFC 2131 client identifier is a
/// hardware-type byte followed by the client's link-layer address; when a client sends no explicit
/// option 61 the server falls back to the frame's <c>chaddr</c>, which
/// <see cref="FromHardwareAddress"/> models. Value type with value-based equality, so it is a safe
/// dictionary key - the requirement the brief calls out ("two clients must not accidentally receive
/// the same lease because the server cannot distinguish them").
/// </summary>
public readonly record struct DhcpClientId
{
    private DhcpClientId(byte hardwareType, MacAddress hardwareAddress)
    {
        HardwareType = hardwareType;
        HardwareAddress = hardwareAddress;
    }

    /// <summary>The BOOTP <c>htype</c> (1 = Ethernet) - part of the identity so a future non-Ethernet medium never collides.</summary>
    public byte HardwareType { get; }

    /// <summary>The client's link-layer address.</summary>
    public MacAddress HardwareAddress { get; }

    /// <summary>Builds an Ethernet client identifier from a MAC address (the common case).</summary>
    public static DhcpClientId FromHardwareAddress(MacAddress hardwareAddress) =>
        new(DhcpProtocol.EthernetHardwareType, hardwareAddress);

    /// <summary>Builds a client identifier for an explicit hardware type.</summary>
    public static DhcpClientId Create(byte hardwareType, MacAddress hardwareAddress) => new(hardwareType, hardwareAddress);

    public override string ToString() => $"{HardwareType:X2}:{HardwareAddress}";
}
