using System.Linq;
using System.Text;
using NetSim.Core.Arp;
using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Topology;
using NetSim.Core.Transport;
using NetSim.Core.Udp;

namespace NetSim.Core.Tests.Udp;

/// <summary>
/// Phase 22 integration (brief section 39): the full "PC0 sends PC1 a UDP datagram" flow -
/// datagram -&gt; IPv4 -&gt; ARP (if needed) -&gt; Ethernet -&gt; PC1 recognises it owns the destination
/// and finds a bound endpoint -&gt; payload delivered. No handshake anywhere in this flow. Mirrors
/// <c>IcmpEchoIntegrationTests</c>.
/// </summary>
public class UdpIntegrationTests
{
    private static readonly IPv4Address Pc0Ip = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address Pc1Ip = IPv4Address.Parse("192.168.1.20");

    private sealed record Lab(
        Network Network, EthernetTransmissionService Ethernet, IPv4Layer Ipv4, ArpLayer Arp,
        UdpLayer Udp, UdpDeliveryManager Delivery, NetworkInterface Pc0, NetworkInterface Pc1);

    private static Lab BuildLab()
    {
        var network = new Network("Lab");
        var ethernet = new EthernetTransmissionService(new PacketEngine());
        var ipv4 = new IPv4Layer(new IPv4Processor());
        var arp = new ArpLayer(new ArpProcessor());
        var delivery = new UdpDeliveryManager();
        var udp = new UdpLayer(new UdpProcessor(), delivery);

        var pc0 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var pc1 = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");
        network.AddDevice(pc0);
        network.AddDevice(pc1);

        var g0 = pc0.Interfaces.Single();
        var g1 = pc1.Interfaces.Single();
        g0.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(Pc0Ip, 24));
        g1.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(Pc1Ip, 24));
        network.Connect(g0, g1);
        g0.BringUp();
        g1.BringUp();

        return new Lab(network, ethernet, ipv4, arp, udp, delivery, g0, g1);
    }

    private static EthernetFrame DeliveredFrame(EthernetTransmissionResult result) =>
        Assert.IsType<EthernetFrame>(result.Packet!.Payload);

    private static MacAddress ResolvePc1Mac(Lab lab)
    {
        var resolution = lab.Arp.Resolve(lab.Pc0, Pc1Ip);
        if (resolution.IsResolved)
        {
            return resolution.HardwareAddress!.Value;
        }

        var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, resolution.RequestFrame!);
        var pc1Report = lab.Arp.HandleIncoming(lab.Pc1, DeliveredFrame(toPc1));
        var toPc0 = lab.Ethernet.Transmit(lab.Network, lab.Pc1, pc1Report.ReplyFrame!);
        lab.Arp.HandleIncoming(lab.Pc0, DeliveredFrame(toPc0));

        Assert.True(lab.Pc0.ArpCache.TryResolve(Pc1Ip, out var mac));
        return mac;
    }

    [Fact]
    public void FullDatagram_Pc0ToPc1_DeliversPayloadToTheBoundEndpoint()
    {
        var lab = BuildLab();
        lab.Delivery.Bind(lab.Pc1.Device, Port.Create(5000));
        var pc1Mac = ResolvePc1Mac(lab);

        var datagram = lab.Udp.CreateDatagram(Pc0Ip, Pc1Ip, Port.Create(50000), Port.Create(5000), RawPayload.FromText("Hello UDP"));
        var requestIp = lab.Udp.Encapsulate(datagram, Pc0Ip, Pc1Ip);
        Assert.Equal(ProtocolNumber.Udp, requestIp.Protocol);

        var requestFrame = lab.Ipv4.Encapsulate(requestIp, lab.Pc0.MacAddress!.Value, pc1Mac);
        var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, requestFrame);
        Assert.True(toPc1.IsSuccess);

        var deliveredIp = Assert.IsType<IPv4Packet>(DeliveredFrame(toPc1).Payload);
        var report = lab.Udp.HandleIncoming(lab.Pc1, deliveredIp);

        Assert.True(report.IsSuccess);
        Assert.True(report.IsDelivered);
        Assert.True(lab.Delivery.TryFindBinding(lab.Pc1.Device, Port.Create(5000), out var binding));
        var received = Assert.Single(binding!.ReceivedDatagrams);
        Assert.Equal(Pc0Ip, received.RemoteAddress);
        Assert.Equal(50000, received.RemotePort.Value);
        Assert.Equal("Hello UDP", Encoding.UTF8.GetString(((RawPayload)received.Datagram.Payload).Data.Span));
    }

    [Fact]
    public void Datagram_ToAPortWithNoBoundEndpoint_IsReportedAsPortUnavailable_WithoutCrashing()
    {
        var lab = BuildLab();
        var pc1Mac = ResolvePc1Mac(lab);

        var datagram = lab.Udp.CreateDatagram(Pc0Ip, Pc1Ip, Port.Create(50000), Port.Create(9999));
        var requestIp = lab.Udp.Encapsulate(datagram, Pc0Ip, Pc1Ip);
        var requestFrame = lab.Ipv4.Encapsulate(requestIp, lab.Pc0.MacAddress!.Value, pc1Mac);
        var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, requestFrame);
        var deliveredIp = Assert.IsType<IPv4Packet>(DeliveredFrame(toPc1).Payload);

        var report = lab.Udp.HandleIncoming(lab.Pc1, deliveredIp);

        Assert.True(report.IsSuccess);
        Assert.False(report.IsDelivered);
    }

    [Fact]
    public void CorruptedDatagram_IsDroppedByTheProcessor_WithoutCrashing()
    {
        var lab = BuildLab();
        lab.Delivery.Bind(lab.Pc1.Device, Port.Create(5000));
        var pc1Mac = ResolvePc1Mac(lab);

        var datagram = lab.Udp.CreateDatagram(Pc0Ip, Pc1Ip, Port.Create(50000), Port.Create(5000), RawPayload.FromText("Hello"));
        var corrupted = datagram.WithChecksum(unchecked((ushort)(datagram.Checksum + 1)));
        var requestIp = lab.Udp.Encapsulate(corrupted, Pc0Ip, Pc1Ip);
        var requestFrame = lab.Ipv4.Encapsulate(requestIp, lab.Pc0.MacAddress!.Value, pc1Mac);
        var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, requestFrame);
        var deliveredIp = Assert.IsType<IPv4Packet>(DeliveredFrame(toPc1).Payload);

        var report = lab.Udp.HandleIncoming(lab.Pc1, deliveredIp);

        Assert.False(report.IsSuccess);
        Assert.Same(UdpDropReasons.InvalidChecksum, report.DropReason);
    }

    [Fact]
    public void MultipleUdpEndpoints_OnTheSameDevice_EachReceiveTheirOwnTraffic()
    {
        var lab = BuildLab();
        lab.Delivery.Bind(lab.Pc1.Device, Port.Create(5000));
        lab.Delivery.Bind(lab.Pc1.Device, Port.Create(6000));
        var pc1Mac = ResolvePc1Mac(lab);

        foreach (var port in new[] { 5000, 6000 })
        {
            var datagram = lab.Udp.CreateDatagram(Pc0Ip, Pc1Ip, Port.Create(50000), Port.Create(port), RawPayload.FromText($"to-{port}"));
            var requestIp = lab.Udp.Encapsulate(datagram, Pc0Ip, Pc1Ip);
            var requestFrame = lab.Ipv4.Encapsulate(requestIp, lab.Pc0.MacAddress!.Value, pc1Mac);
            var toPc1 = lab.Ethernet.Transmit(lab.Network, lab.Pc0, requestFrame);
            var deliveredIp = Assert.IsType<IPv4Packet>(DeliveredFrame(toPc1).Payload);

            var report = lab.Udp.HandleIncoming(lab.Pc1, deliveredIp);
            Assert.True(report.IsDelivered);
        }

        Assert.True(lab.Delivery.TryFindBinding(lab.Pc1.Device, Port.Create(5000), out var binding5000));
        Assert.True(lab.Delivery.TryFindBinding(lab.Pc1.Device, Port.Create(6000), out var binding6000));
        Assert.Single(binding5000!.ReceivedDatagrams);
        Assert.Single(binding6000!.ReceivedDatagrams);
    }
}
