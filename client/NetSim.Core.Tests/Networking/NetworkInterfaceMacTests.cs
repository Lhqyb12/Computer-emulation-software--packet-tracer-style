using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

/// <summary>
/// Phase 17: an Ethernet-capable <see cref="NetworkInterface"/> is born with a stable
/// locally-administered unicast MAC; non-Ethernet media have none.
/// </summary>
public class NetworkInterfaceMacTests
{
    [Theory]
    [InlineData(InterfaceType.Ethernet)]
    [InlineData(InterfaceType.FastEthernet)]
    [InlineData(InterfaceType.GigabitEthernet)]
    public void EthernetCapableInterface_GetsALocallyAdministeredUnicastMac(InterfaceType type)
    {
        var iface = new Switch("S1").AddInterface("Port1", type);

        Assert.True(iface.SupportsEthernet);
        Assert.NotNull(iface.MacAddress);
        Assert.True(iface.MacAddress!.Value.IsUnicast);
        Assert.True(iface.MacAddress!.Value.IsLocallyAdministered);
        Assert.False(iface.MacAddress!.Value.IsBroadcast);
    }

    [Theory]
    [InlineData(InterfaceType.Serial)]
    [InlineData(InterfaceType.Console)]
    public void NonEthernetInterface_HasNoMac(InterfaceType type)
    {
        var iface = new Router("R1").AddInterface(type == InterfaceType.Serial ? "Serial0/0" : "Console0", type);

        Assert.False(iface.SupportsEthernet);
        Assert.Null(iface.MacAddress);
    }

    [Fact]
    public void EachInterface_GetsADistinctMac()
    {
        var sw = new Switch("S1");
        var macs = Enumerable.Range(0, 8)
            .Select(i => sw.AddInterface($"Port{i}", InterfaceType.FastEthernet).MacAddress)
            .ToList();

        Assert.All(macs, m => Assert.NotNull(m));
        Assert.Equal(macs.Count, macs.Select(m => m!.Value).Distinct().Count());
    }

    [Fact]
    public void Mac_IsStableForTheLifetimeOfTheInterface()
    {
        var iface = new Pc("PC1").AddInterface("Ethernet0", InterfaceType.Ethernet);
        var original = iface.MacAddress;

        iface.BringUp();
        iface.SetDescription("uplink");
        iface.Disable();
        iface.Enable();

        Assert.Equal(original, iface.MacAddress);
    }

    [Fact]
    public void SetMacAddress_RestoresAPersistedValue()
    {
        var iface = new Pc("PC1").AddInterface("Ethernet0", InterfaceType.Ethernet);
        var saved = MacAddress.Parse("02:AA:BB:CC:DD:EE");

        iface.SetMacAddress(saved);

        Assert.Equal(saved, iface.MacAddress);
    }
}
