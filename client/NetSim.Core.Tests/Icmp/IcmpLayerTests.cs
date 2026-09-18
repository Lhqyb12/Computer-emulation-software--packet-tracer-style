using System.Linq;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Icmp;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Icmp;

public class IcmpLayerTests
{
    private static readonly IPv4Address Pc0Ip = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address Pc1Ip = IPv4Address.Parse("192.168.1.20");
    private static readonly IPv4Address AbsentIp = IPv4Address.Parse("192.168.1.30");

    private static IcmpLayer NewLayer() => new(new IcmpProcessor());

    private static NetworkInterface NewInterface(string device, IPv4Address ip)
    {
        var pc = NetworkDeviceFactory.Create(DeviceType.Pc, device);
        var ni = pc.Interfaces.Single();
        ni.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(ip, 24));
        return ni;
    }

    [Fact]
    public void CreateEchoRequest_RaisesEchoRequestCreated()
    {
        var layer = NewLayer();
        IcmpEventArgs? seen = null;
        layer.EchoRequestCreated += (_, e) => seen = e;

        var request = layer.CreateEchoRequest(1234, 1, RawPayload.FromText("Hello"));

        Assert.True(request.IsEchoRequest);
        Assert.Same(request, seen!.Message);
    }

    [Fact]
    public void CreateEchoReply_PreservesRequestFields_AndRaisesEchoReplyCreated()
    {
        var layer = NewLayer();
        var request = layer.CreateEchoRequest(1234, 1, RawPayload.FromText("Hello"));
        IcmpEventArgs? seen = null;
        layer.EchoReplyCreated += (_, e) => seen = e;

        var reply = layer.CreateEchoReply(request);

        Assert.True(reply.IsEchoReply);
        Assert.Equal(request.Identifier, reply.Identifier);
        Assert.Equal(request.SequenceNumber, reply.SequenceNumber);
        Assert.Same(reply, seen!.Message);
    }

    [Fact]
    public void CreateEchoReply_GivenAReply_Throws()
    {
        var layer = NewLayer();
        var reply = IcmpMessage.CreateEchoReply(1, 1);

        Assert.Throws<DomainException>(() => layer.CreateEchoReply(reply));
    }

    [Fact]
    public void Encapsulate_WrapsTheMessageInAnIPv4PacketWithIcmpProtocol_AndRaisesEncapsulatedEvent()
    {
        var layer = NewLayer();
        var request = layer.CreateEchoRequest(1, 1);
        IcmpEventArgs? seen = null;
        layer.EchoRequestEncapsulated += (_, e) => seen = e;

        var ipPacket = layer.Encapsulate(request, Pc0Ip, Pc1Ip);

        Assert.Equal(ProtocolNumber.Icmp, ipPacket.Protocol);
        Assert.Same(request, ipPacket.Payload);
        Assert.Equal(Pc0Ip, ipPacket.SourceAddress);
        Assert.Equal(Pc1Ip, ipPacket.DestinationAddress);
        Assert.Same(ipPacket, seen!.IPv4Packet);
    }

    [Fact]
    public void TryDecapsulate_ReturnsTheIcmpFromAnIcmpProtocolPacket_AndFailsForOthers()
    {
        var layer = NewLayer();
        var request = layer.CreateEchoRequest(1, 1);
        var ipPacket = layer.Encapsulate(request, Pc0Ip, Pc1Ip);
        var otherProtocol = IPv4Packet.Create(Pc0Ip, Pc1Ip, RawPayload.OfSize(4), ProtocolNumber.Tcp);

        Assert.True(layer.TryDecapsulate(ipPacket, out var extracted));
        Assert.Same(request, extracted);
        Assert.False(layer.TryDecapsulate(otherProtocol, out var none));
        Assert.Null(none);
    }

    [Fact]
    public void HandleIncoming_EchoRequest_ForAnOwnedDestination_GeneratesAReplyWithMatchingIdentifierAndSequence()
    {
        var layer = NewLayer();
        var pc1 = NewInterface("PC1", Pc1Ip);
        var request = IcmpMessage.CreateEchoRequest(1234, 1, RawPayload.FromText("Hello"));
        var ipPacket = IPv4Packet.Create(Pc0Ip, Pc1Ip, request, ProtocolNumber.Icmp);
        var received = new List<IcmpEventArgs>();
        layer.EchoRequestReceived += (_, e) => received.Add(e);

        var report = layer.HandleIncoming(pc1, ipPacket);

        Assert.True(report.IsSuccess);
        Assert.True(report.LocalInterfaceOwnsDestination);
        Assert.True(report.ReplyGenerated);
        Assert.Equal(1234, report.Reply!.Identifier);
        Assert.Equal(1, report.Reply.SequenceNumber);
        Assert.Equal("Hello", System.Text.Encoding.UTF8.GetString(report.Reply.Data.Data.Span));
        Assert.Equal(Pc1Ip, report.ReplyPacket!.SourceAddress);
        Assert.Equal(Pc0Ip, report.ReplyPacket.DestinationAddress);
        Assert.Single(received);
    }

    [Fact]
    public void HandleIncoming_EchoRequest_ForAnAddressNotOwned_ProducesNoReply()
    {
        var layer = NewLayer();
        var pc1 = NewInterface("PC1", Pc1Ip);
        var request = IcmpMessage.CreateEchoRequest(1, 1);
        var ipPacket = IPv4Packet.Create(Pc0Ip, AbsentIp, request, ProtocolNumber.Icmp);

        var report = layer.HandleIncoming(pc1, ipPacket);

        Assert.True(report.IsSuccess);
        Assert.False(report.LocalInterfaceOwnsDestination);
        Assert.False(report.ReplyGenerated);
        Assert.Null(report.ReplyPacket);
    }

    [Fact]
    public void HandleIncoming_EchoReply_ReturnsAReportWithNoNewReply_AndRaisesEchoReplyReceived()
    {
        var layer = NewLayer();
        var pc0 = NewInterface("PC0", Pc0Ip);
        var reply = IcmpMessage.CreateEchoReply(1234, 1);
        var ipPacket = IPv4Packet.Create(Pc1Ip, Pc0Ip, reply, ProtocolNumber.Icmp);
        IcmpEventArgs? seen = null;
        layer.EchoReplyReceived += (_, e) => seen = e;

        var report = layer.HandleIncoming(pc0, ipPacket);

        Assert.True(report.IsSuccess);
        Assert.False(report.ReplyGenerated);
        Assert.Same(reply, report.Message);
        Assert.NotNull(seen);
    }

    [Fact]
    public void HandleIncoming_DestinationUnreachable_RaisesErrorMessageReceived()
    {
        var layer = NewLayer();
        var pc0 = NewInterface("PC0", Pc0Ip);
        var original = IcmpOriginalDatagramInfo.FromPacket(IPv4Packet.Create(Pc0Ip, Pc1Ip, RawPayload.OfSize(4), ProtocolNumber.Icmp));
        var error = IcmpMessage.CreateDestinationUnreachable(original, IcmpCode.HostUnreachable);
        var ipPacket = IPv4Packet.Create(Pc1Ip, Pc0Ip, error, ProtocolNumber.Icmp);
        IcmpEventArgs? seen = null;
        layer.ErrorMessageReceived += (_, e) => seen = e;

        var report = layer.HandleIncoming(pc0, ipPacket);

        Assert.True(report.IsSuccess);
        Assert.False(report.ReplyGenerated);
        Assert.NotNull(seen);
    }

    [Fact]
    public void HandleIncoming_NonIcmpProtocol_IsDropped_WithNotIcmpReason()
    {
        var layer = NewLayer();
        var pc1 = NewInterface("PC1", Pc1Ip);
        var ipPacket = IPv4Packet.Create(Pc0Ip, Pc1Ip, RawPayload.OfSize(4), ProtocolNumber.Tcp);
        IcmpEventArgs? dropped = null;
        layer.PacketDropped += (_, e) => dropped = e;

        var report = layer.HandleIncoming(pc1, ipPacket);

        Assert.False(report.IsSuccess);
        Assert.Same(IcmpDropReasons.NotIcmp, report.DropReason);
        Assert.NotNull(dropped);
    }

    [Fact]
    public void HandleIncoming_InvalidChecksum_IsDropped_WithInvalidChecksumReason()
    {
        var layer = NewLayer();
        var pc1 = NewInterface("PC1", Pc1Ip);
        var request = IcmpMessage.CreateEchoRequest(1, 1, RawPayload.FromText("Hello"));
        var corrupted = request.WithChecksum(unchecked((ushort)(request.Checksum + 1)));
        var ipPacket = IPv4Packet.Create(Pc0Ip, Pc1Ip, corrupted, ProtocolNumber.Icmp);
        IcmpEventArgs? dropped = null;
        layer.PacketDropped += (_, e) => dropped = e;

        var report = layer.HandleIncoming(pc1, ipPacket);

        Assert.False(report.IsSuccess);
        Assert.Same(IcmpDropReasons.InvalidChecksum, report.DropReason);
        Assert.NotNull(dropped);
    }

    [Fact]
    public void Process_ValidEchoRequest_RaisesPacketProcessed()
    {
        var layer = NewLayer();
        var request = layer.CreateEchoRequest(1, 1);
        var carrier = new Packet(new Pc("A"), new Pc("B"), "ICMP", request);
        IcmpEventArgs? processed = null;
        IcmpEventArgs? dropped = null;
        layer.PacketProcessed += (_, e) => processed = e;
        layer.PacketDropped += (_, e) => dropped = e;

        var result = layer.Process(carrier);

        Assert.True(result.IsSuccess);
        Assert.NotNull(processed);
        Assert.Null(dropped);
        Assert.Same(request, processed!.Message);
    }

    [Fact]
    public void Process_InvalidChecksum_RaisesPacketDropped_WithReason()
    {
        var layer = NewLayer();
        var request = IcmpMessage.CreateEchoRequest(1, 1);
        var corrupted = request.WithChecksum(unchecked((ushort)(request.Checksum + 1)));
        var carrier = new Packet(new Pc("A"), new Pc("B"), "ICMP", corrupted);
        IcmpEventArgs? dropped = null;
        layer.PacketDropped += (_, e) => dropped = e;

        var result = layer.Process(carrier);

        Assert.False(result.IsSuccess);
        Assert.NotNull(dropped);
        Assert.Same(IcmpDropReasons.InvalidChecksum, dropped!.DropReason);
    }

    [Fact]
    public void Constructor_NullProcessor_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new IcmpLayer(null!));
    }
}
