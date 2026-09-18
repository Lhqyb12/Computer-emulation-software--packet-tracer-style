using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tcp;
using NetSim.Core.Transport;

namespace NetSim.Core.Tests.Tcp;

public class TcpProcessorTests
{
    private static readonly IPv4Address SourceIp = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address DestinationIp = IPv4Address.Parse("192.168.1.20");
    private static readonly MacAddress SourceMac = MacAddress.CreateRandomUnicast();
    private static readonly MacAddress DestinationMac = MacAddress.CreateRandomUnicast();

    private static TcpSegment SampleSyn() =>
        TcpSegment.CreateSyn(SourceIp, DestinationIp, Port.Create(50000), Port.Create(80), 1000);

    private static Packet WrapInPacket(IPacketPayload payload) =>
        new(new Pc("Src"), new Pc("Dst"), "TCP", payload);

    [Fact]
    public void Process_SegmentWrappedInIPv4_IsDelivered()
    {
        var segment = SampleSyn();
        var ip = IPv4Packet.Create(SourceIp, DestinationIp, segment, ProtocolNumber.Tcp);

        var result = new TcpProcessor().Process(WrapInPacket(ip));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
    }

    [Fact]
    public void Process_SegmentWrappedInIPv4AndEthernet_IsDelivered()
    {
        var segment = SampleSyn();
        var ip = IPv4Packet.Create(SourceIp, DestinationIp, segment, ProtocolNumber.Tcp);
        var frame = EthernetFrame.Create(SourceMac, DestinationMac, EtherType.IPv4, ip);

        var result = new TcpProcessor().Process(WrapInPacket(frame));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
    }

    [Fact]
    public void Process_BareSegment_WithNoIPv4Context_IsInvalid()
    {
        var result = new TcpProcessor().Process(WrapInPacket(SampleSyn()));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Process_IPv4PacketWithWrongProtocol_IsInvalid()
    {
        var ip = IPv4Packet.Create(SourceIp, DestinationIp, RawPayload.Empty, ProtocolNumber.Udp);

        var result = new TcpProcessor().Process(WrapInPacket(ip));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Process_EthernetFrameWithWrongEtherType_IsInvalid()
    {
        var frame = EthernetFrame.Create(SourceMac, DestinationMac, EtherType.Arp, RawPayload.Empty);

        var result = new TcpProcessor().Process(WrapInPacket(frame));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Process_InvalidChecksum_IsDropped()
    {
        var segment = SampleSyn();
        var corrupted = segment.WithChecksum(unchecked((ushort)(segment.Checksum + 1)));
        var ip = IPv4Packet.Create(SourceIp, DestinationIp, corrupted, ProtocolNumber.Tcp);

        var result = new TcpProcessor().Process(WrapInPacket(ip));

        Assert.Equal(PacketProcessingOutcome.Dropped, result.Outcome);
        Assert.Same(TcpDropReasons.InvalidChecksum, result.DropReason);
    }

    [Fact]
    public void Process_InvalidFlagCombination_IsDropped()
    {
        var invalid = TcpSegment.Create(SourceIp, DestinationIp, Port.Create(1), Port.Create(2), 1, 0, TcpFlags.Syn | TcpFlags.Rst);
        var ip = IPv4Packet.Create(SourceIp, DestinationIp, invalid, ProtocolNumber.Tcp);

        var result = new TcpProcessor().Process(WrapInPacket(ip));

        Assert.Equal(PacketProcessingOutcome.Dropped, result.Outcome);
        Assert.Same(TcpDropReasons.InvalidPacket, result.DropReason);
    }

    [Fact]
    public void Process_EndToEnd_ThroughPacketEngine()
    {
        var segment = SampleSyn();
        var ip = IPv4Packet.Create(SourceIp, DestinationIp, segment, ProtocolNumber.Tcp);
        var engine = new PacketEngine();
        var packet = engine.CreatePacket(new Pc("Src"), new Pc("Dst"), "TCP", ip);

        var result = engine.Process(packet, new TcpProcessor());

        Assert.True(result.IsSuccess);
        Assert.Equal(PacketState.Delivered, packet.State);
    }

    [Fact]
    public void Process_NullPacket_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TcpProcessor().Process(null!));
    }
}
