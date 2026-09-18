using System.Linq;
using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Transport;

namespace NetSim.Core.Tcp;

/// <summary>
/// Default <see cref="ITcpConnectionManager"/>. Owns two plain socket-table dictionaries (listeners
/// keyed by device+port, connections keyed by the full <see cref="TcpConnectionKey"/> four-tuple -
/// brief section 26) and drives every state transition described below. Nothing outside this class
/// mutates a <see cref="TcpConnection"/>.
///
/// <para><b>Supported state transitions</b> (brief section 11 - deliberately not every real-world
/// edge case; see <see cref="TcpConnection"/>'s own transition table for the enforced version):</para>
/// <list type="bullet">
/// <item>Closed -&gt; SynSent: <see cref="Connect"/> (active open).</item>
/// <item>Closed -&gt; SynReceived: a bare SYN matches a <see cref="TcpListener"/> (passive open) - a
/// new connection is created directly in SynReceived, exactly like a real kernel's SYN backlog.</item>
/// <item>SynSent -&gt; Established: a matching SYN+ACK arrives; a final ACK is generated.</item>
/// <item>SynReceived -&gt; Established: the final ACK arrives.</item>
/// <item>Established -&gt; FinWait1: local <see cref="Close"/>.</item>
/// <item>Established -&gt; CloseWait: a FIN arrives from the remote side; an ACK is generated.</item>
/// <item>FinWait1 -&gt; FinWait2: an ACK for the local FIN arrives.</item>
/// <item>FinWait1 -&gt; Closing / TimeWait: a FIN arrives before the local FIN was acknowledged
/// (simultaneous close) - Closing if it did not also acknowledge the local FIN, TimeWait if it did.</item>
/// <item>FinWait2 -&gt; TimeWait: the remote side's FIN arrives; an ACK is generated.</item>
/// <item>Closing -&gt; TimeWait: the delayed ACK for the local FIN arrives.</item>
/// <item>CloseWait -&gt; LastAck: local <see cref="Close"/> after already receiving the remote FIN.</item>
/// <item>LastAck -&gt; Closed: the final ACK arrives - the connection is removed automatically.</item>
/// <item>TimeWait -&gt; Closed: never automatic (brief section 32 - no simulated clock/timer exists
/// yet for a real 2MSL wait) - <see cref="RemoveConnection"/> performs it explicitly.</item>
/// <item>Any open state -&gt; Closed: a RST, received or sent via <see cref="Reset"/>.</item>
/// </list>
/// </summary>
public sealed class TcpConnectionManager : ITcpConnectionManager
{
    private readonly Dictionary<(EntityId DeviceId, Port Port), TcpListener> _listeners = [];
    private readonly Dictionary<TcpConnectionKey, TcpConnection> _connections = [];
    private readonly IInitialSequenceNumberGenerator _isnGenerator;

    public TcpConnectionManager(IInitialSequenceNumberGenerator? isnGenerator = null)
    {
        _isnGenerator = isnGenerator ?? new RandomInitialSequenceNumberGenerator();
    }

    public event EventHandler<TcpConnectionEventArgs>? ConnectionRequested;

    public event EventHandler<TcpConnectionEventArgs>? SynSent;

    public event EventHandler<TcpConnectionEventArgs>? SynReceived;

    public event EventHandler<TcpConnectionEventArgs>? SynAckSent;

    public event EventHandler<TcpConnectionEventArgs>? AckSent;

    public event EventHandler<TcpConnectionEventArgs>? AckProcessed;

    public event EventHandler<TcpConnectionEventArgs>? ConnectionEstablished;

    public event EventHandler<TcpConnectionEventArgs>? SegmentSent;

    public event EventHandler<TcpConnectionEventArgs>? DataDelivered;

    public event EventHandler<TcpConnectionEventArgs>? FinSent;

    public event EventHandler<TcpConnectionEventArgs>? FinReceived;

    public event EventHandler<TcpConnectionEventArgs>? ConnectionClosing;

    public event EventHandler<TcpConnectionEventArgs>? ConnectionClosed;

    public event EventHandler<TcpConnectionEventArgs>? ConnectionRefused;

    public event EventHandler<TcpConnectionEventArgs>? ConnectionReset;

    // ---- Listeners ----

    public TcpListener Listen(NetworkDevice device, Port port, Action<TcpConnection, IPacketPayload>? onDataReceived = null)
    {
        ArgumentNullException.ThrowIfNull(device);

        var key = (device.Id, port);
        if (_listeners.ContainsKey(key))
        {
            throw new DomainException($"Device '{device.Name}' is already listening on TCP port {port}.");
        }

        var listener = new TcpListener(device, port, onDataReceived);
        _listeners[key] = listener;
        return listener;
    }

    public bool StopListening(NetworkDevice device, Port port)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _listeners.Remove((device.Id, port));
    }

    public bool IsListening(NetworkDevice device, Port port)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _listeners.ContainsKey((device.Id, port));
    }

    private bool TryFindListener(NetworkDevice device, Port port, out TcpListener? listener) =>
        _listeners.TryGetValue((device.Id, port), out listener);

    // ---- Connections ----

    public TcpConnectResult Connect(
        NetworkInterface localInterface, IPv4Address remoteAddress, Port remotePort, Port localPort,
        Action<TcpConnection, IPacketPayload>? onDataReceived = null)
    {
        ArgumentNullException.ThrowIfNull(localInterface);

        if (localInterface.PrimaryIPv4Configuration is not { } localConfig)
        {
            throw new DomainException(
                $"Interface '{localInterface.Name}' on '{localInterface.Device.Name}' has no IPv4 address to open a TCP connection from.");
        }

        var localAddress = localConfig.Address;
        var key = new TcpConnectionKey(localAddress, localPort, remoteAddress, remotePort);
        if (_connections.ContainsKey(key))
        {
            throw new DomainException($"A TCP connection {key} already exists.");
        }

        var connection = new TcpConnection(key, localInterface.Device, isServerSide: false, onDataReceived);
        var isn = _isnGenerator.Next();
        connection.SetLocalInitialSequence(isn);
        connection.TransitionTo(TcpConnectionState.SynSent);
        _connections[key] = connection;

        var syn = TcpSegment.CreateSyn(localAddress, remoteAddress, localPort, remotePort, isn);
        connection.AdvanceLocalSequence(syn.SequenceLength);

        ConnectionRequested?.Invoke(this, new TcpConnectionEventArgs(connection, syn, detail: $"Active open to {remoteAddress}:{remotePort}."));
        SynSent?.Invoke(this, new TcpConnectionEventArgs(connection, syn));

        return new TcpConnectResult(connection, syn);
    }

    public TcpConnection? FindConnection(TcpConnectionKey key) => _connections.GetValueOrDefault(key);

    public IReadOnlyCollection<TcpConnection> GetConnections(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _connections.Values.Where(c => c.Device.Id == device.Id).ToList().AsReadOnly();
    }

    public bool RemoveConnection(TcpConnectionKey key)
    {
        if (!_connections.TryGetValue(key, out var connection))
        {
            return false;
        }

        if (connection.State == TcpConnectionState.TimeWait)
        {
            connection.TransitionTo(TcpConnectionState.Closed);
            ConnectionClosed?.Invoke(this, new TcpConnectionEventArgs(
                connection, detail: "TimeWait elapsed (no simulated timer exists yet - removed explicitly)."));
        }

        return _connections.Remove(key);
    }

    // ---- Local sender-side operations ----

    public TcpSegment Send(TcpConnection connection, IPacketPayload payload)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(payload);

        if (connection.State != TcpConnectionState.Established)
        {
            throw new DomainException($"Cannot send data on {connection.Key}: connection is {connection.State}, not Established.");
        }

        var segment = TcpSegment.CreatePushAck(
            connection.Key.LocalAddress, connection.Key.RemoteAddress, connection.Key.LocalPort, connection.Key.RemotePort,
            connection.LocalNextSequenceNumber, connection.RemoteNextSequenceNumber, payload);
        connection.AdvanceLocalSequence(segment.SequenceLength);

        SegmentSent?.Invoke(this, new TcpConnectionEventArgs(connection, segment, payload));
        return segment;
    }

    public TcpSegment Close(TcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.State is not (TcpConnectionState.Established or TcpConnectionState.CloseWait))
        {
            throw new DomainException($"Cannot close {connection.Key} from state {connection.State}.");
        }

        var wasEstablished = connection.State == TcpConnectionState.Established;
        var fin = TcpSegment.CreateFin(
            connection.Key.LocalAddress, connection.Key.RemoteAddress, connection.Key.LocalPort, connection.Key.RemotePort,
            connection.LocalNextSequenceNumber, connection.RemoteNextSequenceNumber);
        connection.AdvanceLocalSequence(fin.SequenceLength);
        connection.TransitionTo(wasEstablished ? TcpConnectionState.FinWait1 : TcpConnectionState.LastAck);

        FinSent?.Invoke(this, new TcpConnectionEventArgs(connection, fin));
        if (wasEstablished)
        {
            ConnectionClosing?.Invoke(this, new TcpConnectionEventArgs(connection, fin));
        }

        return fin;
    }

    public TcpSegment Reset(TcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var rst = TcpSegment.CreateReset(
            connection.Key.LocalAddress, connection.Key.RemoteAddress, connection.Key.LocalPort, connection.Key.RemotePort,
            connection.LocalNextSequenceNumber, connection.RemoteNextSequenceNumber);
        connection.TransitionTo(TcpConnectionState.Closed);
        _connections.Remove(connection.Key);

        ConnectionReset?.Invoke(this, new TcpConnectionEventArgs(connection, rst, detail: "Reset sent locally."));
        return rst;
    }

    // ---- Inbound segment processing ----

    public TcpProcessingReport AcceptSegment(NetworkInterface localInterface, IPv4Address localAddress, IPv4Address remoteAddress, TcpSegment segment)
    {
        ArgumentNullException.ThrowIfNull(localInterface);
        ArgumentNullException.ThrowIfNull(segment);

        var key = new TcpConnectionKey(localAddress, segment.DestinationPort, remoteAddress, segment.SourcePort);

        if (_connections.TryGetValue(key, out var connection))
        {
            if (segment.IsRst)
            {
                connection.TransitionTo(TcpConnectionState.Closed);
                _connections.Remove(key);
                ConnectionReset?.Invoke(this, new TcpConnectionEventArgs(connection, segment, detail: "RST received from peer."));
                return TcpProcessingReport.Reset(connection);
            }

            return connection.State switch
            {
                TcpConnectionState.SynSent => ProcessSynSent(connection, segment),
                TcpConnectionState.SynReceived => ProcessSynReceived(connection, segment),
                TcpConnectionState.Established => ProcessEstablished(connection, segment),
                TcpConnectionState.FinWait1 => ProcessFinWait1(connection, segment),
                TcpConnectionState.FinWait2 => ProcessFinWait2(connection, segment),
                TcpConnectionState.Closing => ProcessClosing(connection, segment),
                TcpConnectionState.LastAck => ProcessLastAck(connection, segment),
                _ => TcpProcessingReport.Ignored(connection, $"Segment ignored: {connection.Key} is in state {connection.State}."),
            };
        }

        if (segment.IsSyn && !segment.IsAck && TryFindListener(localInterface.Device, segment.DestinationPort, out var listener))
        {
            return AcceptOnListener(localInterface, localAddress, remoteAddress, segment, listener!, key);
        }

        return Refuse(localAddress, remoteAddress, segment);
    }

    private TcpProcessingReport AcceptOnListener(
        NetworkInterface localInterface, IPv4Address localAddress, IPv4Address remoteAddress, TcpSegment syn, TcpListener listener, TcpConnectionKey key)
    {
        var connection = new TcpConnection(key, localInterface.Device, isServerSide: true, listener.OnDataReceived);
        connection.SetRemoteInitialSequence(syn.SequenceNumber);
        connection.AdvanceRemoteSequence(syn.SequenceLength);

        var isn = _isnGenerator.Next();
        connection.SetLocalInitialSequence(isn);
        connection.TransitionTo(TcpConnectionState.SynReceived);
        _connections[key] = connection;

        var synAck = TcpSegment.CreateSynAck(localAddress, remoteAddress, syn.DestinationPort, syn.SourcePort, isn, connection.RemoteNextSequenceNumber);
        connection.AdvanceLocalSequence(synAck.SequenceLength);

        SynReceived?.Invoke(this, new TcpConnectionEventArgs(connection, syn, detail: "Passive open: SYN received on listener."));
        SynAckSent?.Invoke(this, new TcpConnectionEventArgs(connection, synAck));

        return TcpProcessingReport.SynReceived(connection, synAck);
    }

    private TcpProcessingReport Refuse(IPv4Address localAddress, IPv4Address remoteAddress, TcpSegment segment)
    {
        var rst = segment.IsAck
            ? TcpSegment.CreateReset(localAddress, remoteAddress, segment.DestinationPort, segment.SourcePort, segment.AcknowledgmentNumber)
            : TcpSegment.CreateReset(
                localAddress, remoteAddress, segment.DestinationPort, segment.SourcePort, 0,
                unchecked(segment.SequenceNumber + (uint)segment.SequenceLength));

        ConnectionRefused?.Invoke(this, new TcpConnectionEventArgs(
            null, rst, dropReason: TcpDropReasons.ConnectionRefused,
            detail: $"No listener or connection for {localAddress}:{segment.DestinationPort} from {remoteAddress}:{segment.SourcePort}."));

        return TcpProcessingReport.Refused(rst);
    }

    private TcpProcessingReport ProcessSynSent(TcpConnection connection, TcpSegment segment)
    {
        if (segment.IsSynAck && segment.AcknowledgmentNumber == connection.LocalNextSequenceNumber)
        {
            connection.SetRemoteInitialSequence(segment.SequenceNumber);
            connection.AdvanceRemoteSequence(segment.SequenceLength);
            connection.TransitionTo(TcpConnectionState.Established);

            var ack = BuildAck(connection);
            AckSent?.Invoke(this, new TcpConnectionEventArgs(connection, ack));
            ConnectionEstablished?.Invoke(this, new TcpConnectionEventArgs(connection, ack));

            return TcpProcessingReport.HandshakeCompleted(connection, ack);
        }

        return TcpProcessingReport.Ignored(connection, $"Unexpected segment for {connection.Key} in SynSent state: [{segment.Flags.Describe()}].");
    }

    private TcpProcessingReport ProcessSynReceived(TcpConnection connection, TcpSegment segment)
    {
        if (segment.IsAck && !segment.IsSyn
            && segment.AcknowledgmentNumber == connection.LocalNextSequenceNumber
            && segment.SequenceNumber == connection.RemoteNextSequenceNumber)
        {
            connection.TransitionTo(TcpConnectionState.Established);
            AckProcessed?.Invoke(this, new TcpConnectionEventArgs(connection, segment));
            ConnectionEstablished?.Invoke(this, new TcpConnectionEventArgs(connection, segment));
            return TcpProcessingReport.ConnectionEstablished(connection);
        }

        return TcpProcessingReport.Ignored(connection, $"Unexpected segment for {connection.Key} in SynReceived state: [{segment.Flags.Describe()}].");
    }

    private TcpProcessingReport ProcessEstablished(TcpConnection connection, TcpSegment segment)
    {
        if (segment.SequenceNumber != connection.RemoteNextSequenceNumber)
        {
            return TcpProcessingReport.Ignored(
                connection,
                $"Unexpected sequence number for {connection.Key}: expected {connection.RemoteNextSequenceNumber}, got {segment.SequenceNumber}.");
        }

        if (segment.Payload.Length > 0)
        {
            connection.AdvanceRemoteSequence(segment.Payload.Length);
            connection.DeliverData(segment.Payload);
            DataDelivered?.Invoke(this, new TcpConnectionEventArgs(connection, segment, segment.Payload));

            if (segment.IsFin)
            {
                return ReceiveFinFromEstablished(connection, segment);
            }

            var ack = BuildAck(connection);
            AckSent?.Invoke(this, new TcpConnectionEventArgs(connection, ack));
            return TcpProcessingReport.DataDelivered(connection, ack, segment.Payload);
        }

        if (segment.IsFin)
        {
            return ReceiveFinFromEstablished(connection, segment);
        }

        if (segment.IsAck)
        {
            AckProcessed?.Invoke(this, new TcpConnectionEventArgs(connection, segment));
            return TcpProcessingReport.AckProcessed(connection);
        }

        return TcpProcessingReport.Ignored(connection, $"Segment for {connection.Key} carried no data/FIN/ACK to process.");
    }

    private TcpProcessingReport ReceiveFinFromEstablished(TcpConnection connection, TcpSegment fin)
    {
        connection.AdvanceRemoteSequence(1);
        connection.TransitionTo(TcpConnectionState.CloseWait);

        var ack = BuildAck(connection);
        FinReceived?.Invoke(this, new TcpConnectionEventArgs(connection, fin));
        AckSent?.Invoke(this, new TcpConnectionEventArgs(connection, ack));
        ConnectionClosing?.Invoke(this, new TcpConnectionEventArgs(connection, ack));

        return TcpProcessingReport.FinReceived(connection, ack);
    }

    private TcpProcessingReport ProcessFinWait1(TcpConnection connection, TcpSegment segment)
    {
        if (segment.IsFin)
        {
            if (segment.SequenceNumber == connection.RemoteNextSequenceNumber)
            {
                connection.AdvanceRemoteSequence(1);
            }

            var acknowledgedOurFin = segment.IsAck && segment.AcknowledgmentNumber == connection.LocalNextSequenceNumber;
            connection.TransitionTo(acknowledgedOurFin ? TcpConnectionState.TimeWait : TcpConnectionState.Closing);

            var ack = BuildAck(connection);
            FinReceived?.Invoke(this, new TcpConnectionEventArgs(connection, segment));
            AckSent?.Invoke(this, new TcpConnectionEventArgs(connection, ack));
            return TcpProcessingReport.FinReceived(connection, ack);
        }

        if (segment.IsAck && segment.AcknowledgmentNumber == connection.LocalNextSequenceNumber)
        {
            connection.TransitionTo(TcpConnectionState.FinWait2);
            AckProcessed?.Invoke(this, new TcpConnectionEventArgs(connection, segment));
            return TcpProcessingReport.AckProcessed(connection);
        }

        return TcpProcessingReport.Ignored(connection, $"Unexpected segment for {connection.Key} in FinWait1 state: [{segment.Flags.Describe()}].");
    }

    private TcpProcessingReport ProcessFinWait2(TcpConnection connection, TcpSegment segment)
    {
        if (segment.IsFin && segment.SequenceNumber == connection.RemoteNextSequenceNumber)
        {
            connection.AdvanceRemoteSequence(1);
            connection.TransitionTo(TcpConnectionState.TimeWait);

            var ack = BuildAck(connection);
            FinReceived?.Invoke(this, new TcpConnectionEventArgs(connection, segment));
            AckSent?.Invoke(this, new TcpConnectionEventArgs(connection, ack));
            return TcpProcessingReport.FinReceived(connection, ack);
        }

        return TcpProcessingReport.Ignored(connection, $"Unexpected segment for {connection.Key} in FinWait2 state: [{segment.Flags.Describe()}].");
    }

    private TcpProcessingReport ProcessClosing(TcpConnection connection, TcpSegment segment)
    {
        if (segment.IsAck && segment.AcknowledgmentNumber == connection.LocalNextSequenceNumber)
        {
            connection.TransitionTo(TcpConnectionState.TimeWait);
            AckProcessed?.Invoke(this, new TcpConnectionEventArgs(connection, segment));
            return TcpProcessingReport.AckProcessed(connection);
        }

        return TcpProcessingReport.Ignored(connection, $"Unexpected segment for {connection.Key} in Closing state: [{segment.Flags.Describe()}].");
    }

    private TcpProcessingReport ProcessLastAck(TcpConnection connection, TcpSegment segment)
    {
        if (segment.IsAck && segment.AcknowledgmentNumber == connection.LocalNextSequenceNumber)
        {
            connection.TransitionTo(TcpConnectionState.Closed);
            _connections.Remove(connection.Key);
            ConnectionClosed?.Invoke(this, new TcpConnectionEventArgs(connection));
            return TcpProcessingReport.ConnectionClosed(connection);
        }

        return TcpProcessingReport.Ignored(connection, $"Unexpected segment for {connection.Key} in LastAck state: [{segment.Flags.Describe()}].");
    }

    private static TcpSegment BuildAck(TcpConnection connection) =>
        TcpSegment.CreateAck(
            connection.Key.LocalAddress, connection.Key.RemoteAddress, connection.Key.LocalPort, connection.Key.RemotePort,
            connection.LocalNextSequenceNumber, connection.RemoteNextSequenceNumber);
}
