using NetSim.Application.Canvas;
using NetSim.Application.State;
using NetSim.Core.Common;

namespace NetSim.Application.Tests.Canvas;

public class CanvasSelectionStateTests
{
    [Fact]
    public void Construction_StartsEmpty()
    {
        var state = new CanvasSelectionState(new ApplicationState());

        Assert.Empty(state.SelectedIds);
    }

    [Fact]
    public void Replace_SetsSelectionToGivenIds_AndRaisesEvent()
    {
        var state = new CanvasSelectionState(new ApplicationState());
        var a = EntityId.New();
        var b = EntityId.New();
        var raised = 0;
        state.SelectionChanged += (_, _) => raised++;

        state.Replace([a, b]);

        Assert.True(state.IsSelected(a));
        Assert.True(state.IsSelected(b));
        Assert.Equal(2, state.SelectedIds.Count);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Replace_WithSameSet_DoesNotRaiseEvent()
    {
        var state = new CanvasSelectionState(new ApplicationState());
        var a = EntityId.New();
        state.Replace([a]);
        var raised = false;
        state.SelectionChanged += (_, _) => raised = true;

        state.Replace([a]);

        Assert.False(raised);
    }

    [Fact]
    public void Replace_WithNewSingleId_ReplacesPreviousSelection()
    {
        var state = new CanvasSelectionState(new ApplicationState());
        var a = EntityId.New();
        var b = EntityId.New();
        state.Replace([a]);

        state.Replace([b]);

        Assert.False(state.IsSelected(a));
        Assert.True(state.IsSelected(b));
    }

    [Fact]
    public void Toggle_UnselectedId_AddsIt()
    {
        var state = new CanvasSelectionState(new ApplicationState());
        var a = EntityId.New();

        state.Toggle(a);

        Assert.True(state.IsSelected(a));
    }

    [Fact]
    public void Toggle_AlreadySelectedId_RemovesIt_KeepsOthers()
    {
        var state = new CanvasSelectionState(new ApplicationState());
        var a = EntityId.New();
        var b = EntityId.New();
        state.Replace([a, b]);

        state.Toggle(a);

        Assert.False(state.IsSelected(a));
        Assert.True(state.IsSelected(b));
    }

    [Fact]
    public void Clear_EmptiesSelection_AndRaisesEvent()
    {
        var state = new CanvasSelectionState(new ApplicationState());
        state.Replace([EntityId.New()]);
        var raised = 0;
        state.SelectionChanged += (_, _) => raised++;

        state.Clear();

        Assert.Empty(state.SelectedIds);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Clear_AlreadyEmpty_DoesNotRaiseEvent()
    {
        var state = new CanvasSelectionState(new ApplicationState());
        var raised = false;
        state.SelectionChanged += (_, _) => raised = true;

        state.Clear();

        Assert.False(raised);
    }

    [Fact]
    public void CurrentProjectChanged_ClearsSelection()
    {
        var applicationState = new ApplicationState();
        var state = new CanvasSelectionState(applicationState);
        state.Replace([EntityId.New(), EntityId.New()]);

        applicationState.SetCurrentProject(new Projects.Project(
            EntityId.New(), "Test", null, System.DateTime.UtcNow, System.DateTime.UtcNow, null, 1));

        Assert.Empty(state.SelectedIds);
    }
}
