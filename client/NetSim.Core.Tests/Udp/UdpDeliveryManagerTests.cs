using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Transport;
using NetSim.Core.Udp;

namespace NetSim.Core.Tests.Udp;

public class UdpDeliveryManagerTests
{
    private static readonly IPv4Address RemoteIp = IPv4Address.Parse("192.168.1.10");

    [Fact]
    public void Bind_ThenIsBound_ReturnsTrue()
    {
        var manager = new UdpDeliveryManager();
        var device = new Pc("Server");

        manager.Bind(device, Port.Create(5000));

        Assert.True(manager.IsBound(device, Port.Create(5000)));
    }

    [Fact]
    public void Bind_SamePortTwice_Throws()
    {
        var manager = new UdpDeliveryManager();
        var device = new Pc("Server");
        manager.Bind(device, Port.Create(5000));

        Assert.Throws<DomainException>(() => manager.Bind(device, Port.Create(5000)));
    }

    [Fact]
    public void Bind_DifferentDevicesOnSamePort_BothSucceed()
    {
        var manager = new UdpDeliveryManager();
        var deviceA = new Pc("A");
        var deviceB = new Pc("B");

        manager.Bind(deviceA, Port.Create(5000));
        manager.Bind(deviceB, Port.Create(5000));

        Assert.True(manager.IsBound(deviceA, Port.Create(5000)));
        Assert.True(manager.IsBound(deviceB, Port.Create(5000)));
    }

    [Fact]
    public void Unbind_RemovesTheBinding()
    {
        var manager = new UdpDeliveryManager();
        var device = new Pc("Server");
        manager.Bind(device, Port.Create(5000));

        Assert.True(manager.Unbind(device, Port.Create(5000)));
        Assert.False(manager.IsBound(device, Port.Create(5000)));
    }

    [Fact]
    public void Unbind_NotBound_ReturnsFalse()
    {
        var manager = new UdpDeliveryManager();

        Assert.False(manager.Unbind(new Pc("Server"), Port.Create(5000)));
    }

    [Fact]
    public void Deliver_ToABoundEndpoint_Succeeds_AndInvokesCallback()
    {
        var manager = new UdpDeliveryManager();
        var device = new Pc("Server");
        UdpReceivedDatagram? received = null;
        manager.Bind(device, Port.Create(5000), r => received = r);

        var datagram = UdpDatagram.Create(RemoteIp, IPv4Address.Parse("192.168.1.20"), Port.Create(50000), Port.Create(5000), RawPayload.FromText("Hello UDP"));
        var result = manager.Deliver(device, datagram, RemoteIp, Port.Create(50000));

        Assert.True(result.IsDelivered);
        Assert.NotNull(received);
        Assert.Equal(RemoteIp, received!.RemoteAddress);
        Assert.Equal(50000, received.RemotePort.Value);
        Assert.Same(datagram, received.Datagram);
    }

    [Fact]
    public void Deliver_ToAnUnboundPort_ReportsPortUnavailable()
    {
        var manager = new UdpDeliveryManager();
        var device = new Pc("Server");
        var datagram = UdpDatagram.Create(RemoteIp, IPv4Address.Parse("192.168.1.20"), Port.Create(50000), Port.Create(9999));

        var result = manager.Deliver(device, datagram, RemoteIp, Port.Create(50000));

        Assert.False(result.IsDelivered);
        Assert.Null(result.Binding);
    }

    [Fact]
    public void Deliver_RecordsReceivedDatagramsOnTheBinding()
    {
        var manager = new UdpDeliveryManager();
        var device = new Pc("Server");
        var binding = manager.Bind(device, Port.Create(5000));
        var datagram = UdpDatagram.Create(RemoteIp, IPv4Address.Parse("192.168.1.20"), Port.Create(50000), Port.Create(5000));

        manager.Deliver(device, datagram, RemoteIp, Port.Create(50000));

        Assert.Single(binding.ReceivedDatagrams);
    }

    [Fact]
    public void GetBindings_ReturnsOnlyThatDevicesBindings()
    {
        var manager = new UdpDeliveryManager();
        var deviceA = new Pc("A");
        var deviceB = new Pc("B");
        manager.Bind(deviceA, Port.Create(1000));
        manager.Bind(deviceA, Port.Create(2000));
        manager.Bind(deviceB, Port.Create(1000));

        var bindingsA = manager.GetBindings(deviceA);

        Assert.Equal(2, bindingsA.Count);
        Assert.All(bindingsA, b => Assert.Same(deviceA, b.Device));
    }
}
