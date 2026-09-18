using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Arp;

/// <summary>
/// The rich, human-readable outcome of <see cref="IArpLayer.HandleIncoming(NetworkInterface, EthernetFrame)"/>
/// - the information a diagnostics panel or a future AI explanation needs about one received ARP
/// message: was it a request or a reply, did the receiving interface own the target IP, was a
/// reply generated, and what did the cache do. Deliberately more descriptive than a bare
/// <see cref="PacketProcessingResult"/>.
/// </summary>
public sealed class ArpProcessingReport
{
    private ArpProcessingReport(
        bool isSuccess,
        PacketDropReason? dropReason,
        ArpOperation? operation,
        IPv4Address? targetProtocolAddress,
        bool localInterfaceOwnsTarget,
        ArpPacket? reply,
        EthernetFrame? replyFrame,
        ArpCacheChange cacheChange,
        bool cacheConflict,
        ArpCacheEntry? learnedEntry,
        string description)
    {
        IsSuccess = isSuccess;
        DropReason = dropReason;
        Operation = operation;
        TargetProtocolAddress = targetProtocolAddress;
        LocalInterfaceOwnsTarget = localInterfaceOwnsTarget;
        Reply = reply;
        ReplyFrame = replyFrame;
        CacheChange = cacheChange;
        CacheConflict = cacheConflict;
        LearnedEntry = learnedEntry;
        Description = description;
    }

    /// <summary>True when the message parsed and was processed; false when it was dropped.</summary>
    public bool IsSuccess { get; }

    /// <summary>Why the message was dropped - null when <see cref="IsSuccess"/>.</summary>
    public PacketDropReason? DropReason { get; }

    /// <summary>Request or Reply - null only for a dropped message.</summary>
    public ArpOperation? Operation { get; }

    /// <summary>The message's target IPv4 address.</summary>
    public IPv4Address? TargetProtocolAddress { get; }

    /// <summary>True when the receiving interface owns <see cref="TargetProtocolAddress"/> (a request it should answer).</summary>
    public bool LocalInterfaceOwnsTarget { get; }

    /// <summary>True when this processing produced an ARP reply to send back.</summary>
    public bool ReplyGenerated => Reply is not null;

    /// <summary>The generated reply - set only when <see cref="ReplyGenerated"/>.</summary>
    public ArpPacket? Reply { get; }

    /// <summary>The unicast Ethernet frame carrying <see cref="Reply"/> (destination = the requester's MAC).</summary>
    public EthernetFrame? ReplyFrame { get; }

    /// <summary>What learning the sender's mapping did to the receiving interface's ARP cache.</summary>
    public ArpCacheChange CacheChange { get; }

    /// <summary>True when learning the sender replaced an existing mapping with a <em>different</em> MAC.</summary>
    public bool CacheConflict { get; }

    /// <summary>The cache entry learned / refreshed for the sender, when the cache changed.</summary>
    public ArpCacheEntry? LearnedEntry { get; }

    /// <summary>A diagnostic sentence, e.g. "ARP request received. Target IP 192.168.1.20. Local interface owns target: Yes. ARP reply generated."</summary>
    public string Description { get; }

    internal static ArpProcessingReport Dropped(PacketDropReason reason, string detail) =>
        new(false, reason, null, null, false, null, null, ArpCacheChange.None, false, null,
            $"ARP message dropped ({reason.Code}): {detail}");

    internal static ArpProcessingReport ForReply(ArpPacket reply, ArpCacheUpdateResult update) =>
        new(true, null, ArpOperation.Reply, reply.TargetProtocolAddress, false, null, null,
            update.Change, update.IsConflict, update.Entry,
            $"ARP reply received. {reply.SenderProtocolAddress} is at {reply.SenderHardwareAddress}. " +
            $"Cache {update.Change.ToString().ToLowerInvariant()}{(update.IsConflict ? " (mapping changed)" : string.Empty)}.");

    internal static ArpProcessingReport ForRequest(
        ArpPacket request, ArpCacheUpdateResult update, bool ownsTarget, ArpPacket? reply, EthernetFrame? replyFrame)
    {
        var description = ownsTarget
            ? $"ARP request received. Target IP {request.TargetProtocolAddress}. Local interface owns target: Yes. ARP reply generated."
            : $"ARP request received. Target IP {request.TargetProtocolAddress}. Local interface owns target: No. No reply generated.";

        return new(true, null, ArpOperation.Request, request.TargetProtocolAddress, ownsTarget,
            reply, replyFrame, update.Change, update.IsConflict, update.Entry, description);
    }
}
