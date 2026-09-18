using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Transport;

namespace NetSim.Core.Tests.Transport;

public class TransportEndpointTests
{
    private static readonly IPv4Address Address = IPv4Address.Parse("192.168.1.20");

    [Fact]
    public void SameAddressPortAndProtocol_AreEqual()
    {
        var a = new TransportEndpoint(Address, Port.Create(80), TransportProtocol.Tcp);
        var b = new TransportEndpoint(Address, Port.Create(80), TransportProtocol.Tcp);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void DifferentPort_AreNotEqual()
    {
        var a = new TransportEndpoint(Address, Port.Create(80), TransportProtocol.Tcp);
        var b = new TransportEndpoint(Address, Port.Create(443), TransportProtocol.Tcp);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void SameAddressAndPort_DifferentProtocol_AreNotEqual()
    {
        var tcp = new TransportEndpoint(Address, Port.Create(53), TransportProtocol.Tcp);
        var udp = new TransportEndpoint(Address, Port.Create(53), TransportProtocol.Udp);

        Assert.NotEqual(tcp, udp);
    }

    [Fact]
    public void DifferentAddress_AreNotEqual()
    {
        var a = new TransportEndpoint(Address, Port.Create(80), TransportProtocol.Tcp);
        var b = new TransportEndpoint(IPv4Address.Parse("192.168.1.30"), Port.Create(80), TransportProtocol.Tcp);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ToString_RendersAddressPortAndProtocol()
    {
        var endpoint = new TransportEndpoint(Address, Port.Create(80), TransportProtocol.Tcp);

        Assert.Equal("192.168.1.20:80/TCP", endpoint.ToString());
    }

    [Fact]
    public void ToProtocolNumber_MapsToTheCorrectIPv4ProtocolNumber()
    {
        Assert.Equal(ProtocolNumber.Tcp, TransportProtocol.Tcp.ToProtocolNumber());
        Assert.Equal(ProtocolNumber.Udp, TransportProtocol.Udp.ToProtocolNumber());
    }

    [Fact]
    public void FromProtocolNumber_RoundTrips()
    {
        Assert.Equal(TransportProtocol.Tcp, TransportProtocolExtensions.FromProtocolNumber(ProtocolNumber.Tcp));
        Assert.Equal(TransportProtocol.Udp, TransportProtocolExtensions.FromProtocolNumber(ProtocolNumber.Udp));
        Assert.Null(TransportProtocolExtensions.FromProtocolNumber(ProtocolNumber.Icmp));
    }
}
