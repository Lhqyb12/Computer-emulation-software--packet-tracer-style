using NetSim.Core.Devices;
using NetSim.Core.Packets;
using NetSim.Core.Transport;

namespace NetSim.Core.Tcp;

/// <summary>
/// A bound, listening TCP server socket (brief section 19): a device and a port, waiting to accept
/// connections. Modelled as its own small type rather than a <see cref="TcpConnection"/> in the
/// <see cref="TcpConnectionState.Listen"/> state, because a listening socket has no remote
/// address/port yet - a real <see cref="TcpConnectionKey"/> genuinely does not exist until a SYN
/// arrives and one is spawned from it. <see cref="TcpConnectionState.Listen"/> still exists on the
/// enum (the brief asks every state be represented) and is exactly what this type conceptually is.
/// </summary>
public sealed class TcpListener
{
    internal TcpListener(NetworkDevice device, Port port, Action<TcpConnection, IPacketPayload>? onDataReceived)
    {
        Device = device;
        Port = port;
        OnDataReceived = onDataReceived;
    }

    public NetworkDevice Device { get; }

    public Port Port { get; }

    /// <summary>Copied onto every <see cref="TcpConnection"/> spawned by an incoming SYN on this listener.</summary>
    public Action<TcpConnection, IPacketPayload>? OnDataReceived { get; }

    public TcpConnectionState State => TcpConnectionState.Listen;

    public override string ToString() => $"{Device.Name}:{Port}/TCP [Listen]";
}
