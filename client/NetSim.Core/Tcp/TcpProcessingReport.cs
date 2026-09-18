using NetSim.Core.Packets;

namespace NetSim.Core.Tcp;

/// <summary>
/// The outcome of running one inbound TCP segment through <see cref="ITcpConnectionManager.AcceptSegment"/>
/// (or, transitively, <see cref="ITcpLayer.HandleIncoming"/>): what happened
/// (<see cref="Kind"/>), the connection involved, and the segment (if any) the caller must transmit
/// back to the remote side. Mirrors the descriptive-report shape of <see cref="Icmp.IcmpProcessingReport"/> /
/// <see cref="Udp.UdpProcessingReport"/>.
/// </summary>
public sealed class TcpProcessingReport
{
    private TcpProcessingReport(
        TcpProcessingOutcomeKind kind, TcpConnection? connection, TcpSegment? responseSegment,
        IPacketPayload? deliveredPayload, PacketDropReason? dropReason, string description)
    {
        Kind = kind;
        Connection = connection;
        ResponseSegment = responseSegment;
        DeliveredPayload = deliveredPayload;
        DropReason = dropReason;
        Description = description;
    }

    public TcpProcessingOutcomeKind Kind { get; }

    /// <summary>True for anything other than <see cref="TcpProcessingOutcomeKind.Dropped"/> - the segment was structurally accepted, even if it led to a refusal or a reset.</summary>
    public bool IsSuccess => Kind != TcpProcessingOutcomeKind.Dropped;

    /// <summary>The connection involved - null only for <see cref="TcpProcessingOutcomeKind.Dropped"/> or <see cref="TcpProcessingOutcomeKind.ConnectionRefused"/> (no connection exists yet).</summary>
    public TcpConnection? Connection { get; }

    /// <summary>A segment the caller must transmit back to the remote side (a SYN+ACK, an ACK, a RST, ...) - null when nothing needs to be sent.</summary>
    public TcpSegment? ResponseSegment { get; }

    /// <summary>Set only for <see cref="TcpProcessingOutcomeKind.DataDelivered"/> - the application payload that was delivered.</summary>
    public IPacketPayload? DeliveredPayload { get; }

    public PacketDropReason? DropReason { get; }

    public string Description { get; }

    internal static TcpProcessingReport Dropped(PacketDropReason reason, string detail) =>
        new(TcpProcessingOutcomeKind.Dropped, null, null, null, reason, $"TCP segment dropped ({reason.Code}): {detail}");

    internal static TcpProcessingReport Refused(TcpSegment rst) =>
        new(TcpProcessingOutcomeKind.ConnectionRefused, null, rst, null, TcpDropReasons.ConnectionRefused,
            "No listener or matching connection exists; connection refused (RST sent).");

    internal static TcpProcessingReport SynReceived(TcpConnection connection, TcpSegment synAck) =>
        new(TcpProcessingOutcomeKind.SynReceived, connection, synAck, null, null,
            $"SYN received for {connection.Key}; SYN+ACK sent, connection now {connection.State}.");

    internal static TcpProcessingReport HandshakeCompleted(TcpConnection connection, TcpSegment ack) =>
        new(TcpProcessingOutcomeKind.HandshakeCompleted, connection, ack, null, null,
            $"SYN+ACK received for {connection.Key}; final ACK sent, connection now {connection.State}.");

    internal static TcpProcessingReport ConnectionEstablished(TcpConnection connection) =>
        new(TcpProcessingOutcomeKind.ConnectionEstablished, connection, null, null, null,
            $"Final ACK received for {connection.Key}; connection now {connection.State}.");

    internal static TcpProcessingReport DataDelivered(TcpConnection connection, TcpSegment ack, IPacketPayload payload) =>
        new(TcpProcessingOutcomeKind.DataDelivered, connection, ack, payload, null,
            $"{payload.Length}B delivered on {connection.Key}; ACK sent.");

    internal static TcpProcessingReport AckProcessed(TcpConnection connection) =>
        new(TcpProcessingOutcomeKind.AckProcessed, connection, null, null, null, $"ACK processed for {connection.Key}.");

    internal static TcpProcessingReport FinReceived(TcpConnection connection, TcpSegment ack) =>
        new(TcpProcessingOutcomeKind.FinReceived, connection, ack, null, null,
            $"FIN received for {connection.Key}; ACK sent, connection now {connection.State}.");

    internal static TcpProcessingReport ConnectionClosed(TcpConnection connection) =>
        new(TcpProcessingOutcomeKind.ConnectionClosed, connection, null, null, null,
            $"{connection.Key} fully closed (state {connection.State}).");

    internal static TcpProcessingReport Reset(TcpConnection connection) =>
        new(TcpProcessingOutcomeKind.Reset, connection, null, null, null, $"{connection.Key} reset by peer.");

    internal static TcpProcessingReport Ignored(TcpConnection connection, string detail) =>
        new(TcpProcessingOutcomeKind.Ignored, connection, null, null, null, detail);
}
