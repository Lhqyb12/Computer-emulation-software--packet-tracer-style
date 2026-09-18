using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Transport;

namespace NetSim.Core.Udp;

/// <summary>
/// The information delivered to a bound endpoint's receive callback: the datagram itself plus who
/// it came from. Kept separate from <see cref="UdpDatagram"/> because the datagram's own header has
/// no notion of the sender's IP address (only its source port) - the address comes from the
/// enclosing IPv4 packet, exactly as in real UDP.
/// </summary>
public sealed record UdpReceivedDatagram(UdpDatagram Datagram, IPv4Address RemoteAddress, Port RemotePort);

/// <summary>
/// A UDP endpoint bound on a device (brief section 20). Host-scoped, not interface-scoped - a
/// device may have several interfaces but the port space is shared across all of them, exactly as
/// section 36 describes. Tracks a small receive history for diagnostics (section 33) plus an
/// optional callback so a future application protocol (DNS in Phase 23, DHCP in Phase 24) can react
/// to arriving datagrams without the transport layer knowing anything about it.
/// </summary>
public sealed class UdpBinding
{
    private readonly List<UdpReceivedDatagram> _received = [];

    internal UdpBinding(NetworkDevice device, Port port, Action<UdpReceivedDatagram>? onReceived)
    {
        Device = device;
        Port = port;
        OnReceived = onReceived;
    }

    public NetworkDevice Device { get; }

    public Port Port { get; }

    /// <summary>Invoked (if provided) for every datagram delivered to this binding, in addition to <see cref="ReceivedDatagrams"/>.</summary>
    public Action<UdpReceivedDatagram>? OnReceived { get; }

    /// <summary>Every datagram delivered to this binding so far, oldest first - basic transport diagnostics (brief section 33).</summary>
    public IReadOnlyList<UdpReceivedDatagram> ReceivedDatagrams => _received.AsReadOnly();

    internal void Deliver(UdpReceivedDatagram received)
    {
        _received.Add(received);
        OnReceived?.Invoke(received);
    }

    public override string ToString() => $"{Device.Name}:{Port}/UDP";
}
