using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Icmp;

/// <summary>
/// The rich, human-readable outcome of <see cref="IIcmpLayer.HandleIncoming"/> - the information a
/// diagnostics panel or a future AI explanation needs about one received ICMP message: what type it
/// was, whether the receiving interface owned the destination (for an Echo Request), and whether a
/// reply was generated. Deliberately more descriptive than a bare <see cref="PacketProcessingResult"/>,
/// mirroring <see cref="Arp.ArpProcessingReport"/>.
/// </summary>
public sealed class IcmpProcessingReport
{
    private IcmpProcessingReport(
        bool isSuccess,
        PacketDropReason? dropReason,
        IcmpType? messageType,
        IcmpMessage? message,
        bool localInterfaceOwnsDestination,
        IcmpMessage? reply,
        IPv4Packet? replyPacket,
        string description)
    {
        IsSuccess = isSuccess;
        DropReason = dropReason;
        MessageType = messageType;
        Message = message;
        LocalInterfaceOwnsDestination = localInterfaceOwnsDestination;
        Reply = reply;
        ReplyPacket = replyPacket;
        Description = description;
    }

    /// <summary>True when the message parsed and was processed; false when it was dropped.</summary>
    public bool IsSuccess { get; }

    /// <summary>Why the message was dropped - null when <see cref="IsSuccess"/>.</summary>
    public PacketDropReason? DropReason { get; }

    /// <summary>The message's ICMP type - null only for a dropped message.</summary>
    public IcmpType? MessageType { get; }

    /// <summary>The received message - null only for a dropped message.</summary>
    public IcmpMessage? Message { get; }

    /// <summary>
    /// For an Echo Request: true when the receiving interface owns the IPv4 packet's destination
    /// address (so it is answered). Always false for anything else.
    /// </summary>
    public bool LocalInterfaceOwnsDestination { get; }

    /// <summary>True when this processing produced an Echo Reply to send back.</summary>
    public bool ReplyGenerated => Reply is not null;

    /// <summary>The generated Echo Reply - set only when <see cref="ReplyGenerated"/>.</summary>
    public IcmpMessage? Reply { get; }

    /// <summary>The IPv4 packet carrying <see cref="Reply"/>, addressed back to the requester - set only when <see cref="ReplyGenerated"/>.</summary>
    public IPv4Packet? ReplyPacket { get; }

    /// <summary>A diagnostic sentence, e.g. "ICMP Echo Request received. Destination 192.168.1.20. Local interface owns destination: Yes. Echo Reply generated."</summary>
    public string Description { get; }

    internal static IcmpProcessingReport Dropped(PacketDropReason reason, string detail) =>
        new(false, reason, null, null, false, null, null, $"ICMP message dropped ({reason.Code}): {detail}");

    internal static IcmpProcessingReport ForEchoRequest(
        IcmpMessage request, IPv4Address destination, bool ownsDestination, IcmpMessage? reply, IPv4Packet? replyPacket)
    {
        var description = ownsDestination
            ? $"ICMP Echo Request received. Destination {destination}. Local interface owns destination: Yes. Echo Reply generated."
            : $"ICMP Echo Request received. Destination {destination}. Local interface owns destination: No. No reply generated.";

        return new(true, null, IcmpType.EchoRequest, request, ownsDestination, reply, replyPacket, description);
    }

    internal static IcmpProcessingReport ForEchoReply(IcmpMessage reply, IPv4Address source) =>
        new(true, null, IcmpType.EchoReply, reply, false, null, null,
            $"ICMP Echo Reply received from {source}. id={reply.Identifier} seq={reply.SequenceNumber}.");

    internal static IcmpProcessingReport ForError(IcmpMessage errorMessage, IPv4Address source) =>
        new(true, null, errorMessage.Type, errorMessage, false, null, null,
            $"{errorMessage} received from {source}.");
}
