using NetSim.Application.Common;
using NetSim.Application.Services;
using NetSim.Application.State;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Application.Tests.Services;

public class ConnectionServiceTests
{
    private static (ConnectionService ConnectionService, DeviceService DeviceService, NetworkService NetworkService) CreateServices()
    {
        var state = new ApplicationState();
        return (new ConnectionService(state), new DeviceService(state), new NetworkService(state));
    }

    [Fact]
    public void Connect_WithoutActiveNetwork_ReturnsInvalidState()
    {
        var (connectionService, _, _) = CreateServices();
        var r1 = new Router("R1");
        var r2 = new Router("R2");
        var ifaceA = r1.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var ifaceB = r2.AddInterface("G0/0", InterfaceType.GigabitEthernet);

        var result = connectionService.Connect(ifaceA, ifaceB);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.InvalidState, result.ErrorType);
    }

    [Fact]
    public void Connect_TwoInterfacesInCurrentNetwork_Succeeds()
    {
        var (connectionService, deviceService, networkService) = CreateServices();
        var network = networkService.CreateNetwork("Net");
        var r1 = deviceService.AddRouter("R1").Value!;
        var r2 = deviceService.AddRouter("R2").Value!;
        var ifaceA = deviceService.AddInterface(r1, "G0/0", InterfaceType.GigabitEthernet).Value!;
        var ifaceB = deviceService.AddInterface(r2, "G0/0", InterfaceType.GigabitEthernet).Value!;

        var result = connectionService.Connect(ifaceA, ifaceB);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value, network.Connections);
        Assert.True(ifaceA.IsConnected);
        Assert.True(ifaceB.IsConnected);
    }

    [Fact]
    public void Connect_MarksTheCurrentProjectDirty()
    {
        var state = new ApplicationState();
        var connectionService = new ConnectionService(state);
        var deviceService = new DeviceService(state);
        var networkService = new NetworkService(state);
        state.SetCurrentProject(new Application.Projects.Project(
            Core.Common.EntityId.New(), "P", null, System.DateTime.UtcNow, System.DateTime.UtcNow, null, 1));
        networkService.CreateNetwork("Net");
        var r1 = deviceService.AddRouter("R1").Value!;
        var r2 = deviceService.AddRouter("R2").Value!;
        var ifaceA = deviceService.AddInterface(r1, "G0/0", InterfaceType.GigabitEthernet).Value!;
        var ifaceB = deviceService.AddInterface(r2, "G0/0", InterfaceType.GigabitEthernet).Value!;
        Assert.False(state.IsCurrentProjectDirty);

        connectionService.Connect(ifaceA, ifaceB);

        Assert.True(state.IsCurrentProjectDirty);
    }

    [Fact]
    public void Disconnect_MarksTheCurrentProjectDirty()
    {
        var state = new ApplicationState();
        var connectionService = new ConnectionService(state);
        var deviceService = new DeviceService(state);
        var networkService = new NetworkService(state);
        state.SetCurrentProject(new Application.Projects.Project(
            Core.Common.EntityId.New(), "P", null, System.DateTime.UtcNow, System.DateTime.UtcNow, null, 1));
        networkService.CreateNetwork("Net");
        var r1 = deviceService.AddRouter("R1").Value!;
        var r2 = deviceService.AddRouter("R2").Value!;
        var ifaceA = deviceService.AddInterface(r1, "G0/0", InterfaceType.GigabitEthernet).Value!;
        var ifaceB = deviceService.AddInterface(r2, "G0/0", InterfaceType.GigabitEthernet).Value!;
        var connection = connectionService.Connect(ifaceA, ifaceB).Value!;
        state.MarkCurrentProjectClean();

        connectionService.Disconnect(connection);

        Assert.True(state.IsCurrentProjectDirty);
    }

    [Fact]
    public void Connect_ToAnAdministrativelyDisabledInterface_ReturnsInvalidState()
    {
        var (connectionService, deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var r1 = deviceService.AddRouter("R1").Value!;
        var r2 = deviceService.AddRouter("R2").Value!;
        var ifaceA = deviceService.AddInterface(r1, "G0/0", InterfaceType.GigabitEthernet).Value!;
        var ifaceB = deviceService.AddInterface(r2, "G0/0", InterfaceType.GigabitEthernet).Value!;
        ifaceB.Disable();

        var result = connectionService.Connect(ifaceA, ifaceB);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.InvalidState, result.ErrorType);
        Assert.Contains("administratively disabled", result.ErrorMessage);
        Assert.False(ifaceA.IsConnected);
    }

    [Fact]
    public void Connect_SameInterfaceToItself_ThrowsDomainException()
    {
        var (connectionService, deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var router = deviceService.AddRouter("R1").Value!;
        var iface = deviceService.AddInterface(router, "G0/0", InterfaceType.GigabitEthernet).Value!;

        Assert.Throws<DomainException>(() => connectionService.Connect(iface, iface));
    }

    [Fact]
    public void Connect_InterfaceAlreadyConnected_ThrowsDomainException()
    {
        var (connectionService, deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var r1 = deviceService.AddRouter("R1").Value!;
        var r2 = deviceService.AddRouter("R2").Value!;
        var r3 = deviceService.AddRouter("R3").Value!;
        var ifaceA = deviceService.AddInterface(r1, "G0/0", InterfaceType.GigabitEthernet).Value!;
        var ifaceB = deviceService.AddInterface(r2, "G0/0", InterfaceType.GigabitEthernet).Value!;
        var ifaceC = deviceService.AddInterface(r3, "G0/0", InterfaceType.GigabitEthernet).Value!;
        connectionService.Connect(ifaceA, ifaceB);

        Assert.Throws<DomainException>(() => connectionService.Connect(ifaceA, ifaceC));
    }

    [Fact]
    public void Connect_DeviceNotPartOfCurrentNetwork_ThrowsDomainException()
    {
        var (connectionService, deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");
        var r1 = deviceService.AddRouter("R1").Value!;
        var ifaceA = deviceService.AddInterface(r1, "G0/0", InterfaceType.GigabitEthernet).Value!;

        var foreignRouter = new Router("Foreign");
        var ifaceB = foreignRouter.AddInterface("G0/0", InterfaceType.GigabitEthernet);

        Assert.Throws<DomainException>(() => connectionService.Connect(ifaceA, ifaceB));
    }

    [Fact]
    public void Disconnect_ExistingConnection_Succeeds()
    {
        var (connectionService, deviceService, networkService) = CreateServices();
        var network = networkService.CreateNetwork("Net");
        var r1 = deviceService.AddRouter("R1").Value!;
        var r2 = deviceService.AddRouter("R2").Value!;
        var ifaceA = deviceService.AddInterface(r1, "G0/0", InterfaceType.GigabitEthernet).Value!;
        var ifaceB = deviceService.AddInterface(r2, "G0/0", InterfaceType.GigabitEthernet).Value!;
        var connection = connectionService.Connect(ifaceA, ifaceB).Value!;

        var result = connectionService.Disconnect(connection);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(connection, network.Connections);
        Assert.False(ifaceA.IsConnected);
    }

    [Fact]
    public void Disconnect_ConnectionNotInCurrentNetwork_ReturnsNotFound()
    {
        var (connectionService, deviceService, networkService) = CreateServices();
        networkService.CreateNetwork("Net");

        var r1 = new Router("R1");
        var r2 = new Router("R2");
        var ifaceA = r1.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var ifaceB = r2.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var foreignConnection = Connection.Create(ifaceA, ifaceB);

        var result = connectionService.Disconnect(foreignConnection);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public void Disconnect_WithoutActiveNetwork_ReturnsInvalidState()
    {
        var (connectionService, _, _) = CreateServices();
        var r1 = new Router("R1");
        var r2 = new Router("R2");
        var ifaceA = r1.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var ifaceB = r2.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var connection = Connection.Create(ifaceA, ifaceB);

        var result = connectionService.Disconnect(connection);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.InvalidState, result.ErrorType);
    }
}
