using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Devices;

public class NetworkDeviceTests
{
    [Fact]
    public void Router_IsANetworkDevice()
    {
        var router = new Router("Router1");

        Assert.IsAssignableFrom<NetworkDevice>(router);
        Assert.Equal(DeviceType.Router, router.DeviceType);
    }

    [Fact]
    public void Switch_IsANetworkDevice()
    {
        var sw = new Switch("Switch1");

        Assert.IsAssignableFrom<NetworkDevice>(sw);
        Assert.Equal(DeviceType.Switch, sw.DeviceType);
    }

    [Fact]
    public void Pc_IsAnEndDeviceAndANetworkDevice()
    {
        var pc = new Pc("PC1");

        Assert.IsAssignableFrom<EndDevice>(pc);
        Assert.IsAssignableFrom<NetworkDevice>(pc);
    }

    [Fact]
    public void Server_IsAnEndDeviceAndANetworkDevice()
    {
        var server = new Server("Server1");

        Assert.IsAssignableFrom<EndDevice>(server);
        Assert.IsAssignableFrom<NetworkDevice>(server);
    }

    [Fact]
    public void Laptop_IsAnEndDeviceAndANetworkDevice()
    {
        var laptop = new Laptop("Laptop1");

        Assert.IsAssignableFrom<EndDevice>(laptop);
        Assert.IsAssignableFrom<NetworkDevice>(laptop);
        Assert.Equal(DeviceType.Laptop, laptop.DeviceType);
    }

    [Fact]
    public void NewDevice_HasUniqueIdAndIsPoweredOff()
    {
        var device = new Router("Router1");

        Assert.NotEqual(default, device.Id);
        Assert.Equal(DeviceOperationalState.PoweredOff, device.OperationalState);
    }

    [Fact]
    public void TwoDevices_HaveDifferentIdsEvenWithSameName()
    {
        var first = new Router("Router1");
        var second = new Router("Router1");

        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_RejectsInvalidName(string? name)
    {
        Assert.Throws<DomainException>(() => new Router(name!));
    }

    [Fact]
    public void Rename_ChangesNameButNotIdentity()
    {
        var router = new Router("Router1");
        var originalId = router.Id;

        router.Rename("Core-Router");

        Assert.Equal("Core-Router", router.Name);
        Assert.Equal(originalId, router.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rename_RejectsInvalidName(string? name)
    {
        var router = new Router("Router1");

        Assert.Throws<DomainException>(() => router.Rename(name!));
    }

    [Fact]
    public void PowerOnAndPowerOff_ChangeOperationalState()
    {
        var device = new Router("Router1");

        device.PowerOn();
        Assert.Equal(DeviceOperationalState.PoweredOn, device.OperationalState);

        device.PowerOff();
        Assert.Equal(DeviceOperationalState.PoweredOff, device.OperationalState);
    }

    [Fact]
    public void AddInterface_AddsToDeviceInterfaceCollection()
    {
        var router = new Router("Router1");

        var iface = router.AddInterface("G0/0", InterfaceType.GigabitEthernet);

        Assert.Contains(iface, router.Interfaces);
        Assert.Same(router, iface.Device);
    }

    [Fact]
    public void AddInterface_RejectsDuplicateNameOnSameDevice()
    {
        var router = new Router("Router1");
        router.AddInterface("G0/0", InterfaceType.GigabitEthernet);

        Assert.Throws<DomainException>(() => router.AddInterface("G0/0", InterfaceType.GigabitEthernet));
    }

    [Fact]
    public void RemoveInterface_RemovesUnconnectedInterface()
    {
        var router = new Router("Router1");
        var iface = router.AddInterface("G0/0", InterfaceType.GigabitEthernet);

        var removed = router.RemoveInterface(iface);

        Assert.True(removed);
        Assert.DoesNotContain(iface, router.Interfaces);
    }

    [Fact]
    public void RemoveInterface_ThrowsWhenInterfaceIsConnected()
    {
        var router1 = new Router("Router1");
        var router2 = new Router("Router2");
        var ifaceA = router1.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var ifaceB = router2.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        Connection.Create(ifaceA, ifaceB);

        Assert.Throws<DomainException>(() => router1.RemoveInterface(ifaceA));
    }

    [Fact]
    public void AddInterface_RaisesInterfaceAdded_WithTheNewInterface()
    {
        var router = new Router("Router1");
        NetworkInterface? raised = null;
        router.InterfaceAdded += (_, iface) => raised = iface;

        var added = router.AddInterface("G0/0", InterfaceType.GigabitEthernet);

        Assert.Same(added, raised);
    }

    [Fact]
    public void RemoveInterface_RaisesInterfaceRemoved_OnlyWhenSomethingWasRemoved()
    {
        var router = new Router("Router1");
        var iface = router.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var raisedCount = 0;
        router.InterfaceRemoved += (_, _) => raisedCount++;

        router.RemoveInterface(iface);
        router.RemoveInterface(iface); // already gone - no event

        Assert.Equal(1, raisedCount);
    }
}
