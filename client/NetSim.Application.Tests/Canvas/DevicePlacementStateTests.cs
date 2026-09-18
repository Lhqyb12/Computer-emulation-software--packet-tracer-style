using NetSim.Application.Canvas;
using NetSim.Application.State;
using NetSim.Core.Devices;

namespace NetSim.Application.Tests.Canvas;

public class DevicePlacementStateTests
{
    [Fact]
    public void Construction_NotPlacing()
    {
        var state = new DevicePlacementState(new ApplicationState());

        Assert.False(state.IsPlacing);
        Assert.Null(state.PendingDeviceType);
    }

    [Fact]
    public void Begin_SetsPendingDeviceType_AndRaisesChanged()
    {
        var state = new DevicePlacementState(new ApplicationState());
        var raised = 0;
        state.Changed += (_, _) => raised++;

        state.Begin(DeviceType.Router);

        Assert.True(state.IsPlacing);
        Assert.Equal(DeviceType.Router, state.PendingDeviceType);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Begin_WhileAlreadyPlacing_ReplacesThePendingType()
    {
        var state = new DevicePlacementState(new ApplicationState());
        state.Begin(DeviceType.Router);

        state.Begin(DeviceType.Switch);

        Assert.Equal(DeviceType.Switch, state.PendingDeviceType);
    }

    [Fact]
    public void Cancel_ClearsPendingDeviceType_AndRaisesChanged()
    {
        var state = new DevicePlacementState(new ApplicationState());
        state.Begin(DeviceType.Pc);
        var raised = 0;
        state.Changed += (_, _) => raised++;

        state.Cancel();

        Assert.False(state.IsPlacing);
        Assert.Null(state.PendingDeviceType);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Cancel_WhenNotPlacing_DoesNotRaiseChanged()
    {
        var state = new DevicePlacementState(new ApplicationState());
        var raised = false;
        state.Changed += (_, _) => raised = true;

        state.Cancel();

        Assert.False(raised);
    }

    [Fact]
    public void CurrentProjectChanged_CancelsAnyInProgressPlacement()
    {
        var applicationState = new ApplicationState();
        var state = new DevicePlacementState(applicationState);
        state.Begin(DeviceType.Server);

        applicationState.SetCurrentProject(new Projects.Project(
            Core.Common.EntityId.New(), "Test", null, System.DateTime.UtcNow, System.DateTime.UtcNow, null, 1));

        Assert.False(state.IsPlacing);
    }
}
