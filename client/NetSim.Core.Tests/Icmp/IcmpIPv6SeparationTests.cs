using NetSim.Core.Ethernet;
using NetSim.Core.Icmp;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Icmp;

/// <summary>
/// Phase 21 (brief sections 32 &amp; 47): this phase implements ICMP for IPv4 only. An IPv6 packet
/// / frame must never be routed into the ICMP engine, and IPv6 never touches
/// <see cref="ProtocolNumber.Icmp"/> - IPv6 will use ICMPv6 in a later phase.
/// </summary>
public class IcmpIPv6SeparationTests
{
    private static readonly MacAddress SrcMac = MacAddress.Parse("00:11:22:33:44:55");
    private static readonly MacAddress DstMac = MacAddress.Parse("AA:BB:CC:DD:EE:FF");

    [Fact]
    public void IcmpProcessor_RejectsAnIPv6Frame()
    {
        var ipv6 = IPv6Packet.Create(IPv6Address.Parse("2001:db8::1"), IPv6Address.Parse("2001:db8::2"));
        var frame = EthernetFrame.Create(SrcMac, DstMac, EtherType.IPv6, ipv6);
        var packet = new Packet(new NetSim.Core.Devices.Pc("A"), new NetSim.Core.Devices.Pc("B"), "IPv6", frame);

        var result = new IcmpProcessor().Process(packet);

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
        Assert.Contains("does not carry IPv4", result.Detail);
    }

    [Fact]
    public void IcmpLayer_TryDecapsulate_IsFalseForAnIPv6Packet()
    {
        var layer = new IcmpLayer(new IcmpProcessor());
        var ipv4WithTcp = IPv4Packet.Create(
            IPv4Address.Parse("192.168.1.10"), IPv4Address.Parse("192.168.1.20"), RawPayload.OfSize(4), ProtocolNumber.Tcp);

        Assert.False(layer.TryDecapsulate(ipv4WithTcp, out var icmp));
        Assert.Null(icmp);
    }
}
