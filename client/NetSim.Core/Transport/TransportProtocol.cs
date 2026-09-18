using NetSim.Core.IP;

namespace NetSim.Core.Transport;

/// <summary>
/// The two transport-layer protocols this phase implements. A small closed enum (unlike
/// <see cref="ProtocolNumber"/>, which must represent any IPv4 protocol number including ones this
/// simulation never implements) because <see cref="Core.Tcp"/> and <see cref="Core.Udp"/> are the
/// only transports a <see cref="TransportEndpoint"/> or a connection/binding table can ever be keyed
/// on - there is no "unknown transport protocol" case to represent here the way there is for
/// <see cref="ProtocolNumber"/>.
/// </summary>
public enum TransportProtocol
{
    Tcp,
    Udp,
}

/// <summary>Conversions between <see cref="TransportProtocol"/> and the IPv4 <see cref="ProtocolNumber"/> that carries it on the wire.</summary>
public static class TransportProtocolExtensions
{
    /// <summary>The IPv4 protocol number a segment/datagram of this transport is carried under (TCP=6, UDP=17).</summary>
    public static ProtocolNumber ToProtocolNumber(this TransportProtocol protocol) => protocol switch
    {
        TransportProtocol.Tcp => ProtocolNumber.Tcp,
        TransportProtocol.Udp => ProtocolNumber.Udp,
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Unknown transport protocol."),
    };

    /// <summary>The reverse of <see cref="ToProtocolNumber"/>: null when <paramref name="protocol"/> is neither TCP nor UDP.</summary>
    public static TransportProtocol? FromProtocolNumber(ProtocolNumber protocol)
    {
        if (protocol == ProtocolNumber.Tcp)
        {
            return TransportProtocol.Tcp;
        }

        if (protocol == ProtocolNumber.Udp)
        {
            return TransportProtocol.Udp;
        }

        return null;
    }
}
