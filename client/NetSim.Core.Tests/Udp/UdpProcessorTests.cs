using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Transport;
using NetSim.Core.Udp;

namespace NetSim.Core.Tests.Udp;

public class UdpProcessorTests
{
    private static readonly IPv4Address SourceIp = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address DestinationIp = IPv4Address.Parse("192.168.1.20");
    private static readonly MacAddress SourceMac = MacAddress.CreateRandomUnicast();
    private static readonly MacAddress DestinationMac = MacAddress.CreateRandomUnicast();

    private static UdpDatagram SampleDatagram() =>
        UdpDatagram.Create(SourceIp, DestinationIp, Port.Create(50000), Port.Create(5000), RawPayload.FromText("Hello UDP"));

    private static Packet WrapInPacket(IPacketPayload payload) =>
        new(new Pc("Src"), new Pc("Dst"), "UDP", payload);

    [Fact]
    public void Process_DatagramWrappedInIPv4_IsDelivered()
    {
        var datagram = SampleDatagram();
        var ip = IPv4Packet.Create(SourceIp, DestinationIp, datagram, ProtocolNumber.Udp);

        var result = new UdpProcessor().Process(WrapInPacket(ip));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
    }

    [Fact]
    public void Process_DatagramWrappedInIPv4AndEthernet_IsDelivered()
    {
        var datagram = SampleDatagram();
        var ip = IPv4Packet.Create(SourceIp, DestinationIp, datagram, ProtocolNumber.Udp);
        var frame = EthernetFrame.Create(SourceMac, DestinationMac, EtherType.IPv4, ip);

        var result = new UdpProcessor().Process(WrapInPacket(frame));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
    }

    [Fact]
    public void Process_BareDatagram_WithNoIPv4Context_IsInvalid()
    {
        var datagram = SampleDatagram();

        var result = new UdpProcessor().Process(WrapInPacket(datagram));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Process_IPv4PacketWithWrongProtocol_IsInvalid()
    {
        var ip = IPv4Packet.Create(SourceIp, DestinationIp, RawPayload.Empty, ProtocolNumber.Tcp);

        var result = new UdpProcessor().Process(WrapInPacket(ip));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Process_EthernetFrameWithWrongEtherType_IsInvalid()
    {
        var frame = EthernetFrame.Create(SourceMac, DestinationMac, EtherType.Arp, RawPayload.Empty);

        var result = new UdpProcessor().Process(WrapInPacket(frame));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Process_InvalidChecksum_IsDropped()
    {
        var datagram = SampleDatagram();
        var corrupted = datagram.WithChecksum(unchecked((ushort)(datagram.Checksum + 1)));
        var ip = IPv4Packet.Create(SourceIp, DestinationIp, corrupted, ProtocolNumber.Udp);

        var result = new UdpProcessor().Process(WrapInPacket(ip));

        Assert.Equal(PacketProcessingOutcome.Dropped, result.Outcome);
        Assert.Same(UdpDropReasons.InvalidChecksum, result.DropReason);
    }

    [Fact]
    public void Process_EndToEnd_ThroughPacketEngine()
    {
        var datagram = SampleDatagram();
        var ip = IPv4Packet.Create(SourceIp, DestinationIp, datagram, ProtocolNumber.Udp);
        var engine = new PacketEngine();
        var packet = engine.CreatePacket(new Pc("Src"), new Pc("Dst"), "UDP", ip);

        var result = engine.Process(packet, new UdpProcessor());

        Assert.True(result.IsSuccess);
        Assert.Equal(PacketState.Delivered, packet.State);
    }

    [Fact]
    public void Process_NullPacket_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new UdpProcessor().Process(null!));
    }
}
