using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Devices;

public class DeviceInterfaceCatalogTests
{
    [Fact]
    public void Router_DefinesGigabitAndSerialInterfaces()
    {
        var definitions = DeviceInterfaceCatalog.For(DeviceType.Router);

        Assert.Equal(
            ["GigabitEthernet0/0", "GigabitEthernet0/1", "Serial0/0"],
            definitions.Select(d => d.Name));
        Assert.Equal(InterfaceType.Serial, definitions.Single(d => d.Name == "Serial0/0").InterfaceType);
    }

    [Fact]
    public void Switch_DefinesEightFastEthernetPorts()
    {
        var definitions = DeviceInterfaceCatalog.For(DeviceType.Switch);

        Assert.Equal(8, definitions.Count);
        Assert.All(definitions, d => Assert.Equal(InterfaceType.FastEthernet, d.InterfaceType));
        Assert.Equal("FastEthernet0/1", definitions[0].Name);
        Assert.Equal("FastEthernet0/8", definitions[7].Name);
    }

    [Theory]
    [InlineData(DeviceType.Pc)]
    [InlineData(DeviceType.Laptop)]
    [InlineData(DeviceType.Server)]
    public void EndDevices_DefineASingleEthernetInterface(DeviceType deviceType)
    {
        var definition = Assert.Single(DeviceInterfaceCatalog.For(deviceType));

        Assert.Equal("Ethernet0", definition.Name);
        Assert.Equal(InterfaceType.Ethernet, definition.InterfaceType);
    }

    [Fact]
    public void Factory_MaterialisesTheCatalogDefinitionsForEveryRegisteredType()
    {
        foreach (var descriptor in DeviceTypeRegistry.SupportedTypes)
        {
            var device = NetworkDeviceFactory.Create(descriptor.DeviceType, "D0");
            var expected = DeviceInterfaceCatalog.For(descriptor.DeviceType);

            Assert.Equal(expected.Select(d => d.Name), device.Interfaces.Select(i => i.Name));
            Assert.Equal(expected.Select(d => d.InterfaceType), device.Interfaces.Select(i => i.InterfaceType));
        }
    }

    [Fact]
    public void For_UnsupportedType_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DeviceInterfaceCatalog.For((DeviceType)999));
    }
}
