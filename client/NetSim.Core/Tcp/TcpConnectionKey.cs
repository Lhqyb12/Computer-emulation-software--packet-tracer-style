using NetSim.Core.Networking;
using NetSim.Core.Transport;

namespace NetSim.Core.Tcp;

/// <summary>
/// The standard TCP connection tuple (brief section 26): local/remote IPv4 address and port. A
/// connection is identified by all four values together, never by port alone - two different
/// clients connecting to the same listening port are two entirely separate connections. A
/// <c>readonly record struct</c> so it compares by value and works directly as a dictionary key in
/// <see cref="TcpConnectionManager"/>.
/// </summary>
public readonly record struct TcpConnectionKey(IPv4Address LocalAddress, Port LocalPort, IPv4Address RemoteAddress, Port RemotePort)
{
    /// <summary>Always TCP - this type only ever identifies a TCP connection (UDP has no connection concept to key).</summary>
    public TransportProtocol Protocol => TransportProtocol.Tcp;

    public override string ToString() => $"{LocalAddress}:{LocalPort} <-> {RemoteAddress}:{RemotePort} (TCP)";
}
