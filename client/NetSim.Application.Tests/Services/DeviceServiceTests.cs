using NetSim.Application.Common;
using NetSim.Application.Services;
using NetSim.Application.State;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Application.Tests.Services;

public class DeviceServiceTests
{
    private static (DeviceService DeviceService, NetworkService NetworkService) CreateServices()
    {
        var state = new ApplicationState();
        return (new DeviceService(state), new NetworkService(state));
    }

    [Fact]
    public void AddRouter_WithoutActiveNetwork_ReturnsInvalidState()
    {
        var (deviceService, _) = CreateServices();

        var result = deviceService.AddRouter("R1");

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.InvalidState, result.ErrorType);
    }

    [Fact]
    public void AddRouter_WithActiveNetwork_AddsRouterToNetwork()
    {
        var (deviceService, networkService) = CreateServices();
        var network = networkService.CreateNetwork("Net");

        var result = deviceService.AddRouter("R1");

        Assert.True(result.IsSuccess);
        Assert.IsType<Router>(result.Value);
        Assert.Contains(result.Value, network.Devices);
    }

    [Theory]
    [InlineData(DeviceType.Router)]
    [InlineData(DeviceType.Switch)]
    [InlineData(DeviceType.Pc)]
    [InlineData(DeviceType.Server)]
    [InlineData(DeviceType.Laptop)]
    public void AddDevice_EachSupportedType_AddsToNetwork(DeviceType deviceType)
    {
        var (deviceService, networkService) = CreateServices();
        var network = networkService.CreateNetwork("Net");

        NetworkDevice device = deviceType switch
        {
            DeviceType.Router => deviceService.AddRouter("D1").Value!,
            DeviceType.Switch => deviceService.AddSwitch("D1").Value!,
            DeviceType.Pc => deviceService.AddPc("D1").Value!,
            DeviceType.Server => deviceService.AddServer("D1").Value!,
            DeviceType.Laptop => deviceService.AddLaptop("D1").Value!,
            _ => throw new ArgumentOutOfRangeException(nameof(deviceType)),
        };

        Assert.Equal(deviceType, device.DeviceType);
        Assert.Contains(device, network.Devices);
    }

    [Fact]
    public void AddRouter_DuplicateName_IsAllowed_BecauseCoreDoesNotForbidIt()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");

        var first = deviceService.AddRouter("R1");
        var second = deviceService.AddRouter("R1");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value!.Id, second.Value!.Id);
    }

    [Fact]
    public void AddRouter_BlankName_ThrowsDomainException()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");

        Assert.Throws<DomainException>(() => deviceService.AddRouter(" "));
    }

    [Fact]
    public void AddRouter_ReceivesDefaultInterfacesFromFactory()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");

        var router = deviceService.AddRouter("R1").Value!;

        Assert.Equal(3, router.Interfaces.Count);
    }

    [Fact]
    public void AddPc_ReceivesExactlyOneDefaultInterface()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");

        var pc = deviceService.AddPc("PC1").Value!;

        Assert.Single(pc.Interfaces);
    }

    [Fact]
    public void AddLaptop_WithActiveNetwork_AddsLaptopToNetwork()
    {
        var (deviceService, networkService) = CreateServices();
        var network = networkService.CreateNetwork("Net");

        var result = deviceService.AddLaptop("L1");

        Assert.True(result.IsSuccess);
        Assert.IsType<Laptop>(result.Value);
        Assert.Contains(result.Value, network.Devices);
    }

    [Fact]
    public void AddDevice_WithoutActiveNetwork_ReturnsInvalidState()
    {
        var (deviceService, _) = CreateServices();

        var result = deviceService.AddDevice(DeviceType.Router);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.InvalidState, result.ErrorType);
    }

    [Fact]
    public void AddDevice_WithExplicitName_UsesThatName()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");

        var result = deviceService.AddDevice(DeviceType.Switch, "Core-Switch");

        Assert.True(result.IsSuccess);
        Assert.Equal("Core-Switch", result.Value!.Name);
    }

    [Fact]
    public void AddDevice_WithoutName_GeneratesSequentialDefaultNames()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");

        var first = deviceService.AddDevice(DeviceType.Pc);
        var second = deviceService.AddDevice(DeviceType.Pc);

        Assert.Equal("PC0", first.Value!.Name);
        Assert.Equal("PC1", second.Value!.Name);
    }

    [Fact]
    public void AddDevice_WithoutName_CountsOnlyDevicesOfTheSameType()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        deviceService.AddDevice(DeviceType.Router);

        var firstSwitch = deviceService.AddDevice(DeviceType.Switch);

        Assert.Equal("Switch0", firstSwitch.Value!.Name);
    }

    [Fact]
    public void AddDevice_WithBlankName_ThrowsDomainException()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");

        Assert.Throws<DomainException>(() => deviceService.AddDevice(DeviceType.Router, "   "));
    }

    [Fact]
    public void RemoveDevice_ThatExists_Succeeds()
    {
        var (deviceService, networkService) = CreateServices();
        var network = networkService.CreateNetwork("Net");
        var router = deviceService.AddRouter("R1").Value!;

        var result = deviceService.RemoveDevice(router);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(router, network.Devices);
    }

    [Fact]
    public void RemoveDevice_NotInCurrentNetwork_ReturnsNotFound()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var foreignRouter = new Router("Foreign");

        var result = deviceService.RemoveDevice(foreignRouter);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public void RemoveDevice_WithoutActiveNetwork_ReturnsInvalidState()
    {
        var (deviceService, _) = CreateServices();

        var result = deviceService.RemoveDevice(new Router("R1"));

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.InvalidState, result.ErrorType);
    }

    [Fact]
    public void RenameDevice_ThatExists_Succeeds()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var router = deviceService.AddRouter("R1").Value!;

        var result = deviceService.RenameDevice(router, "Core-Router");

        Assert.True(result.IsSuccess);
        Assert.Equal("Core-Router", router.Name);
    }

    [Fact]
    public void RenameDevice_NotInCurrentNetwork_ReturnsNotFound()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var foreignRouter = new Router("Foreign");

        var result = deviceService.RenameDevice(foreignRouter, "New Name");

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public void AddInterface_OnDeviceInCurrentNetwork_Succeeds()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var router = deviceService.AddRouter("R1").Value!;

        var result = deviceService.AddInterface(router, "G0/0", InterfaceType.GigabitEthernet);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value, router.Interfaces);
    }

    [Fact]
    public void AddInterface_DuplicateNameOnSameDevice_ThrowsDomainException()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var router = deviceService.AddRouter("R1").Value!;
        deviceService.AddInterface(router, "G0/0", InterfaceType.GigabitEthernet);

        Assert.Throws<DomainException>(() => deviceService.AddInterface(router, "G0/0", InterfaceType.GigabitEthernet));
    }

    [Fact]
    public void AddInterface_DeviceNotInCurrentNetwork_ReturnsNotFound()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var foreignRouter = new Router("Foreign");

        var result = deviceService.AddInterface(foreignRouter, "G0/0", InterfaceType.GigabitEthernet);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.NotFound, result.ErrorType);
    }

    // ----- SetInterfaceAdministrativeState (Phase 14) -----

    [Fact]
    public void SetInterfaceAdministrativeState_Disable_DisablesInterface_RaisesEvent_AndMarksDirty()
    {
        var state = new ApplicationState();
        var deviceService = new DeviceService(state);
        var networkService = new NetworkService(state);
        state.SetCurrentProject(new Application.Projects.Project(
            Core.Common.EntityId.New(), "P", null, System.DateTime.UtcNow, System.DateTime.UtcNow, null, 1));
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();
        var raised = 0;
        deviceService.InterfacesChanged += (_, _) => raised++;

        var result = deviceService.SetInterfaceAdministrativeState(iface, InterfaceAdministrativeState.Disabled);

        Assert.True(result.IsSuccess);
        Assert.Equal(InterfaceAdministrativeState.Disabled, iface.AdministrativeState);
        Assert.False(iface.IsEnabled);
        Assert.Equal(1, raised);
        Assert.True(state.IsCurrentProjectDirty);
    }

    [Fact]
    public void SetInterfaceAdministrativeState_NoChange_IsSuccessButRaisesNothing()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();
        var raised = 0;
        deviceService.InterfacesChanged += (_, _) => raised++;

        var result = deviceService.SetInterfaceAdministrativeState(iface, InterfaceAdministrativeState.Enabled);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void SetInterfaceAdministrativeState_WithoutActiveNetwork_ReturnsInvalidState()
    {
        var (deviceService, _) = CreateServices();
        var iface = new Router("R1").AddInterface("G0/0", InterfaceType.GigabitEthernet);

        var result = deviceService.SetInterfaceAdministrativeState(iface, InterfaceAdministrativeState.Disabled);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.InvalidState, result.ErrorType);
    }

    [Fact]
    public void SetInterfaceAdministrativeState_InterfaceNotInCurrentNetwork_ReturnsNotFound()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var iface = new Router("Foreign").AddInterface("G0/0", InterfaceType.GigabitEthernet);

        var result = deviceService.SetInterfaceAdministrativeState(iface, InterfaceAdministrativeState.Disabled);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.NotFound, result.ErrorType);
    }

    // ----- SetInterfaceIPv4Configuration / ClearInterfaceIPv4Configuration (Phase 18) -----

    [Fact]
    public void SetInterfaceIPv4Configuration_ValidAddress_SetsPrimary_RaisesEvent_AndMarksDirty()
    {
        var state = new ApplicationState();
        var deviceService = new DeviceService(state);
        var networkService = new NetworkService(state);
        state.SetCurrentProject(new Application.Projects.Project(
            Core.Common.EntityId.New(), "P", null, System.DateTime.UtcNow, System.DateTime.UtcNow, null, 1));
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();
        var raised = 0;
        deviceService.InterfacesChanged += (_, _) => raised++;

        var result = deviceService.SetInterfaceIPv4Configuration(iface, IPv4Address.Parse("192.168.1.10"), 24);

        Assert.True(result.IsSuccess);
        Assert.Equal("192.168.1.10/24", iface.PrimaryIPv4Configuration!.Cidr);
        Assert.Equal(1, raised);
        Assert.True(state.IsCurrentProjectDirty);
    }

    [Fact]
    public void SetInterfaceIPv4Configuration_ReplacesAnyExistingConfiguration()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();
        deviceService.SetInterfaceIPv4Configuration(iface, IPv4Address.Parse("10.0.0.1"), 8);

        deviceService.SetInterfaceIPv4Configuration(iface, IPv4Address.Parse("192.168.1.10"), 24);

        var only = Assert.Single(iface.IPv4Configurations);
        Assert.Equal(IPv4Address.Parse("192.168.1.10"), only.Address);
    }

    [Theory]
    [InlineData("0.0.0.0", 24)]
    [InlineData("224.0.0.1", 24)]
    [InlineData("192.168.1.10", 33)]
    public void SetInterfaceIPv4Configuration_InvalidValue_ReturnsValidationError_WithoutThrowing(string address, int prefix)
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();

        var result = deviceService.SetInterfaceIPv4Configuration(iface, IPv4Address.Parse(address), prefix);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.ValidationError, result.ErrorType);
        Assert.False(iface.HasIPv4Configuration);
    }

    [Fact]
    public void SetInterfaceIPv4Configuration_WithoutActiveNetwork_ReturnsInvalidState()
    {
        var (deviceService, _) = CreateServices();
        var iface = new Router("R1").AddInterface("G0/0", InterfaceType.GigabitEthernet);

        var result = deviceService.SetInterfaceIPv4Configuration(iface, IPv4Address.Parse("192.168.1.10"), 24);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.InvalidState, result.ErrorType);
    }

    [Fact]
    public void ClearInterfaceIPv4Configuration_RemovesAddresses_RaisesEvent_AndMarksDirty()
    {
        var state = new ApplicationState();
        var deviceService = new DeviceService(state);
        var networkService = new NetworkService(state);
        state.SetCurrentProject(new Application.Projects.Project(
            Core.Common.EntityId.New(), "P", null, System.DateTime.UtcNow, System.DateTime.UtcNow, null, 1));
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();
        deviceService.SetInterfaceIPv4Configuration(iface, IPv4Address.Parse("192.168.1.10"), 24);
        state.MarkCurrentProjectClean();
        var raised = 0;
        deviceService.InterfacesChanged += (_, _) => raised++;

        var result = deviceService.ClearInterfaceIPv4Configuration(iface);

        Assert.True(result.IsSuccess);
        Assert.False(iface.HasIPv4Configuration);
        Assert.Equal(1, raised);
        Assert.True(state.IsCurrentProjectDirty);
    }

    [Fact]
    public void ClearInterfaceIPv4Configuration_WhenNothingConfigured_IsANoOpSuccess()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();
        var raised = 0;
        deviceService.InterfacesChanged += (_, _) => raised++;

        var result = deviceService.ClearInterfaceIPv4Configuration(iface);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, raised);
    }

    // ----- AddInterfaceIPv6Configuration / ClearInterfaceIPv6Configuration (Phase 19) -----

    [Fact]
    public void AddInterfaceIPv6Configuration_ValidAddress_Adds_RaisesEvent_AndMarksDirty()
    {
        var state = new ApplicationState();
        var deviceService = new DeviceService(state);
        var networkService = new NetworkService(state);
        state.SetCurrentProject(new Application.Projects.Project(
            Core.Common.EntityId.New(), "P", null, System.DateTime.UtcNow, System.DateTime.UtcNow, null, 1));
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();
        var raised = 0;
        deviceService.InterfacesChanged += (_, _) => raised++;

        var result = deviceService.AddInterfaceIPv6Configuration(iface, IPv6Address.Parse("2001:db8:1::1"), 64);

        Assert.True(result.IsSuccess);
        Assert.Equal("2001:db8:1::1/64", iface.PrimaryIPv6Configuration!.Cidr);
        Assert.Equal(1, raised);
        Assert.True(state.IsCurrentProjectDirty);
    }

    [Fact]
    public void AddInterfaceIPv6Configuration_KeepsAnyAddressesAlreadyConfigured()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();

        deviceService.AddInterfaceIPv6Configuration(iface, IPv6Address.Parse("fe80::1"), 64);
        deviceService.AddInterfaceIPv6Configuration(iface, IPv6Address.Parse("2001:db8:1::1"), 64);

        Assert.Equal(2, iface.IPv6Configurations.Count);
        Assert.Equal(IPv6Address.Parse("fe80::1"), iface.IPv6Address);
    }

    [Theory]
    [InlineData("::", 64)]
    [InlineData("::1", 64)]
    [InlineData("ff02::1", 64)]
    [InlineData("2001:db8::1", 129)]
    public void AddInterfaceIPv6Configuration_InvalidValue_ReturnsValidationError_WithoutThrowing(string address, int prefix)
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();

        var result = deviceService.AddInterfaceIPv6Configuration(iface, IPv6Address.Parse(address), prefix);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.ValidationError, result.ErrorType);
        Assert.False(iface.HasIPv6Configuration);
    }

    [Fact]
    public void AddInterfaceIPv6Configuration_DuplicateAddress_ReturnsValidationError()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();
        deviceService.AddInterfaceIPv6Configuration(iface, IPv6Address.Parse("2001:db8:1::1"), 64);

        var result = deviceService.AddInterfaceIPv6Configuration(iface, IPv6Address.Parse("2001:0db8:1::1"), 48);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.ValidationError, result.ErrorType);
        Assert.Single(iface.IPv6Configurations);
    }

    [Fact]
    public void AddInterfaceIPv6Configuration_WithoutActiveNetwork_ReturnsInvalidState()
    {
        var (deviceService, _) = CreateServices();
        var iface = new Router("R1").AddInterface("G0/0", InterfaceType.GigabitEthernet);

        var result = deviceService.AddInterfaceIPv6Configuration(iface, IPv6Address.Parse("2001:db8:1::1"), 64);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.InvalidState, result.ErrorType);
    }

    [Fact]
    public void ClearInterfaceIPv6Configuration_RemovesAddresses_RaisesEvent_AndMarksDirty()
    {
        var state = new ApplicationState();
        var deviceService = new DeviceService(state);
        var networkService = new NetworkService(state);
        state.SetCurrentProject(new Application.Projects.Project(
            Core.Common.EntityId.New(), "P", null, System.DateTime.UtcNow, System.DateTime.UtcNow, null, 1));
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();
        deviceService.AddInterfaceIPv6Configuration(iface, IPv6Address.Parse("2001:db8:1::1"), 64);
        state.MarkCurrentProjectClean();
        var raised = 0;
        deviceService.InterfacesChanged += (_, _) => raised++;

        var result = deviceService.ClearInterfaceIPv6Configuration(iface);

        Assert.True(result.IsSuccess);
        Assert.False(iface.HasIPv6Configuration);
        Assert.Equal(1, raised);
        Assert.True(state.IsCurrentProjectDirty);
    }

    [Fact]
    public void ClearInterfaceIPv6Configuration_WhenNothingConfigured_IsANoOpSuccess()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();
        var raised = 0;
        deviceService.InterfacesChanged += (_, _) => raised++;

        var result = deviceService.ClearInterfaceIPv6Configuration(iface);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void IPv4AndIPv6_CoexistOnTheSameInterface_ThroughTheService()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();

        deviceService.SetInterfaceIPv4Configuration(iface, IPv4Address.Parse("192.168.1.10"), 24);
        deviceService.AddInterfaceIPv6Configuration(iface, IPv6Address.Parse("2001:db8:1::10"), 64);

        Assert.Equal("192.168.1.10/24", iface.PrimaryIPv4Configuration!.Cidr);
        Assert.Equal("2001:db8:1::10/64", iface.PrimaryIPv6Configuration!.Cidr);
    }

    // ----- ClearInterfaceArpCache (Phase 20) -----

    [Fact]
    public void ClearInterfaceArpCache_WithoutActiveNetwork_ReturnsInvalidState()
    {
        var (deviceService, _) = CreateServices();
        var pc = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");

        var result = deviceService.ClearInterfaceArpCache(pc.Interfaces.First());

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.InvalidState, result.ErrorType);
    }

    [Fact]
    public void ClearInterfaceArpCache_InterfaceNotInCurrentNetwork_ReturnsNotFound()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var stray = NetworkDeviceFactory.Create(DeviceType.Pc, "Stray");

        var result = deviceService.ClearInterfaceArpCache(stray.Interfaces.First());

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public void ClearInterfaceArpCache_EmptiesTheCache_RaisesEvent_AndDoesNotMarkDirty()
    {
        var state = new ApplicationState();
        var deviceService = new DeviceService(state);
        var networkService = new NetworkService(state);
        state.SetCurrentProject(new Application.Projects.Project(
            Core.Common.EntityId.New(), "P", null, System.DateTime.UtcNow, System.DateTime.UtcNow, null, 1));
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;
        var iface = pc.Interfaces.First();
        iface.ArpCache.AddOrUpdateDynamic(IPv4Address.Parse("192.168.1.20"), MacAddress.Parse("AA:BB:CC:DD:EE:FF"));
        state.MarkCurrentProjectClean();
        var raised = 0;
        deviceService.InterfacesChanged += (_, _) => raised++;

        var result = deviceService.ClearInterfaceArpCache(iface);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, iface.ArpCache.Count);
        Assert.Equal(1, raised);
        Assert.False(state.IsCurrentProjectDirty); // ARP cache is transient runtime state
    }

    [Fact]
    public void ClearInterfaceArpCache_WhenEmpty_IsStillASuccess()
    {
        var (deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var pc = deviceService.AddPc("PC0").Value!;

        var result = deviceService.ClearInterfaceArpCache(pc.Interfaces.First());

        Assert.True(result.IsSuccess);
    }
}
