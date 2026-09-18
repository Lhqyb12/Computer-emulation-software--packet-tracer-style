using NetSim.Core.Common;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Devices;

public class NetworkDeviceFactoryTests
{
    [Fact]
    public void Create_Router_HasGigabitAndSerialDefaultInterfaces()
    {
        var router = NetworkDeviceFactory.Create(DeviceType.Router, "Router0");

        Assert.IsType<Router>(router);
        Assert.Equal(
            ["GigabitEthernet0/0", "GigabitEthernet0/1", "Serial0/0"],
            router.Interfaces.Select(i => i.Name).OrderBy(n => n));
    }

    [Fact]
    public void Create_Switch_HasMultipleFastEthernetDefaultInterfaces()
    {
        var sw = NetworkDeviceFactory.Create(DeviceType.Switch, "Switch0");

        Assert.IsType<Switch>(sw);
        Assert.True(sw.Interfaces.Count > 1);
        Assert.All(sw.Interfaces, i => Assert.Equal(InterfaceType.FastEthernet, i.InterfaceType));
    }

    [Theory]
    [InlineData(DeviceType.Pc)]
    [InlineData(DeviceType.Laptop)]
    [InlineData(DeviceType.Server)]
    public void Create_EndDevice_HasExactlyOneEthernetInterface(DeviceType deviceType)
    {
        var device = NetworkDeviceFactory.Create(deviceType, "Device0");

        var single = Assert.Single(device.Interfaces);
        Assert.Equal(InterfaceType.Ethernet, single.InterfaceType);
    }

    [Fact]
    public void Create_UnsupportedDeviceType_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NetworkDeviceFactory.Create((DeviceType)999, "X"));
    }

    [Fact]
    public void CreateWithoutDefaults_ReturnsDeviceWithNoInterfaces()
    {
        var router = NetworkDeviceFactory.CreateWithoutDefaults(DeviceType.Router, "Router0");

        Assert.Empty(router.Interfaces);
    }

    [Fact]
    public void RehydrateWithoutDefaults_PreservesGivenId()
    {
        var id = EntityId.New();

        var router = NetworkDeviceFactory.RehydrateWithoutDefaults(id, DeviceType.Router, "Router0");

        Assert.Equal(id, router.Id);
        Assert.Empty(router.Interfaces);
    }

    [Fact]
    public void Create_TwoDevicesOfSameType_HaveDifferentIds()
    {
        var first = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var second = NetworkDeviceFactory.Create(DeviceType.Pc, "PC1");

        Assert.NotEqual(first.Id, second.Id);
    }
}
