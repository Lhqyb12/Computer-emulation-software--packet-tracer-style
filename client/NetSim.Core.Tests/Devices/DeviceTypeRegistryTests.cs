using NetSim.Core.Devices;

namespace NetSim.Core.Tests.Devices;

public class DeviceTypeRegistryTests
{
    [Theory]
    [InlineData(DeviceType.Router)]
    [InlineData(DeviceType.Switch)]
    [InlineData(DeviceType.Pc)]
    [InlineData(DeviceType.Server)]
    [InlineData(DeviceType.Laptop)]
    public void SupportedTypes_IncludesEveryDeviceTypeTheFactoryCanCreate(DeviceType deviceType)
    {
        Assert.Contains(DeviceTypeRegistry.SupportedTypes, d => d.DeviceType == deviceType);

        // Every registered type must actually be constructible - a registry entry with no
        // matching factory case would be a UI-visible device the user could never place.
        var device = NetworkDeviceFactory.Create(deviceType, "Test");
        Assert.Equal(deviceType, device.DeviceType);
    }

    [Fact]
    public void Get_UnknownDeviceType_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DeviceTypeRegistry.Get((DeviceType)999));
    }

    [Fact]
    public void Get_ReturnsNonEmptyDisplayName()
    {
        foreach (var descriptor in DeviceTypeRegistry.SupportedTypes)
        {
            Assert.False(string.IsNullOrWhiteSpace(descriptor.DisplayName));
        }
    }
}
