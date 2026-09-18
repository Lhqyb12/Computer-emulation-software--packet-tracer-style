using System.Linq;
using NetSim.Core.Networking;

namespace NetSim.Application.Diagnostics;

/// <summary>
/// The aggregate result of an <see cref="IPingService.Ping"/> call - the brief's section 22
/// "Packets Sent / Packets Received / Packet Loss / Average Time" summary, plus the individual
/// <see cref="Replies"/> so the UI can render each "Reply from ..." line as it happens.
/// </summary>
public sealed class PingSessionResult
{
    private PingSessionResult(IPv4Address destination, IReadOnlyList<PingReply> replies)
    {
        Destination = destination;
        Replies = replies;
    }

    public IPv4Address Destination { get; }

    public IReadOnlyList<PingReply> Replies { get; }

    public int PacketsSent => Replies.Count;

    public int PacketsReceived => Replies.Count(r => r.IsSuccess);

    public int PacketsLost => PacketsSent - PacketsReceived;

    public double PacketLossPercentage => PacketsSent == 0 ? 0 : PacketsLost * 100.0 / PacketsSent;

    /// <summary>The mean round-trip time across successful replies, or null when none succeeded.</summary>
    public TimeSpan? AverageRoundTripTime
    {
        get
        {
            var times = Replies.Where(r => r.IsSuccess).Select(r => r.RoundTripTime!.Value).ToList();
            return times.Count == 0 ? null : TimeSpan.FromTicks((long)times.Average(t => t.Ticks));
        }
    }

    public bool IsFullSuccess => PacketsSent > 0 && PacketsLost == 0;

    internal static PingSessionResult Create(IPv4Address destination, IReadOnlyList<PingReply> replies) =>
        new(destination, replies);
}
