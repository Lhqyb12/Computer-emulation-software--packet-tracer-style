using NetSim.Application.Canvas;
using NetSim.Application.State;
using NetSim.Core.Common;
using NetSim.Core.Devices;

namespace NetSim.Application.Tests.Canvas;

public class CanvasItemsStateTests
{
    private static CanvasItem MakeItem(string label, CanvasPoint position, DeviceType? deviceType = null) =>
        new(EntityId.New(), label, position, new CanvasSize(140, 70), deviceType);

    [Fact]
    public void Construction_StartsEmpty()
    {
        var state = new CanvasItemsState(new ApplicationState());

        Assert.Empty(state.Items);
    }

    [Fact]
    public void AddItem_AddsItAndRaisesItemsChanged()
    {
        var state = new CanvasItemsState(new ApplicationState());
        var raised = 0;
        state.ItemsChanged += (_, _) => raised++;
        var item = MakeItem("Router0", new CanvasPoint(0, 0), DeviceType.Router);

        state.AddItem(item);

        Assert.Single(state.Items);
        Assert.Same(item, state.Items[0]);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void HitTest_PointInsideAnItem_ReturnsThatItem()
    {
        var state = new CanvasItemsState(new ApplicationState());
        var target = MakeItem("A", new CanvasPoint(100, 100));
        state.AddItem(target);

        var hit = state.HitTest(new CanvasPoint(target.Position.X + 1, target.Position.Y + 1));

        Assert.Equal(target.Id, hit?.Id);
    }

    [Fact]
    public void HitTest_PointOutsideEveryItem_ReturnsNull()
    {
        var state = new CanvasItemsState(new ApplicationState());
        state.AddItem(MakeItem("A", new CanvasPoint(0, 0)));

        var hit = state.HitTest(new CanvasPoint(1_000_000, 1_000_000));

        Assert.Null(hit);
    }

    [Fact]
    public void HitTest_OverlappingItems_ReturnsTheTopmostOne()
    {
        var state = new CanvasItemsState(new ApplicationState());
        var first = MakeItem("A", new CanvasPoint(0, 0));
        var last = MakeItem("B", new CanvasPoint(500, 500));
        state.AddItem(first);
        state.AddItem(last);

        // Move the last (topmost, by z-order) item on top of the first one.
        state.MoveItem(last.Id, first.Position);

        var hit = state.HitTest(new CanvasPoint(first.Position.X + 1, first.Position.Y + 1));

        Assert.Equal(last.Id, hit?.Id);
    }

    [Fact]
    public void MoveItem_UpdatesPosition_AndRaisesItemsChanged()
    {
        var state = new CanvasItemsState(new ApplicationState());
        var target = MakeItem("A", new CanvasPoint(0, 0));
        state.AddItem(target);
        var raised = 0;
        state.ItemsChanged += (_, _) => raised++;

        state.MoveItem(target.Id, new CanvasPoint(999, -999));

        Assert.Equal(new CanvasPoint(999, -999), state.Items[0].Position);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void MoveItem_UnknownId_DoesNothing_AndDoesNotRaiseItemsChanged()
    {
        var state = new CanvasItemsState(new ApplicationState());
        var raised = false;
        state.ItemsChanged += (_, _) => raised = true;

        state.MoveItem(EntityId.New(), new CanvasPoint(1, 1));

        Assert.False(raised);
    }

    [Fact]
    public void RenameItem_UpdatesLabel_AndRaisesItemsChanged()
    {
        var state = new CanvasItemsState(new ApplicationState());
        var target = MakeItem("Router0", new CanvasPoint(0, 0), DeviceType.Router);
        state.AddItem(target);
        var raised = 0;
        state.ItemsChanged += (_, _) => raised++;

        state.RenameItem(target.Id, "CoreRouter");

        Assert.Equal("CoreRouter", state.Items[0].Label);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void RenameItem_UnknownId_DoesNothing_AndDoesNotRaiseItemsChanged()
    {
        var state = new CanvasItemsState(new ApplicationState());
        var raised = false;
        state.ItemsChanged += (_, _) => raised = true;

        state.RenameItem(EntityId.New(), "New Name");

        Assert.False(raised);
    }

    [Fact]
    public void CurrentProjectChanged_ClearsAllItems()
    {
        var applicationState = new ApplicationState();
        var state = new CanvasItemsState(applicationState);
        state.AddItem(MakeItem("A", new CanvasPoint(0, 0)));

        applicationState.SetCurrentProject(new Projects.Project(
            EntityId.New(), "Test", null, System.DateTime.UtcNow, System.DateTime.UtcNow, null, 1));

        Assert.Empty(state.Items);
    }

    [Fact]
    public void CurrentProjectChanged_WhenAlreadyEmpty_DoesNotRaiseItemsChanged()
    {
        var applicationState = new ApplicationState();
        var state = new CanvasItemsState(applicationState);
        var raised = false;
        state.ItemsChanged += (_, _) => raised = true;

        applicationState.SetCurrentProject(new Projects.Project(
            EntityId.New(), "Test", null, System.DateTime.UtcNow, System.DateTime.UtcNow, null, 1));

        Assert.False(raised);
    }
}
