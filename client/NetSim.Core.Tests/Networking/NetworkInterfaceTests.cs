using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

public class NetworkInterfaceTests
{
    private static NetworkInterface NewInterface(InterfaceType type = InterfaceType.GigabitEthernet, string name = "GigabitEthernet0/0")
        => new Router("R1").AddInterface(name, type);

    [Fact]
    public void NewInterface_HasTypeDefaultsAndIsEnabledDown()
    {
        var iface = NewInterface(InterfaceType.FastEthernet, "FastEthernet0/1");

        Assert.Equal(InterfaceType.FastEthernet, iface.InterfaceType);
        Assert.Equal(InterfaceSpeed.Mbps100, iface.Speed);
        Assert.Equal(InterfaceCapability.Ethernet, iface.Capabilities);
        Assert.Equal(InterfaceAdministrativeState.Enabled, iface.AdministrativeState);
        Assert.Equal(InterfaceOperationalState.Down, iface.OperationalState);
        Assert.True(iface.IsEnabled);
        Assert.False(iface.IsConnected);
        Assert.True(iface.IsAvailable);
        Assert.False(iface.IsOperational);
    }

    [Theory]
    [InlineData(InterfaceType.Ethernet, 10_000_000L)]
    [InlineData(InterfaceType.FastEthernet, 100_000_000L)]
    [InlineData(InterfaceType.GigabitEthernet, 1_000_000_000L)]
    public void Speed_DefaultsFromInterfaceType(InterfaceType type, long expectedBps)
    {
        Assert.Equal(expectedBps, NewInterface(type, type + "0").Speed.BitsPerSecond);
    }

    [Fact]
    public void ShortName_UsesCompactPrefix()
    {
        Assert.Equal("G0/0", NewInterface(InterfaceType.GigabitEthernet, "GigabitEthernet0/0").ShortName);
        Assert.Equal("Se0/0/0", NewInterface(InterfaceType.Serial, "Serial0/0/0").ShortName);
    }

    [Fact]
    public void Disable_ForcesOperationallyDown_AndBlocksBringUp()
    {
        var iface = NewInterface();
        iface.BringUp();
        Assert.Equal(InterfaceOperationalState.Up, iface.OperationalState);

        iface.Disable();

        Assert.Equal(InterfaceAdministrativeState.Disabled, iface.AdministrativeState);
        Assert.Equal(InterfaceOperationalState.Down, iface.OperationalState);
        Assert.False(iface.IsEnabled);
        Assert.False(iface.IsAvailable);
        Assert.Throws<DomainException>(() => iface.BringUp());
    }

    [Fact]
    public void Enable_AfterDisable_RestoresAvailabilityButNotLink()
    {
        var iface = NewInterface();
        iface.Disable();

        iface.Enable();

        Assert.True(iface.IsEnabled);
        Assert.True(iface.IsAvailable);
        Assert.Equal(InterfaceOperationalState.Down, iface.OperationalState);
        iface.BringUp();
        Assert.True(iface.IsOperational);
    }

    [Fact]
    public void IsAvailable_IsFalseWhenConnected()
    {
        var r1 = new Router("R1");
        var r2 = new Router("R2");
        var a = r1.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var b = r2.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        Connection.Create(a, b);

        Assert.True(a.IsConnected);
        Assert.False(a.IsAvailable);
    }

    [Fact]
    public void SetDescription_TrimsAndNormalisesBlankToNull()
    {
        var iface = NewInterface();

        iface.SetDescription("  Uplink to core  ");
        Assert.Equal("Uplink to core", iface.Description);
        Assert.Equal("Uplink to core", iface.DisplayName);

        iface.SetDescription("   ");
        Assert.Null(iface.Description);
        Assert.Equal(iface.Name, iface.DisplayName);
    }

    [Fact]
    public void SetSpeed_OverridesTheDefault()
    {
        var iface = NewInterface();

        iface.SetSpeed(InterfaceSpeed.Gbps10);

        Assert.Equal(InterfaceSpeed.Gbps10, iface.Speed);
    }

    [Fact]
    public void Ownership_IsSingleDeviceAndImmutable()
    {
        var router = new Router("R1");
        var iface = router.AddInterface("G0/0", InterfaceType.GigabitEthernet);

        Assert.Same(router, iface.Device);
        Assert.Contains(iface, router.Interfaces);
    }
}
