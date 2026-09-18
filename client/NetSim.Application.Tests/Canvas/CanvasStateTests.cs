using NetSim.Application.Canvas;
using NetSim.Application.State;

namespace NetSim.Application.Tests.Canvas;

public class CanvasStateTests
{
    [Fact]
    public void Construction_DefaultsToIdentityViewport_GridVisible_SelectTool()
    {
        var state = new CanvasState(new ApplicationState());

        Assert.Equal(CanvasViewport.Default, state.Viewport);
        Assert.True(state.IsGridVisible);
        Assert.True(state.IsSnapToGridEnabled);
        Assert.Equal(CanvasTool.Select, state.CurrentTool);
        Assert.Equal(CanvasWorldBounds.Default, state.WorldBounds);
    }

    [Fact]
    public void SetZoom_WithinLimits_UpdatesViewport_AndRaisesEvent()
    {
        var state = new CanvasState(new ApplicationState());
        var raised = 0;
        state.ViewportChanged += (_, _) => raised++;

        state.SetZoom(2.0);

        Assert.Equal(2.0, state.Viewport.Zoom);
        Assert.Equal(1, raised);
    }

    [Theory]
    [InlineData(0.0001, CanvasViewport.MinZoom)]
    [InlineData(1000, CanvasViewport.MaxZoom)]
    public void SetZoom_OutOfRange_Clamps(double requested, double expected)
    {
        var state = new CanvasState(new ApplicationState());

        state.SetZoom(requested);

        Assert.Equal(expected, state.Viewport.Zoom);
    }

    [Fact]
    public void Pan_MovesOriginByWorldDelta_KeepsZoom()
    {
        var state = new CanvasState(new ApplicationState());
        state.SetZoom(2.0);

        state.Pan(new CanvasPoint(10, -5));

        Assert.Equal(new CanvasPoint(10, -5), state.Viewport.Origin);
        Assert.Equal(2.0, state.Viewport.Zoom);
    }

    [Fact]
    public void ResetViewport_RestoresDefault_AfterPanAndZoom()
    {
        var state = new CanvasState(new ApplicationState());
        state.SetZoom(3.0);
        state.Pan(new CanvasPoint(500, 500));

        state.ResetViewport();

        Assert.Equal(CanvasViewport.Default, state.Viewport);
    }

    [Fact]
    public void SetViewport_SameValue_DoesNotRaiseEvent()
    {
        var state = new CanvasState(new ApplicationState());
        var raised = false;
        state.ViewportChanged += (_, _) => raised = true;

        state.SetViewport(CanvasViewport.Default);

        Assert.False(raised);
    }

    [Fact]
    public void SetGridVisible_TogglesFlag_AndRaisesEvent()
    {
        var state = new CanvasState(new ApplicationState());
        var raised = 0;
        state.GridSettingsChanged += (_, _) => raised++;

        state.SetGridVisible(false);

        Assert.False(state.IsGridVisible);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetGridVisible_SameValue_DoesNotRaiseEvent()
    {
        var state = new CanvasState(new ApplicationState());
        var raised = false;
        state.GridSettingsChanged += (_, _) => raised = true;

        state.SetGridVisible(true); // already true by default

        Assert.False(raised);
    }

    [Fact]
    public void SetSnapToGridEnabled_TogglesFlag_AndRaisesGridSettingsChanged()
    {
        var state = new CanvasState(new ApplicationState());
        var raised = 0;
        state.GridSettingsChanged += (_, _) => raised++;

        state.SetSnapToGridEnabled(false);

        Assert.False(state.IsSnapToGridEnabled);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetSnapToGridEnabled_SameValue_DoesNotRaiseEvent()
    {
        var state = new CanvasState(new ApplicationState());
        var raised = false;
        state.GridSettingsChanged += (_, _) => raised = true;

        state.SetSnapToGridEnabled(true); // already true by default

        Assert.False(raised);
    }

    [Fact]
    public void SetCurrentTool_ChangesTool_AndRaisesEvent()
    {
        var state = new CanvasState(new ApplicationState());
        var raised = 0;
        state.CurrentToolChanged += (_, _) => raised++;

        state.SetCurrentTool(CanvasTool.Pan);

        Assert.Equal(CanvasTool.Pan, state.CurrentTool);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void CurrentProjectChanged_ResetsViewportGridAndTool()
    {
        var applicationState = new ApplicationState();
        var state = new CanvasState(applicationState);
        state.SetZoom(3.5);
        state.Pan(new CanvasPoint(200, 200));
        state.SetGridVisible(false);
        state.SetSnapToGridEnabled(false);
        state.SetCurrentTool(CanvasTool.Pan);

        applicationState.SetCurrentProject(new Projects.Project(
            Core.Common.EntityId.New(), "Test", null, System.DateTime.UtcNow, System.DateTime.UtcNow, null, 1));

        Assert.Equal(CanvasViewport.Default, state.Viewport);
        Assert.True(state.IsGridVisible);
        Assert.True(state.IsSnapToGridEnabled);
        Assert.Equal(CanvasTool.Select, state.CurrentTool);
    }
}
