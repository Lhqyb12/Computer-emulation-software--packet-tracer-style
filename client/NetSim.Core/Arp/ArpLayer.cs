using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;

namespace NetSim.Core.Arp;

/// <summary>
/// Default <see cref="IArpLayer"/>. Stateless apart from its event subscribers - the mutable ARP
/// state lives in each <see cref="NetworkInterface.ArpCache"/>, and the structural work is
/// delegated to <see cref="ArpPacket"/> / <see cref="EthernetFrame"/> / the injected
/// <see cref="ArpProcessor"/>.
/// </summary>
public sealed class ArpLayer : IArpLayer
{
    private readonly ArpProcessor _processor;

    public ArpLayer(ArpProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _processor = processor;
    }

    public event EventHandler<ArpEventArgs>? RequestCreated;

    public event EventHandler<ArpEventArgs>? RequestEncapsulated;

    public event EventHandler<ArpEventArgs>? ReplyCreated;

    public event EventHandler<ArpEventArgs>? ReplyEncapsulated;

    public event EventHandler<ArpEventArgs>? ResolutionStarted;

    public event EventHandler<ArpEventArgs>? ResolutionSucceeded;

    public event EventHandler<ArpEventArgs>? ResolutionFailed;

    public event EventHandler<ArpEventArgs>? RequestReceived;

    public event EventHandler<ArpEventArgs>? ReplyReceived;

    public event EventHandler<ArpEventArgs>? EntryLearned;

    public event EventHandler<ArpEventArgs>? EntryUpdated;

    public event EventHandler<ArpEventArgs>? PacketDropped;

    public ArpPacket CreateRequest(NetworkInterface senderInterface, IPv4Address targetProtocolAddress)
    {
        ArgumentNullException.ThrowIfNull(senderInterface);

        var (mac, ip) = RequireEthernetIPv4(senderInterface);
        var request = ArpPacket.CreateRequest(mac, ip, targetProtocolAddress);
        RequestCreated?.Invoke(this, new ArpEventArgs(
            request, networkInterface: senderInterface, protocolAddress: targetProtocolAddress, detail: request.ToString()));
        return request;
    }

    public ArpPacket CreateReply(
        NetworkInterface senderInterface,
        IPv4Address senderProtocolAddress,
        MacAddress targetHardwareAddress,
        IPv4Address targetProtocolAddress)
    {
        ArgumentNullException.ThrowIfNull(senderInterface);

        if (senderInterface.MacAddress is not { } mac)
        {
            throw new DomainException(
                $"Interface '{senderInterface.Name}' on '{senderInterface.Device.Name}' has no MAC address to send an ARP reply from.");
        }

        var reply = ArpPacket.CreateReply(mac, senderProtocolAddress, targetHardwareAddress, targetProtocolAddress);
        ReplyCreated?.Invoke(this, new ArpEventArgs(
            reply, networkInterface: senderInterface, protocolAddress: targetProtocolAddress,
            hardwareAddress: targetHardwareAddress, detail: reply.ToString()));
        return reply;
    }

    public EthernetFrame EncapsulateRequest(ArpPacket request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.IsRequest)
        {
            throw new DomainException("EncapsulateRequest requires an ARP request.");
        }

        // ARP requests are Ethernet broadcasts - that is how they reach every host on the segment.
        var frame = EthernetFrame.Create(request.SenderHardwareAddress, MacAddress.Broadcast, EtherType.Arp, request);
        RequestEncapsulated?.Invoke(this, new ArpEventArgs(request, frame, detail: frame.ToString()));
        return frame;
    }

    public EthernetFrame EncapsulateReply(ArpPacket reply)
    {
        ArgumentNullException.ThrowIfNull(reply);

        if (!reply.IsReply)
        {
            throw new DomainException("EncapsulateReply requires an ARP reply.");
        }

        // ARP replies are unicast straight back to the requester.
        var frame = EthernetFrame.Create(reply.SenderHardwareAddress, reply.TargetHardwareAddress, EtherType.Arp, reply);
        ReplyEncapsulated?.Invoke(this, new ArpEventArgs(reply, frame, detail: frame.ToString()));
        return frame;
    }

    public bool TryDecapsulate(EthernetFrame frame, [NotNullWhen(true)] out ArpPacket? arp)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.EtherType == EtherType.Arp && frame.Payload is ArpPacket inner)
        {
            arp = inner;
            return true;
        }

        arp = null;
        return false;
    }

    public ArpResolution Resolve(NetworkInterface senderInterface, IPv4Address targetProtocolAddress)
    {
        ArgumentNullException.ThrowIfNull(senderInterface);

        ResolutionStarted?.Invoke(this, new ArpEventArgs(
            networkInterface: senderInterface, protocolAddress: targetProtocolAddress,
            detail: $"Resolving {targetProtocolAddress} on {Describe(senderInterface)}"));

        // 1. Cache lookup first - a hit means no ARP request is needed at all.
        if (senderInterface.ArpCache.TryGet(targetProtocolAddress, out var entry))
        {
            ResolutionSucceeded?.Invoke(this, new ArpEventArgs(
                networkInterface: senderInterface, entry: entry, protocolAddress: targetProtocolAddress,
                hardwareAddress: entry.HardwareAddress,
                detail: $"ARP cache hit: {targetProtocolAddress} is at {entry.HardwareAddress}"));
            return ArpResolution.Resolved(entry.HardwareAddress, entry);
        }

        // 2. Cache miss - the interface must be usable for ARP to build a request.
        if (!senderInterface.SupportsEthernet
            || senderInterface.MacAddress is not { } senderMac
            || senderInterface.PrimaryIPv4Configuration is not { } senderIp)
        {
            ResolutionFailed?.Invoke(this, new ArpEventArgs(
                networkInterface: senderInterface, protocolAddress: targetProtocolAddress,
                dropReason: ArpDropReasons.InvalidSenderInterface,
                detail: $"Interface '{senderInterface.Name}' has no MAC and/or IPv4 address; cannot build an ARP request for {targetProtocolAddress}."));
            return ArpResolution.Failed(ArpDropReasons.InvalidSenderInterface.Description);
        }

        // 3. Cache miss with a usable interface - generate the broadcast ARP request.
        var request = ArpPacket.CreateRequest(senderMac, senderIp.Address, targetProtocolAddress);
        RequestCreated?.Invoke(this, new ArpEventArgs(
            request, networkInterface: senderInterface, protocolAddress: targetProtocolAddress, detail: request.ToString()));
        var frame = EncapsulateRequest(request);
        return ArpResolution.Pending(request, frame);
    }

    public ArpProcessingReport HandleIncoming(NetworkInterface receivingInterface, EthernetFrame frame)
    {
        ArgumentNullException.ThrowIfNull(receivingInterface);
        ArgumentNullException.ThrowIfNull(frame);

        if (!TryDecapsulate(frame, out var arp))
        {
            var report = ArpProcessingReport.Dropped(
                ArpDropReasons.NotArp, $"frame carries {frame.EtherType.Name}, not ARP");
            PacketDropped?.Invoke(this, new ArpEventArgs(
                frame: frame, networkInterface: receivingInterface, dropReason: ArpDropReasons.NotArp, detail: report.Description));
            return report;
        }

        return HandleIncomingCore(receivingInterface, arp, frame);
    }

    public ArpProcessingReport HandleIncoming(NetworkInterface receivingInterface, ArpPacket arp)
    {
        ArgumentNullException.ThrowIfNull(receivingInterface);
        ArgumentNullException.ThrowIfNull(arp);
        return HandleIncomingCore(receivingInterface, arp, frame: null);
    }

    private ArpProcessingReport HandleIncomingCore(NetworkInterface receivingInterface, ArpPacket arp, EthernetFrame? frame)
    {
        var validation = arp.Validate();
        if (!validation.IsValid)
        {
            var report = ArpProcessingReport.Dropped(ArpDropReasons.InvalidPacket, string.Join("; ", validation.Errors));
            PacketDropped?.Invoke(this, new ArpEventArgs(
                arp, frame, networkInterface: receivingInterface, dropReason: ArpDropReasons.InvalidPacket, detail: report.Description));
            return report;
        }

        (arp.IsRequest ? RequestReceived : ReplyReceived)?.Invoke(this, new ArpEventArgs(
            arp, frame, networkInterface: receivingInterface, detail: arp.ToString()));

        // Learn the sender's mapping from any received ARP message (RFC 826). The cache resolves a
        // conflicting mapping deterministically (last writer wins) - see ArpCache.
        var update = receivingInterface.ArpCache.AddOrUpdateDynamic(arp.SenderProtocolAddress, arp.SenderHardwareAddress);
        RaiseCacheEvent(receivingInterface, update);

        if (arp.IsReply)
        {
            return ArpProcessingReport.ForReply(arp, update);
        }

        // A request is answered only when THIS interface owns the target IPv4 - never for an
        // arbitrary address, and never forwarded (no routing / proxy ARP in this phase).
        var ownsTarget = receivingInterface.IPv4Configurations.Any(c => c.Address == arp.TargetProtocolAddress);
        if (!ownsTarget)
        {
            return ArpProcessingReport.ForRequest(arp, update, ownsTarget: false, reply: null, replyFrame: null);
        }

        var reply = CreateReply(
            receivingInterface, arp.TargetProtocolAddress, arp.SenderHardwareAddress, arp.SenderProtocolAddress);
        var replyFrame = EncapsulateReply(reply);
        return ArpProcessingReport.ForRequest(arp, update, ownsTarget: true, reply: reply, replyFrame: replyFrame);
    }

    private void RaiseCacheEvent(NetworkInterface receivingInterface, ArpCacheUpdateResult update)
    {
        if (update is { Change: ArpCacheChange.Added, Entry: { } added })
        {
            EntryLearned?.Invoke(this, new ArpEventArgs(
                networkInterface: receivingInterface, entry: added, protocolAddress: added.ProtocolAddress,
                hardwareAddress: added.HardwareAddress, detail: $"Learned {added}"));
        }
        else if (update is { Change: ArpCacheChange.Updated, Entry: { } updated })
        {
            EntryUpdated?.Invoke(this, new ArpEventArgs(
                networkInterface: receivingInterface, entry: updated, protocolAddress: updated.ProtocolAddress,
                hardwareAddress: updated.HardwareAddress,
                detail: update.IsConflict
                    ? $"Updated {updated.ProtocolAddress}: {update.PreviousHardwareAddress} -> {updated.HardwareAddress}"
                    : $"Refreshed {updated}"));
        }
    }

    private static (MacAddress mac, IPv4Address ip) RequireEthernetIPv4(NetworkInterface networkInterface)
    {
        if (!networkInterface.SupportsEthernet || networkInterface.MacAddress is not { } mac)
        {
            throw new DomainException(
                $"Interface '{networkInterface.Name}' on '{networkInterface.Device.Name}' has no MAC address for ARP.");
        }

        if (networkInterface.PrimaryIPv4Configuration is not { } configuration)
        {
            throw new DomainException(
                $"Interface '{networkInterface.Name}' on '{networkInterface.Device.Name}' has no IPv4 address for ARP.");
        }

        return (mac, configuration.Address);
    }

    private static string Describe(NetworkInterface networkInterface) =>
        $"{networkInterface.Device.Name}/{networkInterface.Name}";
}
