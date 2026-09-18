using NetSim.Core.Arp;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Arp;

public class ArpLayerTests
{
    private static readonly IPv4Address IpA = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address IpB = IPv4Address.Parse("192.168.1.20");
    private static readonly IPv4Address IpC = IPv4Address.Parse("192.168.1.30");

    private static ArpLayer NewLayer() => new(new ArpProcessor());

    private static NetworkInterface NewInterface(string device, IPv4Address? ip)
    {
        var pc = NetworkDeviceFactory.Create(DeviceType.Pc, device);
        var ni = pc.Interfaces.Single();
        if (ip is { } address)
        {
            ni.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(address, 24));
        }

        return ni;
    }

    [Fact]
    public void CreateRequest_UsesTheInterfaceMacAndIp_AndRaisesRequestCreated()
    {
        var layer = NewLayer();
        var ni = NewInterface("PC0", IpA);
        ArpEventArgs? seen = null;
        layer.RequestCreated += (_, e) => seen = e;

        var request = layer.CreateRequest(ni, IpB);

        Assert.Equal(ArpOperation.Request, request.Operation);
        Assert.Equal(ni.MacAddress!.Value, request.SenderHardwareAddress);
        Assert.Equal(IpA, request.SenderProtocolAddress);
        Assert.Equal(IpB, request.TargetProtocolAddress);
        Assert.Same(request, seen!.Packet);
    }

    [Fact]
    public void CreateRequest_WithoutIPv4_Throws()
    {
        var layer = NewLayer();
        var ni = NewInterface("PC0", ip: null);

        Assert.Throws<DomainException>(() => layer.CreateRequest(ni, IpB));
    }

    [Fact]
    public void EncapsulateRequest_ProducesABroadcastArpFrame()
    {
        var layer = NewLayer();
        var ni = NewInterface("PC0", IpA);
        var request = layer.CreateRequest(ni, IpB);
        ArpEventArgs? seen = null;
        layer.RequestEncapsulated += (_, e) => seen = e;

        var frame = layer.EncapsulateRequest(request);

        Assert.Equal(EtherType.Arp, frame.EtherType);
        Assert.Equal(0x0806, frame.EtherType.Value);
        Assert.True(frame.IsBroadcast);
        Assert.Equal(MacAddress.Broadcast, frame.DestinationMac);
        Assert.Equal(ni.MacAddress!.Value, frame.SourceMac);
        Assert.Same(request, frame.Payload);
        Assert.Same(frame, seen!.Frame);
    }

    [Fact]
    public void EncapsulateReply_ProducesAUnicastFrameToTheRequester()
    {
        var layer = NewLayer();
        var owner = NewInterface("PC1", IpB);
        var requesterMac = MacAddress.Parse("00:11:22:33:44:55");
        var reply = layer.CreateReply(owner, IpB, requesterMac, IpA);

        var frame = layer.EncapsulateReply(reply);

        Assert.Equal(EtherType.Arp, frame.EtherType);
        Assert.True(frame.IsUnicast);
        Assert.Equal(requesterMac, frame.DestinationMac);
        Assert.Equal(owner.MacAddress!.Value, frame.SourceMac);
    }

    [Fact]
    public void EncapsulateRequest_RejectsAReply_AndViceVersa()
    {
        var layer = NewLayer();
        var ni = NewInterface("PC0", IpA);
        var request = layer.CreateRequest(ni, IpB);
        var reply = layer.CreateReply(ni, IpA, MacAddress.Parse("00:11:22:33:44:55"), IpB);

        Assert.Throws<DomainException>(() => layer.EncapsulateRequest(reply));
        Assert.Throws<DomainException>(() => layer.EncapsulateReply(request));
    }

    [Fact]
    public void TryDecapsulate_ReturnsTheArpFromAnArpFrame_AndFailsForOthers()
    {
        var layer = NewLayer();
        var ni = NewInterface("PC0", IpA);
        var request = layer.CreateRequest(ni, IpB);
        var arpFrame = layer.EncapsulateRequest(request);
        var ipv4Frame = EthernetFrame.Create(
            ni.MacAddress!.Value, MacAddress.Broadcast, EtherType.IPv4, RawPayloadStub());

        Assert.True(layer.TryDecapsulate(arpFrame, out var extracted));
        Assert.Same(request, extracted);
        Assert.False(layer.TryDecapsulate(ipv4Frame, out var none));
        Assert.Null(none);
    }

    private static RawPayload RawPayloadStub() => RawPayload.OfSize(4);

    [Fact]
    public void Resolve_CacheMiss_RaisesStarted_AndReturnsABroadcastRequestToSend()
    {
        var layer = NewLayer();
        var ni = NewInterface("PC0", IpA);
        var events = new List<string>();
        layer.ResolutionStarted += (_, _) => events.Add("started");
        layer.ResolutionSucceeded += (_, _) => events.Add("succeeded");
        layer.RequestCreated += (_, _) => events.Add("request");

        var resolution = layer.Resolve(ni, IpB);

        Assert.False(resolution.IsResolved);
        Assert.True(resolution.RequiresRequest);
        Assert.NotNull(resolution.Request);
        Assert.Equal(IpB, resolution.Request!.TargetProtocolAddress);
        Assert.NotNull(resolution.RequestFrame);
        Assert.True(resolution.RequestFrame!.IsBroadcast);
        Assert.Equal(["started", "request"], events);
    }

    [Fact]
    public void Resolve_CacheHit_RaisesSucceeded_AndGeneratesNoRequest()
    {
        var layer = NewLayer();
        var ni = NewInterface("PC0", IpA);
        ni.ArpCache.AddOrUpdateDynamic(IpB, MacAddress.Parse("AA:BB:CC:DD:EE:FF"));
        var requestRaised = false;
        ArpEventArgs? succeeded = null;
        layer.RequestCreated += (_, _) => requestRaised = true;
        layer.ResolutionSucceeded += (_, e) => succeeded = e;

        var resolution = layer.Resolve(ni, IpB);

        Assert.True(resolution.IsResolved);
        Assert.False(resolution.RequiresRequest);
        Assert.Equal(MacAddress.Parse("AA:BB:CC:DD:EE:FF"), resolution.HardwareAddress);
        Assert.False(requestRaised);
        Assert.Equal(IpB, succeeded!.ProtocolAddress);
    }

    [Fact]
    public void Resolve_InterfaceWithoutIPv4_Fails_AndRaisesResolutionFailed()
    {
        var layer = NewLayer();
        var ni = NewInterface("PC0", ip: null);
        ArpEventArgs? failed = null;
        layer.ResolutionFailed += (_, e) => failed = e;

        var resolution = layer.Resolve(ni, IpB);

        Assert.True(resolution.HasFailed);
        Assert.False(resolution.RequiresRequest);
        Assert.NotNull(resolution.FailureReason);
        Assert.Same(ArpDropReasons.InvalidSenderInterface, failed!.DropReason);
    }

    [Fact]
    public void HandleIncoming_Request_ForAnOwnedAddress_LearnsSender_AndGeneratesUnicastReply()
    {
        var layer = NewLayer();
        var owner = NewInterface("PC1", IpB);
        var requesterMac = MacAddress.Parse("00:11:22:33:44:55");
        var request = ArpPacket.CreateRequest(requesterMac, IpA, IpB);
        var learned = new List<ArpEventArgs>();
        var received = new List<ArpEventArgs>();
        layer.EntryLearned += (_, e) => learned.Add(e);
        layer.RequestReceived += (_, e) => received.Add(e);

        var report = layer.HandleIncoming(owner, request);

        Assert.True(report.IsSuccess);
        Assert.Equal(ArpOperation.Request, report.Operation);
        Assert.True(report.LocalInterfaceOwnsTarget);
        Assert.True(report.ReplyGenerated);
        Assert.Equal(requesterMac, report.ReplyFrame!.DestinationMac);
        Assert.Equal(IpB, report.Reply!.SenderProtocolAddress);
        Assert.Equal(owner.MacAddress!.Value, report.Reply.SenderHardwareAddress);

        // Learned the requester's mapping from the request itself.
        Assert.True(owner.ArpCache.TryResolve(IpA, out var mac));
        Assert.Equal(requesterMac, mac);
        Assert.Single(learned);
        Assert.Single(received);
    }

    [Fact]
    public void HandleIncoming_Request_ForAnAddressTheInterfaceDoesNotOwn_ProducesNoReply()
    {
        var layer = NewLayer();
        var owner = NewInterface("PC1", IpB);
        var request = ArpPacket.CreateRequest(MacAddress.Parse("00:11:22:33:44:55"), IpA, IpC);

        var report = layer.HandleIncoming(owner, request);

        Assert.True(report.IsSuccess);
        Assert.False(report.LocalInterfaceOwnsTarget);
        Assert.False(report.ReplyGenerated);
        Assert.Null(report.ReplyFrame);
    }

    [Fact]
    public void HandleIncoming_Reply_UpdatesTheCache_AndRaisesReplyReceived()
    {
        var layer = NewLayer();
        var ni = NewInterface("PC0", IpA);
        var replierMac = MacAddress.Parse("AA:BB:CC:DD:EE:FF");
        var reply = ArpPacket.CreateReply(replierMac, IpB, ni.MacAddress!.Value, IpA);
        ArpEventArgs? replyReceived = null;
        ArpEventArgs? learned = null;
        layer.ReplyReceived += (_, e) => replyReceived = e;
        layer.EntryLearned += (_, e) => learned = e;

        var report = layer.HandleIncoming(ni, reply);

        Assert.True(report.IsSuccess);
        Assert.Equal(ArpOperation.Reply, report.Operation);
        Assert.Equal(ArpCacheChange.Added, report.CacheChange);
        Assert.True(ni.ArpCache.TryResolve(IpB, out var mac));
        Assert.Equal(replierMac, mac);
        Assert.NotNull(replyReceived);
        Assert.NotNull(learned);
    }

    [Fact]
    public void HandleIncoming_Reply_WithAConflictingMapping_UpdatesAndFlagsConflict()
    {
        var layer = NewLayer();
        var ni = NewInterface("PC0", IpA);
        ni.ArpCache.AddOrUpdateDynamic(IpB, MacAddress.Parse("02:11:11:11:11:11"));
        var newMac = MacAddress.Parse("AA:BB:CC:DD:EE:FF");
        var reply = ArpPacket.CreateReply(newMac, IpB, ni.MacAddress!.Value, IpA);
        ArpEventArgs? updated = null;
        layer.EntryUpdated += (_, e) => updated = e;

        var report = layer.HandleIncoming(ni, reply);

        Assert.Equal(ArpCacheChange.Updated, report.CacheChange);
        Assert.True(report.CacheConflict);
        Assert.True(ni.ArpCache.TryResolve(IpB, out var mac));
        Assert.Equal(newMac, mac);
        Assert.NotNull(updated);
    }

    [Fact]
    public void HandleIncoming_NonArpFrame_IsDropped_WithNotArpReason()
    {
        var layer = NewLayer();
        var ni = NewInterface("PC0", IpA);
        var frame = EthernetFrame.Create(ni.MacAddress!.Value, MacAddress.Broadcast, EtherType.IPv4, RawPayload.OfSize(4));
        ArpEventArgs? dropped = null;
        layer.PacketDropped += (_, e) => dropped = e;

        var report = layer.HandleIncoming(ni, frame);

        Assert.False(report.IsSuccess);
        Assert.Same(ArpDropReasons.NotArp, report.DropReason);
        Assert.NotNull(dropped);
    }

    [Fact]
    public void Constructor_NullProcessor_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ArpLayer(null!));
    }
}
