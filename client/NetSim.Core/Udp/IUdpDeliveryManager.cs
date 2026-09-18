using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Transport;

namespace NetSim.Core.Udp;

/// <summary>The outcome of attempting to deliver a datagram to a device's bound endpoints (brief section 28).</summary>
public sealed record UdpDeliveryResult(bool IsDelivered, UdpBinding? Binding)
{
    public static UdpDeliveryResult Delivered(UdpBinding binding) => new(true, binding);

    public static UdpDeliveryResult PortUnavailable { get; } = new(false, null);
}

/// <summary>
/// A clean, UI-independent mechanism for UDP endpoint registration and datagram delivery (brief
/// section 28): bind a device to a port, look the binding up again, and hand a datagram to whatever
/// is bound - or report that the destination port is unavailable. Host-scoped (keyed by device, not
/// by interface) since a device's port space is shared across every interface it owns. Holds no
/// protocol logic of its own (checksum/structural validation stays in <see cref="UdpProcessor"/> /
/// <see cref="IUdpLayer"/>) - purely the socket-table half of the UDP engine.
/// </summary>
public interface IUdpDeliveryManager
{
    /// <summary>
    /// Binds <paramref name="device"/> to <paramref name="port"/>, optionally with a callback
    /// invoked for every datagram delivered to it. Throws <see cref="Common.Exceptions.DomainException"/>
    /// if the device is already bound to that port.
    /// </summary>
    UdpBinding Bind(NetworkDevice device, Port port, Action<UdpReceivedDatagram>? onReceived = null);

    /// <summary>Removes the binding for <paramref name="device"/> on <paramref name="port"/>. Returns false when there was none.</summary>
    bool Unbind(NetworkDevice device, Port port);

    /// <summary>True when <paramref name="device"/> has a live binding on <paramref name="port"/>.</summary>
    bool IsBound(NetworkDevice device, Port port);

    bool TryFindBinding(NetworkDevice device, Port port, [NotNullWhen(true)] out UdpBinding? binding);

    /// <summary>Every endpoint currently bound on <paramref name="device"/> - basic transport diagnostics (brief section 33).</summary>
    IReadOnlyCollection<UdpBinding> GetBindings(NetworkDevice device);

    /// <summary>
    /// Delivers <paramref name="datagram"/> (received from <paramref name="remoteAddress"/>:<paramref name="remotePort"/>)
    /// to whatever is bound to <paramref name="device"/>'s <see cref="UdpDatagram.DestinationPort"/>.
    /// Never throws for a missing binding - returns <see cref="UdpDeliveryResult.PortUnavailable"/>.
    /// </summary>
    UdpDeliveryResult Deliver(NetworkDevice device, UdpDatagram datagram, IPv4Address remoteAddress, Port remotePort);
}
