using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

/// <summary>
/// Phase 20: ARP state hangs off the <see cref="NetworkInterface"/> and is scoped per interface, so
/// a multi-interface device never mixes IPv4 -&gt; MAC mappings across unrelated Layer 2 domains.
/// </summary>
public class NetworkInterfaceArpTests
{
    [Fact]
    public void EveryInterface_HasItsOwnEmptyArpCache()
    {
        var pc = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var ethernet0 = pc.Interfaces.Single();

        Assert.NotNull(ethernet0.ArpCache);
        Assert.Equal(0, ethernet0.ArpCache.Count);
        Assert.Empty(ethernet0.ArpCache.Entries);
    }

    [Fact]
    public void ArpCache_ReferenceIsStable()
    {
        var pc = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var ethernet0 = pc.Interfaces.Single();

        Assert.Same(ethernet0.ArpCache, ethernet0.ArpCache);
    }

    [Fact]
    public void MultiInterfaceDevice_KeepsArpStatePerInterface()
    {
        var router = NetworkDeviceFactory.Create(DeviceType.Router, "Router0");
        var g0 = router.Interfaces.Single(i => i.Name == "GigabitEthernet0/0");
        var g1 = router.Interfaces.Single(i => i.Name == "GigabitEthernet0/1");

        g0.ArpCache.AddOrUpdateDynamic(IPv4Address.Parse("192.168.1.10"), MacAddress.Parse("00:11:22:33:44:55"));

        Assert.NotSame(g0.ArpCache, g1.ArpCache);
        Assert.True(g0.ArpCache.Contains(IPv4Address.Parse("192.168.1.10")));
        Assert.False(g1.ArpCache.Contains(IPv4Address.Parse("192.168.1.10")));
        Assert.Equal(0, g1.ArpCache.Count);
    }
}
