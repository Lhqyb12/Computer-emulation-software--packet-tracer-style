using NetSim.Core.Networking;

namespace NetSim.Core.Transport;

/// <summary>
/// A logical transport-layer communication endpoint: an IPv4 address, a <see cref="Transport.Port"/>
/// and a <see cref="TransportProtocol"/>, e.g. <c>192.168.1.20:80/Tcp</c>. This is deliberately
/// distinct from a <see cref="NetworkInterface"/> (the device's physical/L2 network connection) -
/// several transport endpoints (many ports, both protocols) can exist on top of one interface's
/// single IPv4 address, and a device with several interfaces still has one flat space of endpoints.
///
/// A <c>readonly record struct</c> so two endpoints compare by value (brief section 6: "support
/// comparison/equality") - the same address and port on different protocols are deliberately
/// unequal, since a UDP and a TCP socket on the same port are entirely independent in real
/// networking.
/// </summary>
public readonly record struct TransportEndpoint(IPv4Address Address, Port Port, TransportProtocol Protocol)
{
    public override string ToString() => $"{Address}:{Port.Value}/{Protocol.ToString().ToUpperInvariant()}";
}
