using NetSim.Core.Icmp;
using NetSim.Core.Networking;

namespace NetSim.Application.Diagnostics;

/// <summary>
/// The result of one simulated echo exchange - one "Reply from ..." (or failure) line of a ping.
/// Carries everything the brief's section 21 asks for beyond a boolean: the sequence number, the
/// responding address, TTL, a (currently nominal - see <see cref="PingService"/>) round-trip time,
/// the structured <see cref="PingReplyStatus"/>, a human-readable <see cref="ErrorMessage"/>, and
/// the actual <see cref="EchoRequest"/> / <see cref="EchoReply"/> messages for a future packet
/// inspector.
/// </summary>
public sealed class PingReply
{
    private PingReply(
        PingReplyStatus status,
        int sequenceNumber,
        IPv4Address destination,
        IPv4Address? respondingAddress,
        byte? timeToLive,
        TimeSpan? roundTripTime,
        string? errorMessage,
        IcmpMessage? echoRequest,
        IcmpMessage? echoReply)
    {
        Status = status;
        SequenceNumber = sequenceNumber;
        Destination = destination;
        RespondingAddress = respondingAddress;
        TimeToLive = timeToLive;
        RoundTripTime = roundTripTime;
        ErrorMessage = errorMessage;
        EchoRequest = echoRequest;
        EchoReply = echoReply;
    }

    public PingReplyStatus Status { get; }

    public bool IsSuccess => Status == PingReplyStatus.Success;

    public int SequenceNumber { get; }

    public IPv4Address Destination { get; }

    /// <summary>The address that answered - set only on <see cref="IsSuccess"/>.</summary>
    public IPv4Address? RespondingAddress { get; }

    /// <summary>The reply's IPv4 TTL - set only on <see cref="IsSuccess"/>.</summary>
    public byte? TimeToLive { get; }

    /// <summary>Simulated round-trip time - set only on <see cref="IsSuccess"/>. See <see cref="PingService"/> for why this is currently a nominal value.</summary>
    public TimeSpan? RoundTripTime { get; }

    /// <summary>A human-readable explanation - set only when not <see cref="IsSuccess"/>. Safe to show directly in the UI.</summary>
    public string? ErrorMessage { get; }

    /// <summary>The Echo Request that was sent, when one was built.</summary>
    public IcmpMessage? EchoRequest { get; }

    /// <summary>The Echo Reply that was received - set only on <see cref="IsSuccess"/>.</summary>
    public IcmpMessage? EchoReply { get; }

    internal static PingReply Success(
        int sequenceNumber, IPv4Address destination, IPv4Address respondingAddress, byte timeToLive,
        TimeSpan roundTripTime, IcmpMessage echoRequest, IcmpMessage echoReply) =>
        new(PingReplyStatus.Success, sequenceNumber, destination, respondingAddress, timeToLive, roundTripTime, null, echoRequest, echoReply);

    internal static PingReply Failure(
        PingReplyStatus status, int sequenceNumber, IPv4Address destination, string errorMessage, IcmpMessage? echoRequest = null) =>
        new(status, sequenceNumber, destination, null, null, null, errorMessage, echoRequest, null);

    public override string ToString() => IsSuccess
        ? $"Reply from {RespondingAddress}: seq={SequenceNumber} time={RoundTripTime!.Value.TotalMilliseconds:0}ms TTL={TimeToLive}"
        : $"Request to {Destination} failed (seq={SequenceNumber}): {ErrorMessage}";
}
