using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tests.Packets;

namespace NetSim.Core.Tests.Ethernet;

public class EthernetFrameTests
{
    private static readonly MacAddress Src = MacAddress.Parse("00:11:22:33:44:55");
    private static readonly MacAddress Dst = MacAddress.Parse("00:AA:BB:CC:DD:EE");

    [Fact]
    public void Create_SetsHeaderAndPayload()
    {
        var payload = RawPayload.FromText("hello");

        var frame = EthernetFrame.Create(Src, Dst, EtherType.IPv4, payload);

        Assert.Equal(Src, frame.SourceMac);
        Assert.Equal(Dst, frame.DestinationMac);
        Assert.Equal(EtherType.IPv4, frame.EtherType);
        Assert.Same(payload, frame.Payload);
        Assert.Same(payload, frame.EncapsulatedPayload);
    }

    [Fact]
    public void Create_WithNoPayload_UsesEmptyLeaf()
    {
        var frame = EthernetFrame.Create(Src, Dst, EtherType.Arp);

        Assert.Same(RawPayload.Empty, frame.Payload);
        Assert.Equal(EthernetFrame.HeaderSizeBytes, frame.Length);
    }

    [Fact]
    public void IsAPacketPayload_WithEthernetTypeAndAggregatedLength()
    {
        var frame = EthernetFrame.Create(Src, Dst, EtherType.IPv4, RawPayload.OfSize(100));

        Assert.IsAssignableFrom<IPacketPayload>(frame);
        Assert.Equal("Ethernet", frame.PayloadType);
        Assert.Equal(EthernetFrame.HeaderSizeBytes + 100, frame.Length);
    }

    [Fact]
    public void DestinationClassification_ReflectsTheDestinationMac()
    {
        var unicast = EthernetFrame.Create(Src, Dst, EtherType.IPv4);
        var broadcast = EthernetFrame.Create(Src, MacAddress.Broadcast, EtherType.IPv4);
        var multicast = EthernetFrame.Create(Src, MacAddress.Parse("01:00:5E:00:00:FB"), EtherType.IPv4);

        Assert.True(unicast.IsUnicast);
        Assert.Equal(MacAddressKind.Unicast, unicast.DestinationKind);

        Assert.True(broadcast.IsBroadcast);
        Assert.False(broadcast.IsUnicast);
        Assert.Equal(MacAddressKind.Broadcast, broadcast.DestinationKind);

        Assert.True(multicast.IsMulticast);
        Assert.Equal(MacAddressKind.Multicast, multicast.DestinationKind);
    }

    [Fact]
    public void Create_RejectsANonUnicastSourceMac()
    {
        Assert.Throws<DomainException>(() => EthernetFrame.Create(MacAddress.Broadcast, Dst, EtherType.IPv4));
        Assert.Throws<DomainException>(() => EthernetFrame.Create(MacAddress.Parse("01:00:5E:00:00:01"), Dst, EtherType.IPv4));
    }

    [Fact]
    public void Create_RejectsAnUnsetEtherType()
    {
        Assert.Throws<DomainException>(() => EthernetFrame.Create(Src, Dst, EtherType.Unspecified));
    }

    [Fact]
    public void Validate_IsValid_ForAWellFormedFrame()
    {
        var frame = EthernetFrame.Create(Src, Dst, EtherType.IPv4, RawPayload.OfSize(64));

        Assert.True(frame.Validate().IsValid);
    }

    [Fact]
    public void Validate_SurfacesABrokenEncapsulatedPayload()
    {
        // Create only guards the header, so a frame can be built around a payload that later
        // reports itself invalid; Validate() (and the packet engine) is where that is caught.
        var badInner = new StubPayload { ValidationResult = PacketValidationResult.Invalid("truncated header") };
        var frame = EthernetFrame.Create(Src, Dst, EtherType.IPv4, badInner);

        var result = frame.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("truncated header", result.Errors);
    }

    [Fact]
    public void Frame_RidesInsideAGenericPacket()
    {
        var frame = EthernetFrame.Create(Src, Dst, EtherType.IPv4, RawPayload.FromText("payload"));
        var packet = new Packet(new Pc("A"), new Pc("B"), "Ethernet", frame);

        Assert.Same(frame, packet.Payload);
        Assert.Equal("Ethernet", packet.Payload!.PayloadType);
        Assert.True(packet.Validate().IsValid);
    }
}
