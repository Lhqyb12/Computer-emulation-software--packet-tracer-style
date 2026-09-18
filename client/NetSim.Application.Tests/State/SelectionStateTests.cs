using NetSim.Application.State;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Application.Tests.State;

public class SelectionStateTests
{
    [Fact]
    public void SelectDevice_SetsSelectedDevice_AndClearsOthers()
    {
        var state = new SelectionState();
        var router = new Router("R1");
        var iface = router.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        state.SelectInterface(iface);

        state.SelectDevice(router);

        Assert.Same(router, state.SelectedDevice);
        Assert.Null(state.SelectedInterface);
        Assert.Null(state.SelectedConnection);
    }

    [Fact]
    public void SelectConnection_SetsSelectedConnection_AndClearsOthers()
    {
        var state = new SelectionState();
        var r1 = new Router("R1");
        var r2 = new Router("R2");
        var ifaceA = r1.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var ifaceB = r2.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        var connection = Connection.Create(ifaceA, ifaceB);
        state.SelectDevice(r1);

        state.SelectConnection(connection);

        Assert.Same(connection, state.SelectedConnection);
        Assert.Null(state.SelectedDevice);
    }

    [Fact]
    public void ClearSelection_NullsEverything_AndRaisesEventOnce()
    {
        var state = new SelectionState();
        var router = new Router("R1");
        state.SelectDevice(router);
        var raiseCount = 0;
        state.SelectionChanged += (_, _) => raiseCount++;

        state.ClearSelection();

        Assert.Null(state.SelectedDevice);
        Assert.Null(state.SelectedInterface);
        Assert.Null(state.SelectedConnection);
        Assert.Equal(1, raiseCount);
    }

    [Fact]
    public void ClearSelection_WhenAlreadyEmpty_DoesNotRaiseEvent()
    {
        var state = new SelectionState();
        var raised = false;
        state.SelectionChanged += (_, _) => raised = true;

        state.ClearSelection();

        Assert.False(raised);
    }
}
