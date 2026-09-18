using System.Linq;
using NetSim.Application.Canvas;
using NetSim.Application.Services;
using NetSim.Application.State;
using NetSim.Core.Devices;

namespace NetSim.Application.Tests.Canvas;

public class ConnectionToolControllerTests
{
    private const double HitRadius = 12.0;
    private static readonly CanvasSize DeviceSize = new(96, 72);

    private static (ConnectionToolController Tool, ApplicationState State, CanvasItemsState Items, DeviceService Devices) Create()
    {
        var state = new ApplicationState();
        var items = new CanvasItemsState(state);
        var networkService = new NetworkService(state);
        var devices = new DeviceService(state);
        var connections = new ConnectionService(state);
        networkService.CreateNetwork("Net");
        var tool = new ConnectionToolController(state, items, connections);
        return (tool, state, items, devices);
    }

    private static NetworkDevice Place(DeviceService devices, CanvasItemsState items, DeviceType type, CanvasPoint position)
    {
        var device = devices.AddDevice(type).Value!;
        items.AddItem(new CanvasItem(device.Id, device.Name, position, DeviceSize, type));
        return device;
    }

    private static CanvasPoint AnchorOf(NetworkDevice device, CanvasPoint devicePosition, int interfaceIndex)
    {
        var count = device.Interfaces.Count;
        return InterfaceAnchors.ForInterface(devicePosition, DeviceSize, interfaceIndex, count);
    }

    [Fact]
    public void InterfaceEndpoints_WithNoNetwork_IsEmpty()
    {
        var tool = new ConnectionToolController(new ApplicationState(), new CanvasItemsState(new ApplicationState()), new ConnectionService(new ApplicationState()));

        Assert.Empty(tool.InterfaceEndpoints());
    }

    [Fact]
    public void InterfaceEndpoints_HasOneEntryPerDeviceInterface()
    {
        var (tool, _, items, devices) = Create();
        Place(devices, items, DeviceType.Pc, new CanvasPoint(0, 0));       // 1 interface
        Place(devices, items, DeviceType.Router, new CanvasPoint(300, 0)); // 3 interfaces

        Assert.Equal(4, tool.InterfaceEndpoints().Count);
    }

    [Fact]
    public void OnClick_FirstInterfacePick_BecomesPendingEndpoint()
    {
        var (tool, _, items, devices) = Create();
        var pcPosition = new CanvasPoint(0, 0);
        var pc = Place(devices, items, DeviceType.Pc, pcPosition);

        tool.OnClick(AnchorOf(pc, pcPosition, 0), HitRadius);

        Assert.True(tool.HasPendingEndpoint);
        Assert.Equal(pc.Id, tool.PendingEndpoint!.DeviceId);
    }

    [Fact]
    public void OnClick_SecondInterfacePick_CreatesConnectionBetweenTheTwoInterfaces()
    {
        var (tool, state, items, devices) = Create();
        var aPos = new CanvasPoint(0, 0);
        var bPos = new CanvasPoint(300, 0);
        var pcA = Place(devices, items, DeviceType.Pc, aPos);
        var pcB = Place(devices, items, DeviceType.Pc, bPos);

        tool.OnClick(AnchorOf(pcA, aPos, 0), HitRadius);
        tool.OnClick(AnchorOf(pcB, bPos, 0), HitRadius);

        var connection = Assert.Single(state.CurrentNetwork!.Connections);
        Assert.False(tool.HasPendingEndpoint);
        Assert.Equal(connection.Id, tool.LastCreatedConnectionId);
        Assert.True(pcA.Interfaces.Single().IsConnected);
        Assert.True(pcB.Interfaces.Single().IsConnected);
    }

    [Fact]
    public void OnClick_OnEmptyCanvas_CancelsAnInProgressCable()
    {
        var (tool, _, items, devices) = Create();
        var pcPos = new CanvasPoint(0, 0);
        var pc = Place(devices, items, DeviceType.Pc, pcPos);
        tool.OnClick(AnchorOf(pc, pcPos, 0), HitRadius);
        Assert.True(tool.HasPendingEndpoint);

        tool.OnClick(new CanvasPoint(9000, 9000), HitRadius);

        Assert.False(tool.HasPendingEndpoint);
        Assert.DoesNotContain(tool.InterfaceEndpoints(), e => e.IsConnected);
    }

    [Fact]
    public void OnClick_PickingAnAlreadyConnectedInterfaceFirst_IsRejectedWithAMessage()
    {
        var (tool, state, items, devices) = Create();
        var aPos = new CanvasPoint(0, 0);
        var bPos = new CanvasPoint(300, 0);
        var pcA = Place(devices, items, DeviceType.Pc, aPos);
        var pcB = Place(devices, items, DeviceType.Pc, bPos);
        tool.OnClick(AnchorOf(pcA, aPos, 0), HitRadius);
        tool.OnClick(AnchorOf(pcB, bPos, 0), HitRadius);
        Assert.Single(state.CurrentNetwork!.Connections);

        tool.OnClick(AnchorOf(pcA, aPos, 0), HitRadius);

        Assert.False(tool.HasPendingEndpoint);
        Assert.Contains("already connected", tool.StatusMessage);
    }

    [Fact]
    public void OnClick_SecondPickWithIncompatibleMedia_SurfacesReasonAndCreatesNoConnection()
    {
        var (tool, state, items, devices) = Create();
        var pcPos = new CanvasPoint(0, 0);
        var routerPos = new CanvasPoint(300, 0);
        var pc = Place(devices, items, DeviceType.Pc, pcPos);
        var router = Place(devices, items, DeviceType.Router, routerPos);

        tool.OnClick(AnchorOf(pc, pcPos, 0), HitRadius);          // Ethernet0
        tool.OnClick(AnchorOf(router, routerPos, 2), HitRadius);  // Serial0/0

        Assert.Empty(state.CurrentNetwork!.Connections);
        // Phase 14: an incompatible second pick surfaces the reason but keeps the first pick
        // active so the user can immediately choose a compatible interface instead.
        Assert.True(tool.HasPendingEndpoint);
        Assert.Contains("cannot be connected", tool.StatusMessage);
    }

    [Fact]
    public void OnClick_SecondPickAfterIncompatible_CanStillCompleteWithACompatibleInterface()
    {
        var (tool, state, items, devices) = Create();
        var pcAPos = new CanvasPoint(0, 0);
        var routerPos = new CanvasPoint(300, 0);
        var pcBPos = new CanvasPoint(600, 0);
        var pcA = Place(devices, items, DeviceType.Pc, pcAPos);
        var router = Place(devices, items, DeviceType.Router, routerPos);
        var pcB = Place(devices, items, DeviceType.Pc, pcBPos);

        tool.OnClick(AnchorOf(pcA, pcAPos, 0), HitRadius);        // Ethernet0
        tool.OnClick(AnchorOf(router, routerPos, 2), HitRadius);  // Serial0/0 - rejected
        tool.OnClick(AnchorOf(pcB, pcBPos, 0), HitRadius);        // Ethernet0 - compatible

        Assert.Single(state.CurrentNetwork!.Connections);
        Assert.False(tool.HasPendingEndpoint);
        Assert.True(pcA.Interfaces.Single().IsConnected);
        Assert.True(pcB.Interfaces.Single().IsConnected);
    }

    [Fact]
    public void OnClick_PickingADisabledInterfaceFirst_IsRejectedWithAMessage()
    {
        var (tool, _, items, devices) = Create();
        var pcPos = new CanvasPoint(0, 0);
        var pc = Place(devices, items, DeviceType.Pc, pcPos);
        pc.Interfaces.Single().Disable();

        tool.OnClick(AnchorOf(pc, pcPos, 0), HitRadius);

        Assert.False(tool.HasPendingEndpoint);
        Assert.Contains("administratively disabled", tool.StatusMessage);
    }

    [Fact]
    public void OnClick_SecondPickOnADisabledInterface_SurfacesReasonAndCreatesNoConnection()
    {
        var (tool, state, items, devices) = Create();
        var aPos = new CanvasPoint(0, 0);
        var bPos = new CanvasPoint(300, 0);
        var pcA = Place(devices, items, DeviceType.Pc, aPos);
        var pcB = Place(devices, items, DeviceType.Pc, bPos);
        pcB.Interfaces.Single().Disable();

        tool.OnClick(AnchorOf(pcA, aPos, 0), HitRadius);
        tool.OnClick(AnchorOf(pcB, bPos, 0), HitRadius);

        Assert.Empty(state.CurrentNetwork!.Connections);
        Assert.Contains("administratively disabled", tool.StatusMessage);
    }

    [Fact]
    public void IsCompatibleWithPending_ReflectsMediaAndAdministrativeState()
    {
        var (tool, _, items, devices) = Create();
        var pcAPos = new CanvasPoint(0, 0);
        var routerPos = new CanvasPoint(300, 0);
        var pcA = Place(devices, items, DeviceType.Pc, pcAPos);
        var router = Place(devices, items, DeviceType.Router, routerPos);

        tool.OnClick(AnchorOf(pcA, pcAPos, 0), HitRadius); // pending = PC Ethernet0

        var endpoints = tool.InterfaceEndpoints();
        var routerGig = endpoints.Single(e => e.DeviceId == router.Id && e.InterfaceName == "GigabitEthernet0/0");
        var routerSerial = endpoints.Single(e => e.DeviceId == router.Id && e.InterfaceName == "Serial0/0");

        Assert.True(tool.IsCompatibleWithPending(routerGig));    // Ethernet <-> Ethernet family
        Assert.False(tool.IsCompatibleWithPending(routerSerial)); // Ethernet <-> Serial
    }

    [Fact]
    public void Cancel_ClearsPendingEndpointAndStatus()
    {
        var (tool, _, items, devices) = Create();
        var pcPos = new CanvasPoint(0, 0);
        var pc = Place(devices, items, DeviceType.Pc, pcPos);
        tool.OnClick(AnchorOf(pc, pcPos, 0), HitRadius);

        tool.Cancel();

        Assert.False(tool.HasPendingEndpoint);
        Assert.Null(tool.StatusMessage);
    }

    [Fact]
    public void OnPointerMoved_TracksTheHoveredInterfaceAnchor()
    {
        var (tool, _, items, devices) = Create();
        var pcPos = new CanvasPoint(0, 0);
        var pc = Place(devices, items, DeviceType.Pc, pcPos);

        tool.OnPointerMoved(AnchorOf(pc, pcPos, 0), HitRadius);
        Assert.NotNull(tool.HoveredEndpoint);

        tool.OnPointerMoved(new CanvasPoint(5000, 5000), HitRadius);
        Assert.Null(tool.HoveredEndpoint);
    }
}
