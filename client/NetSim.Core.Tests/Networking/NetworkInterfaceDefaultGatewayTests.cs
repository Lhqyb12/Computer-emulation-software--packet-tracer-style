using System.Linq;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

/// <summary>
/// Phase 27 section 6/44: an interface's IPv4 default gateway - the host-side next hop for off-link
/// traffic.
/// </summary>
public class NetworkInterfaceDefaultGatewayTests
{
    private static NetworkInterface Nic()
    {
        var pc = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        return pc.Interfaces.First();
    }

    [Fact]
    public void NoGatewayByDefault()
    {
        Assert.Null(Nic().IPv4DefaultGateway);
    }

    [Fact]
    public void SetAndClear()
    {
        var nic = Nic();
        var gateway = IPv4Address.Parse("192.168.1.1");

        nic.SetIPv4DefaultGateway(gateway);
        Assert.Equal(gateway, nic.IPv4DefaultGateway);

        nic.SetIPv4DefaultGateway(null);
        Assert.Null(nic.IPv4DefaultGateway);
    }

    [Fact]
    public void ClearingIPv4Configuration_AlsoClearsTheGateway()
    {
        var nic = Nic();
        nic.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24));
        nic.SetIPv4DefaultGateway(IPv4Address.Parse("192.168.1.1"));

        nic.ClearIPv4Configuration();

        Assert.Null(nic.IPv4DefaultGateway);
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("224.0.0.1")]
    public void RejectsAnInvalidGatewayAddress(string address)
    {
        Assert.Throws<DomainException>(() => Nic().SetIPv4DefaultGateway(IPv4Address.Parse(address)));
    }
}
