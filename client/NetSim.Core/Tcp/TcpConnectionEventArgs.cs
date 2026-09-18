using NetSim.Core.Packets;

namespace NetSim.Core.Tcp;

/// <summary>
/// Payload for every <see cref="ITcpConnectionManager"/> connection-lifecycle notification (brief
/// section 30) - the same optional-fields carrier pattern as <see cref="Icmp.IcmpEventArgs"/> /
/// <see cref="Udp.UdpEventArgs"/>.
/// </summary>
public sealed class TcpConnectionEventArgs : EventArgs
{
    public TcpConnectionEventArgs(
        TcpConnection? connection, TcpSegment? segment = null, IPacketPayload? deliveredPayload = null,
        PacketDropReason? dropReason = null, string? detail = null)
    {
        Connection = connection;
        Segment = segment;
        DeliveredPayload = deliveredPayload;
        DropReason = dropReason;
        Detail = detail;
    }

    /// <summary>The connection this event is about - null only for <see cref="ITcpConnectionManager.ConnectionRefused"/>, where no connection was ever created.</summary>
    public TcpConnection? Connection { get; }

    /// <summary>The segment sent or received that triggered this event, when applicable.</summary>
    public TcpSegment? Segment { get; }

    /// <summary>Set only for a data-delivery event.</summary>
    public IPacketPayload? DeliveredPayload { get; }

    public PacketDropReason? DropReason { get; }

    public string? Detail { get; }
}
