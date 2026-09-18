using NetSim.Core.Arp;
using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Arp;

/// <summary>
/// Phase 20 (brief sections 30 &amp; 50): IPv6 does not use ARP. An IPv6 packet / frame must never
/// be routed into the ARP engine, and there is no IPv6 -&gt; ARP behaviour anywhere.
/// </summary>
public class ArpIPv6SeparationTests
{
    private static readonly MacAddress SrcMac = MacAddress.Parse("00:11:22:33:44:55");
    private static readonly MacAddress DstMac = MacAddress.Parse("AA:BB:CC:DD:EE:FF");

    [Fact]
    public void ArpProcessor_RejectsAnIPv6Frame()
    {
        var ipv6 = IPv6Packet.Create(IPv6Address.Parse("2001:db8::1"), IPv6Address.Parse("2001:db8::2"));
        var frame = EthernetFrame.Create(SrcMac, DstMac, EtherType.IPv6, ipv6);
        var packet = new Packet(new Pc("A"), new Pc("B"), "IPv6", frame);

        var result = new ArpProcessor().Process(packet);

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
        Assert.Contains("does not carry ARP", result.Detail);
    }

    [Fact]
    public void ArpLayer_TryDecapsulate_IsFalseForAnIPv6Frame()
    {
        var layer = new ArpLayer(new ArpProcessor());
        var ipv6 = IPv6Packet.Create(IPv6Address.Parse("2001:db8::1"), IPv6Address.Parse("2001:db8::2"));
        var frame = EthernetFrame.Create(SrcMac, DstMac, EtherType.IPv6, ipv6);

        Assert.False(layer.TryDecapsulate(frame, out var arp));
        Assert.Null(arp);
    }

    [Fact]
    public void ArpLayer_HandleIncoming_DropsAnIPv6Frame_WithoutTouchingTheCache()
    {
        var layer = new ArpLayer(new ArpProcessor());
        var pc = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var ni = pc.Interfaces.Single();
        ni.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24));
        ni.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8::10"), 64));

        var ipv6 = IPv6Packet.Create(IPv6Address.Parse("2001:db8::1"), IPv6Address.Parse("2001:db8::10"));
        var frame = EthernetFrame.Create(SrcMac, ni.MacAddress!.Value, EtherType.IPv6, ipv6);

        var report = layer.HandleIncoming(ni, frame);

        Assert.False(report.IsSuccess);
        Assert.Same(ArpDropReasons.NotArp, report.DropReason);
        Assert.Equal(0, ni.ArpCache.Count);
    }

    [Fact]
    public void ConfiguringIPv6OnAnInterface_DoesNotPopulateTheArpCache()
    {
        var pc = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var ni = pc.Interfaces.Single();

        ni.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8::10"), 64));

        Assert.Equal(0, ni.ArpCache.Count);
    }
}
