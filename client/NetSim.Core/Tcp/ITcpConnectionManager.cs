using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Transport;

namespace NetSim.Core.Tcp;

/// <summary>
/// Manages every simulated TCP connection and listening socket (brief section 27): who is
/// listening, which connections are active, and the state-machine decision for each inbound
/// segment. This is where the three-way handshake, data-transfer sequencing, termination and reset
/// behaviour actually live - <see cref="ITcpLayer"/> only gates a segment on structural validity,
/// checksum and destination ownership before handing it here. See <see cref="TcpConnectionManager"/>
/// for the full state-transition table this phase supports.
/// </summary>
public interface ITcpConnectionManager
{
    /// <summary>Binds <paramref name="device"/> to <paramref name="port"/> as a passive-open listener. Throws if already listening on that port.</summary>
    TcpListener Listen(NetworkDevice device, Port port, Action<TcpConnection, IPacketPayload>? onDataReceived = null);

    /// <summary>Removes the listener for <paramref name="device"/> on <paramref name="port"/>. Returns false when there was none. Existing connections already accepted from it are unaffected.</summary>
    bool StopListening(NetworkDevice device, Port port);

    bool IsListening(NetworkDevice device, Port port);

    /// <summary>
    /// Active open (brief section 12/38): creates a new connection to <paramref name="remoteAddress"/>:<paramref name="remotePort"/>
    /// from <paramref name="localInterface"/>'s primary IPv4 address and <paramref name="localPort"/>,
    /// moves it to <see cref="TcpConnectionState.SynSent"/>, and returns it together with the SYN
    /// segment the caller must transmit. Throws if a connection with that exact tuple already exists.
    /// </summary>
    TcpConnectResult Connect(
        NetworkInterface localInterface, IPv4Address remoteAddress, Port remotePort, Port localPort,
        Action<TcpConnection, IPacketPayload>? onDataReceived = null);

    TcpConnection? FindConnection(TcpConnectionKey key);

    /// <summary>Every connection currently tracked for <paramref name="device"/>, in any state - basic transport diagnostics (brief section 33).</summary>
    IReadOnlyCollection<TcpConnection> GetConnections(NetworkDevice device);

    /// <summary>
    /// Runs one already-validated (structure, checksum, destination ownership) inbound
    /// <paramref name="segment"/> - which arrived on <paramref name="localInterface"/> addressed to
    /// <paramref name="localAddress"/> from <paramref name="remoteAddress"/> - through the connection
    /// state machine. Never throws for an expected protocol condition (no listener, wrong state,
    /// unexpected sequence number); every such case is reported through the returned
    /// <see cref="TcpProcessingReport"/>.
    /// </summary>
    TcpProcessingReport AcceptSegment(NetworkInterface localInterface, IPv4Address localAddress, IPv4Address remoteAddress, TcpSegment segment);

    /// <summary>Builds a data segment on an <see cref="TcpConnectionState.Established"/> connection and advances its local sequence number. Throws if the connection is not established.</summary>
    TcpSegment Send(TcpConnection connection, IPacketPayload payload);

    /// <summary>Begins normal termination (brief section 17): builds a FIN(+ACK) and moves the connection to <see cref="TcpConnectionState.FinWait1"/> or <see cref="TcpConnectionState.LastAck"/> as appropriate. Throws if the connection cannot be closed from its current state.</summary>
    TcpSegment Close(TcpConnection connection);

    /// <summary>Aborts the connection immediately (brief section 18): builds a RST, moves it to <see cref="TcpConnectionState.Closed"/> and removes it from the tracked set.</summary>
    TcpSegment Reset(TcpConnection connection);

    /// <summary>Removes a tracked connection (e.g. after it lingers in <see cref="TcpConnectionState.TimeWait"/> - no simulated timer exists yet to do this automatically). Returns false when there was none.</summary>
    bool RemoveConnection(TcpConnectionKey key);

    event EventHandler<TcpConnectionEventArgs>? ConnectionRequested;

    event EventHandler<TcpConnectionEventArgs>? SynSent;

    event EventHandler<TcpConnectionEventArgs>? SynReceived;

    event EventHandler<TcpConnectionEventArgs>? SynAckSent;

    event EventHandler<TcpConnectionEventArgs>? AckSent;

    event EventHandler<TcpConnectionEventArgs>? AckProcessed;

    event EventHandler<TcpConnectionEventArgs>? ConnectionEstablished;

    event EventHandler<TcpConnectionEventArgs>? SegmentSent;

    event EventHandler<TcpConnectionEventArgs>? DataDelivered;

    event EventHandler<TcpConnectionEventArgs>? FinSent;

    event EventHandler<TcpConnectionEventArgs>? FinReceived;

    event EventHandler<TcpConnectionEventArgs>? ConnectionClosing;

    event EventHandler<TcpConnectionEventArgs>? ConnectionClosed;

    event EventHandler<TcpConnectionEventArgs>? ConnectionRefused;

    /// <summary>Raised when a connection is torn down by a RST - received from the peer, or generated locally by <see cref="Reset"/>. Named to avoid colliding with the <see cref="Reset"/> method.</summary>
    event EventHandler<TcpConnectionEventArgs>? ConnectionReset;
}
