using System.Linq;
using NetSim.Application.Canvas;
using NetSim.Application.State;
using NetSim.Core.Common;

namespace NetSim.Application.Tests.Canvas;

public class CanvasInteractionControllerTests
{
    private sealed record Context(CanvasInteractionController Controller, ICanvasState CanvasState, ICanvasItemsState ItemsState, ICanvasSelectionState SelectionState);

    private static Context Create()
    {
        var applicationState = new ApplicationState();
        var canvasState = new CanvasState(applicationState);
        var itemsState = new CanvasItemsState(applicationState);
        var selectionState = new CanvasSelectionState(applicationState);
        SeedTestItems(itemsState);
        var controller = new CanvasInteractionController(canvasState, itemsState, selectionState);
        return new Context(controller, canvasState, itemsState, selectionState);
    }

    // CanvasItemsState starts empty (Phase 11 - see CanvasItemsStateTests); this controller's own
    // tests only care about having a handful of known, non-overlapping items to select/drag
    // against, so they seed their own fixed layout here instead of depending on any production
    // default - the same five positions Phase 9 originally seeded, so every existing scenario
    // below keeps working unmodified.
    private static void SeedTestItems(ICanvasItemsState itemsState)
    {
        itemsState.AddItem(new CanvasItem(EntityId.New(), "Item A", new CanvasPoint(-360, -170), new CanvasSize(140, 70)));
        itemsState.AddItem(new CanvasItem(EntityId.New(), "Item B", new CanvasPoint(-120, -170), new CanvasSize(140, 70)));
        itemsState.AddItem(new CanvasItem(EntityId.New(), "Item C", new CanvasPoint(120, -170), new CanvasSize(140, 70)));
        itemsState.AddItem(new CanvasItem(EntityId.New(), "Item D", new CanvasPoint(-240, 60), new CanvasSize(140, 70)));
        itemsState.AddItem(new CanvasItem(EntityId.New(), "Item E", new CanvasPoint(60, 60), new CanvasSize(140, 70)));
    }

    private static CanvasPoint InsideItem(CanvasItem item) => new(item.Position.X + 5, item.Position.Y + 5);

    private static CanvasPoint OutsideAllItems() => new(5000, 5000);

    private sealed class StubConnectionHitTester(EntityId? id, CanvasPoint at) : IConnectionHitTester
    {
        public CanvasPoint? LastQuery { get; private set; }

        public EntityId? HitTestConnection(CanvasPoint worldPoint, double radius)
        {
            LastQuery = worldPoint;
            var dx = worldPoint.X - at.X;
            var dy = worldPoint.Y - at.Y;
            return (dx * dx) + (dy * dy) <= radius * radius ? id : null;
        }
    }

    // ----- Connection selection (Phase 13) -----

    [Fact]
    public void Click_NearAConnectionLine_SelectsThatConnection()
    {
        var applicationState = new ApplicationState();
        var canvasState = new CanvasState(applicationState);
        var itemsState = new CanvasItemsState(applicationState);
        var selectionState = new CanvasSelectionState(applicationState);
        var connectionId = EntityId.New();
        var hitPoint = new CanvasPoint(400, 400);
        var controller = new CanvasInteractionController(
            canvasState, itemsState, selectionState, new StubConnectionHitTester(connectionId, hitPoint));

        controller.OnPointerPressed(hitPoint, CanvasPointerButton.Left, isCtrlHeld: false);
        controller.OnPointerReleased(hitPoint, CanvasPointerButton.Left);

        Assert.True(selectionState.IsSelected(connectionId));
        Assert.Equal(CanvasInteractionMode.Idle, controller.Mode);
    }

    [Fact]
    public void PointerMoved_OverAConnectionLine_SetsHoveredConnectionId()
    {
        var applicationState = new ApplicationState();
        var canvasState = new CanvasState(applicationState);
        var itemsState = new CanvasItemsState(applicationState);
        var selectionState = new CanvasSelectionState(applicationState);
        var connectionId = EntityId.New();
        var hitPoint = new CanvasPoint(10, 10);
        var controller = new CanvasInteractionController(
            canvasState, itemsState, selectionState, new StubConnectionHitTester(connectionId, hitPoint));

        controller.OnPointerMoved(hitPoint, isCtrlHeld: false);
        Assert.Equal(connectionId, controller.HoveredConnectionId);

        controller.OnPointerMoved(new CanvasPoint(4000, 4000), isCtrlHeld: false);
        Assert.Null(controller.HoveredConnectionId);
    }

    // ----- Selection -----

    [Fact]
    public void Click_OnItem_SelectsIt()
    {
        var ctx = Create();
        var item = ctx.ItemsState.Items[0];

        ctx.Controller.OnPointerPressed(InsideItem(item), CanvasPointerButton.Left, isCtrlHeld: false);
        ctx.Controller.OnPointerReleased(InsideItem(item), CanvasPointerButton.Left);

        Assert.True(ctx.SelectionState.IsSelected(item.Id));
        Assert.Single(ctx.SelectionState.SelectedIds);
        Assert.Equal(CanvasInteractionMode.Idle, ctx.Controller.Mode);
    }

    [Fact]
    public void Click_OnEmptyCanvas_ClearsSelection()
    {
        var ctx = Create();
        var item = ctx.ItemsState.Items[0];
        ctx.SelectionState.Replace([item.Id]);

        ctx.Controller.OnPointerPressed(OutsideAllItems(), CanvasPointerButton.Left, isCtrlHeld: false);
        ctx.Controller.OnPointerReleased(OutsideAllItems(), CanvasPointerButton.Left);

        Assert.Empty(ctx.SelectionState.SelectedIds);
    }

    [Fact]
    public void CtrlClick_UnselectedItem_AddsToExistingSelection()
    {
        var ctx = Create();
        var itemA = ctx.ItemsState.Items[0];
        var itemB = ctx.ItemsState.Items[1];
        ctx.SelectionState.Replace([itemA.Id]);

        ctx.Controller.OnPointerPressed(InsideItem(itemB), CanvasPointerButton.Left, isCtrlHeld: true);
        ctx.Controller.OnPointerReleased(InsideItem(itemB), CanvasPointerButton.Left);

        Assert.True(ctx.SelectionState.IsSelected(itemA.Id));
        Assert.True(ctx.SelectionState.IsSelected(itemB.Id));
    }

    [Fact]
    public void CtrlClick_AlreadySelectedItem_RemovesItFromSelection()
    {
        var ctx = Create();
        var itemA = ctx.ItemsState.Items[0];
        var itemB = ctx.ItemsState.Items[1];
        ctx.SelectionState.Replace([itemA.Id, itemB.Id]);

        ctx.Controller.OnPointerPressed(InsideItem(itemB), CanvasPointerButton.Left, isCtrlHeld: true);
        ctx.Controller.OnPointerReleased(InsideItem(itemB), CanvasPointerButton.Left);

        Assert.True(ctx.SelectionState.IsSelected(itemA.Id));
        Assert.False(ctx.SelectionState.IsSelected(itemB.Id));
    }

    [Fact]
    public void Click_UnselectedItem_WithoutCtrl_ReplacesMultiSelection()
    {
        var ctx = Create();
        var itemA = ctx.ItemsState.Items[0];
        var itemB = ctx.ItemsState.Items[1];
        var itemC = ctx.ItemsState.Items[2];
        ctx.SelectionState.Replace([itemA.Id, itemB.Id]);

        ctx.Controller.OnPointerPressed(InsideItem(itemC), CanvasPointerButton.Left, isCtrlHeld: false);
        ctx.Controller.OnPointerReleased(InsideItem(itemC), CanvasPointerButton.Left);

        Assert.Equal([itemC.Id], ctx.SelectionState.SelectedIds);
    }

    [Fact]
    public void Click_AlreadySelectedItem_WithoutCtrl_KeepsMultiSelection()
    {
        var ctx = Create();
        var itemA = ctx.ItemsState.Items[0];
        var itemB = ctx.ItemsState.Items[1];
        ctx.SelectionState.Replace([itemA.Id, itemB.Id]);

        ctx.Controller.OnPointerPressed(InsideItem(itemA), CanvasPointerButton.Left, isCtrlHeld: false);
        ctx.Controller.OnPointerReleased(InsideItem(itemA), CanvasPointerButton.Left);

        Assert.True(ctx.SelectionState.IsSelected(itemA.Id));
        Assert.True(ctx.SelectionState.IsSelected(itemB.Id));
    }

    [Fact]
    public void RightClick_UnselectedItem_SelectsItWithoutDragging()
    {
        var ctx = Create();
        var item = ctx.ItemsState.Items[0];

        ctx.Controller.OnPointerPressed(InsideItem(item), CanvasPointerButton.Right, isCtrlHeld: false);

        Assert.True(ctx.SelectionState.IsSelected(item.Id));
        Assert.Equal(CanvasInteractionMode.Idle, ctx.Controller.Mode);
    }

    [Fact]
    public void RightClick_AlreadySelectedItem_LeavesMultiSelectionUnchanged()
    {
        var ctx = Create();
        var itemA = ctx.ItemsState.Items[0];
        var itemB = ctx.ItemsState.Items[1];
        ctx.SelectionState.Replace([itemA.Id, itemB.Id]);

        ctx.Controller.OnPointerPressed(InsideItem(itemA), CanvasPointerButton.Right, isCtrlHeld: false);

        Assert.True(ctx.SelectionState.IsSelected(itemA.Id));
        Assert.True(ctx.SelectionState.IsSelected(itemB.Id));
    }

    [Fact]
    public void SelectAll_SelectsEveryItem()
    {
        var ctx = Create();

        ctx.Controller.OnSelectAll();

        Assert.Equal(ctx.ItemsState.Items.Select(i => i.Id).ToHashSet(), ctx.SelectionState.SelectedIds);
    }

    [Fact]
    public void Escape_WhenIdle_ClearsSelection()
    {
        var ctx = Create();
        ctx.SelectionState.Replace([ctx.ItemsState.Items[0].Id]);

        ctx.Controller.OnEscape();

        Assert.Empty(ctx.SelectionState.SelectedIds);
    }

    // ----- Hover -----

    [Fact]
    public void PointerMoved_OverItem_SetsHoveredItemId()
    {
        var ctx = Create();
        var item = ctx.ItemsState.Items[0];

        ctx.Controller.OnPointerMoved(InsideItem(item), isCtrlHeld: false);

        Assert.Equal(item.Id, ctx.Controller.HoveredItemId);
    }

    [Fact]
    public void PointerMoved_OffAllItems_ClearsHoveredItemId()
    {
        var ctx = Create();
        var item = ctx.ItemsState.Items[0];
        ctx.Controller.OnPointerMoved(InsideItem(item), isCtrlHeld: false);

        ctx.Controller.OnPointerMoved(OutsideAllItems(), isCtrlHeld: false);

        Assert.Null(ctx.Controller.HoveredItemId);
    }

    [Fact]
    public void PointerExited_ClearsHoveredItemId()
    {
        var ctx = Create();
        var item = ctx.ItemsState.Items[0];
        ctx.Controller.OnPointerMoved(InsideItem(item), isCtrlHeld: false);

        ctx.Controller.OnPointerExited();

        Assert.Null(ctx.Controller.HoveredItemId);
    }

    // ----- Dragging -----

    [Fact]
    public void Click_WithoutMovement_DoesNotMoveItem_OrEnterDraggingMode()
    {
        var ctx = Create();
        var item = ctx.ItemsState.Items[0];
        var originalPosition = item.Position;

        ctx.Controller.OnPointerPressed(InsideItem(item), CanvasPointerButton.Left, isCtrlHeld: false);
        ctx.Controller.OnPointerReleased(InsideItem(item), CanvasPointerButton.Left);

        Assert.Equal(originalPosition, ctx.ItemsState.Items[0].Position);
        Assert.Equal(CanvasInteractionMode.Idle, ctx.Controller.Mode);
    }

    [Fact]
    public void Drag_PastThreshold_MovesSelectedItem_ByPointerWorldDelta()
    {
        var ctx = Create();
        ctx.CanvasState.SetSnapToGridEnabled(false);
        var item = ctx.ItemsState.Items[0];
        var start = InsideItem(item);
        var originalPosition = item.Position;

        ctx.Controller.OnPointerPressed(start, CanvasPointerButton.Left, isCtrlHeld: false);
        ctx.Controller.OnPointerMoved(new CanvasPoint(start.X + 30, start.Y + 45), isCtrlHeld: false);

        Assert.Equal(CanvasInteractionMode.DraggingItems, ctx.Controller.Mode);
        var moved = ctx.ItemsState.Items[0].Position;
        Assert.Equal(originalPosition.X + 30, moved.X, precision: 6);
        Assert.Equal(originalPosition.Y + 45, moved.Y, precision: 6);
    }

    [Fact]
    public void Drag_BelowThreshold_DoesNotEnterDraggingMode_OrMoveItem()
    {
        var ctx = Create();
        var item = ctx.ItemsState.Items[0];
        var start = InsideItem(item);
        var originalPosition = item.Position;

        ctx.Controller.OnPointerPressed(start, CanvasPointerButton.Left, isCtrlHeld: false);
        ctx.Controller.OnPointerMoved(new CanvasPoint(start.X + 1, start.Y + 1), isCtrlHeld: false);

        Assert.Equal(CanvasInteractionMode.Idle, ctx.Controller.Mode);
        Assert.Equal(originalPosition, ctx.ItemsState.Items[0].Position);
    }

    [Fact]
    public void Drag_MultiSelectedItems_AllMoveByTheSameDelta()
    {
        var ctx = Create();
        ctx.CanvasState.SetSnapToGridEnabled(false);
        var itemA = ctx.ItemsState.Items[0];
        var itemB = ctx.ItemsState.Items[1];
        ctx.SelectionState.Replace([itemA.Id, itemB.Id]);
        var startA = itemA.Position;
        var startB = itemB.Position;
        var pressPoint = InsideItem(itemA);

        ctx.Controller.OnPointerPressed(pressPoint, CanvasPointerButton.Left, isCtrlHeld: false);
        ctx.Controller.OnPointerMoved(new CanvasPoint(pressPoint.X + 20, pressPoint.Y - 10), isCtrlHeld: false);

        Assert.Equal(new CanvasPoint(startA.X + 20, startA.Y - 10), ctx.ItemsState.Items[0].Position);
        Assert.Equal(new CanvasPoint(startB.X + 20, startB.Y - 10), ctx.ItemsState.Items[1].Position);
    }

    [Fact]
    public void Drag_WithSnapEnabled_SnapsResultingPositionToTheGrid()
    {
        var ctx = Create();
        ctx.CanvasState.SetSnapToGridEnabled(true);
        var item = ctx.ItemsState.Items[0];
        var start = InsideItem(item);

        ctx.Controller.OnPointerPressed(start, CanvasPointerButton.Left, isCtrlHeld: false);
        ctx.Controller.OnPointerMoved(new CanvasPoint(start.X + 23, start.Y + 7), isCtrlHeld: false);

        var moved = ctx.ItemsState.Items[0].Position;
        Assert.Equal(0, moved.X % ctx.CanvasState.GridSpacing, precision: 6);
        Assert.Equal(0, moved.Y % ctx.CanvasState.GridSpacing, precision: 6);
    }

    [Fact]
    public void Drag_WithSnapDisabled_DoesNotRoundToTheGrid()
    {
        var ctx = Create();
        ctx.CanvasState.SetSnapToGridEnabled(false);
        var item = ctx.ItemsState.Items[0];
        var start = InsideItem(item);
        var originalPosition = item.Position;

        ctx.Controller.OnPointerPressed(start, CanvasPointerButton.Left, isCtrlHeld: false);
        ctx.Controller.OnPointerMoved(new CanvasPoint(start.X + 23, start.Y + 7), isCtrlHeld: false);

        var moved = ctx.ItemsState.Items[0].Position;
        Assert.Equal(originalPosition.X + 23, moved.X, precision: 6);
        Assert.Equal(originalPosition.Y + 7, moved.Y, precision: 6);
    }

    [Fact]
    public void Drag_AtDoubleZoom_MovesItemByCorrectWorldDistance()
    {
        var ctx = Create();
        ctx.CanvasState.SetSnapToGridEnabled(false);
        ctx.CanvasState.SetZoom(2.0);
        var item = ctx.ItemsState.Items[0];
        var originalPosition = item.Position;

        var startScreen = ctx.CanvasState.Viewport.WorldToScreen(InsideItem(item));
        ctx.Controller.OnPointerPressed(startScreen, CanvasPointerButton.Left, isCtrlHeld: false);

        // Move 100 screen pixels right; at 2x zoom that is 50 world units.
        ctx.Controller.OnPointerMoved(new CanvasPoint(startScreen.X + 100, startScreen.Y), isCtrlHeld: false);

        var moved = ctx.ItemsState.Items[0].Position;
        Assert.Equal(originalPosition.X + 50, moved.X, precision: 6);
        Assert.Equal(originalPosition.Y, moved.Y, precision: 6);
    }

    [Fact]
    public void Drag_AtHalfZoom_MovesItemByCorrectWorldDistance()
    {
        var ctx = Create();
        ctx.CanvasState.SetSnapToGridEnabled(false);
        ctx.CanvasState.SetZoom(0.5);
        var item = ctx.ItemsState.Items[0];
        var originalPosition = item.Position;

        var startScreen = ctx.CanvasState.Viewport.WorldToScreen(InsideItem(item));
        ctx.Controller.OnPointerPressed(startScreen, CanvasPointerButton.Left, isCtrlHeld: false);

        // Move 100 screen pixels right; at 0.5x zoom that is 200 world units.
        ctx.Controller.OnPointerMoved(new CanvasPoint(startScreen.X + 100, startScreen.Y), isCtrlHeld: false);

        var moved = ctx.ItemsState.Items[0].Position;
        Assert.Equal(originalPosition.X + 200, moved.X, precision: 6);
        Assert.Equal(originalPosition.Y, moved.Y, precision: 6);
    }

    [Fact]
    public void Drag_AfterPanning_StillMovesItemByCorrectWorldDistance()
    {
        var ctx = Create();
        ctx.CanvasState.SetSnapToGridEnabled(false);
        ctx.CanvasState.Pan(new CanvasPoint(500, -300));
        var item = ctx.ItemsState.Items[0];
        var originalPosition = item.Position;

        var startScreen = ctx.CanvasState.Viewport.WorldToScreen(InsideItem(item));
        ctx.Controller.OnPointerPressed(startScreen, CanvasPointerButton.Left, isCtrlHeld: false);
        ctx.Controller.OnPointerMoved(new CanvasPoint(startScreen.X + 40, startScreen.Y + 15), isCtrlHeld: false);

        var moved = ctx.ItemsState.Items[0].Position;
        Assert.Equal(originalPosition.X + 40, moved.X, precision: 6);
        Assert.Equal(originalPosition.Y + 15, moved.Y, precision: 6);
    }

    [Fact]
    public void Escape_DuringDrag_RevertsItemToStartPosition()
    {
        var ctx = Create();
        ctx.CanvasState.SetSnapToGridEnabled(false);
        var item = ctx.ItemsState.Items[0];
        var originalPosition = item.Position;
        var start = InsideItem(item);

        ctx.Controller.OnPointerPressed(start, CanvasPointerButton.Left, isCtrlHeld: false);
        ctx.Controller.OnPointerMoved(new CanvasPoint(start.X + 200, start.Y + 200), isCtrlHeld: false);
        ctx.Controller.OnEscape();

        Assert.Equal(originalPosition, ctx.ItemsState.Items[0].Position);
        Assert.Equal(CanvasInteractionMode.Idle, ctx.Controller.Mode);
    }

    // ----- Marquee / selection rectangle -----

    [Fact]
    public void MarqueeDrag_SelectsItemsFullyOrPartiallyInsideRect()
    {
        var ctx = Create();
        var itemA = ctx.ItemsState.Items[0]; // (-360,-170)-(-220,-100)
        var itemC = ctx.ItemsState.Items[2]; // (120,-170)-(260,-100), should stay unselected

        ctx.Controller.OnPointerPressed(new CanvasPoint(-400, -200), CanvasPointerButton.Left, isCtrlHeld: false);
        ctx.Controller.OnPointerMoved(new CanvasPoint(-200, -90), isCtrlHeld: false);

        Assert.Equal(CanvasInteractionMode.SelectingRect, ctx.Controller.Mode);
        Assert.NotNull(ctx.Controller.SelectionRectangle);
        Assert.True(ctx.SelectionState.IsSelected(itemA.Id));
        Assert.False(ctx.SelectionState.IsSelected(itemC.Id));

        ctx.Controller.OnPointerReleased(new CanvasPoint(-200, -90), CanvasPointerButton.Left);

        Assert.Equal(CanvasInteractionMode.Idle, ctx.Controller.Mode);
        Assert.Null(ctx.Controller.SelectionRectangle);
        Assert.True(ctx.SelectionState.IsSelected(itemA.Id));
    }

    [Fact]
    public void MarqueeDrag_WithoutCtrl_ReplacesPriorSelection()
    {
        var ctx = Create();
        var itemC = ctx.ItemsState.Items[2];
        var itemA = ctx.ItemsState.Items[0];
        ctx.SelectionState.Replace([itemC.Id]);

        ctx.Controller.OnPointerPressed(new CanvasPoint(-400, -200), CanvasPointerButton.Left, isCtrlHeld: false);
        ctx.Controller.OnPointerMoved(new CanvasPoint(-200, -90), isCtrlHeld: false);
        ctx.Controller.OnPointerReleased(new CanvasPoint(-200, -90), CanvasPointerButton.Left);

        Assert.True(ctx.SelectionState.IsSelected(itemA.Id));
        Assert.False(ctx.SelectionState.IsSelected(itemC.Id));
    }

    [Fact]
    public void MarqueeDrag_WithCtrl_AddsToExistingSelection()
    {
        var ctx = Create();
        var itemC = ctx.ItemsState.Items[2];
        var itemA = ctx.ItemsState.Items[0];
        ctx.SelectionState.Replace([itemC.Id]);

        ctx.Controller.OnPointerPressed(new CanvasPoint(-400, -200), CanvasPointerButton.Left, isCtrlHeld: true);
        ctx.Controller.OnPointerMoved(new CanvasPoint(-200, -90), isCtrlHeld: true);
        ctx.Controller.OnPointerReleased(new CanvasPoint(-200, -90), CanvasPointerButton.Left);

        Assert.True(ctx.SelectionState.IsSelected(itemA.Id));
        Assert.True(ctx.SelectionState.IsSelected(itemC.Id));
    }

    [Fact]
    public void Escape_DuringMarquee_RestoresSelectionFromBeforeTheDrag()
    {
        var ctx = Create();
        var itemC = ctx.ItemsState.Items[2];
        var itemA = ctx.ItemsState.Items[0];
        ctx.SelectionState.Replace([itemC.Id]);

        ctx.Controller.OnPointerPressed(new CanvasPoint(-400, -200), CanvasPointerButton.Left, isCtrlHeld: true);
        ctx.Controller.OnPointerMoved(new CanvasPoint(-200, -90), isCtrlHeld: true);
        ctx.Controller.OnEscape();

        Assert.False(ctx.SelectionState.IsSelected(itemA.Id));
        Assert.True(ctx.SelectionState.IsSelected(itemC.Id));
        Assert.Null(ctx.Controller.SelectionRectangle);
        Assert.Equal(CanvasInteractionMode.Idle, ctx.Controller.Mode);
    }

    // ----- Panning -----

    [Fact]
    public void MiddleButtonDrag_PansTheViewport()
    {
        var ctx = Create();

        ctx.Controller.OnPointerPressed(new CanvasPoint(100, 100), CanvasPointerButton.Middle, isCtrlHeld: false);
        Assert.Equal(CanvasInteractionMode.Panning, ctx.Controller.Mode);

        ctx.Controller.OnPointerMoved(new CanvasPoint(150, 80), isCtrlHeld: false);

        Assert.Equal(new CanvasPoint(-50, 20), ctx.CanvasState.Viewport.Origin);

        ctx.Controller.OnPointerReleased(new CanvasPoint(150, 80), CanvasPointerButton.Middle);
        Assert.Equal(CanvasInteractionMode.Idle, ctx.Controller.Mode);
    }

    [Fact]
    public void MiddleButtonPan_DoesNotMoveOrSelectItemsUnderneath()
    {
        var ctx = Create();
        var item = ctx.ItemsState.Items[0];
        var originalPosition = item.Position;
        var pressPoint = InsideItem(item);

        ctx.Controller.OnPointerPressed(pressPoint, CanvasPointerButton.Middle, isCtrlHeld: false);
        ctx.Controller.OnPointerMoved(new CanvasPoint(pressPoint.X + 50, pressPoint.Y + 50), isCtrlHeld: false);

        Assert.Equal(originalPosition, ctx.ItemsState.Items[0].Position);
        Assert.Empty(ctx.SelectionState.SelectedIds);
    }

    [Fact]
    public void SpaceHeldPlusLeftDrag_Pans()
    {
        var ctx = Create();
        ctx.Controller.SetSpaceHeld(true);

        ctx.Controller.OnPointerPressed(new CanvasPoint(100, 100), CanvasPointerButton.Left, isCtrlHeld: false);

        Assert.Equal(CanvasInteractionMode.Panning, ctx.Controller.Mode);
    }

    [Fact]
    public void ReleasingSpace_MidPan_StopsThePan()
    {
        var ctx = Create();
        ctx.Controller.SetSpaceHeld(true);
        ctx.Controller.OnPointerPressed(new CanvasPoint(100, 100), CanvasPointerButton.Left, isCtrlHeld: false);

        ctx.Controller.SetSpaceHeld(false);

        Assert.Equal(CanvasInteractionMode.Idle, ctx.Controller.Mode);
    }

    [Fact]
    public void PanTool_LeftDrag_PansInsteadOfSelectingAnItemUnderneath()
    {
        var ctx = Create();
        ctx.CanvasState.SetCurrentTool(CanvasTool.Pan);
        var item = ctx.ItemsState.Items[0];

        ctx.Controller.OnPointerPressed(InsideItem(item), CanvasPointerButton.Left, isCtrlHeld: false);

        Assert.Equal(CanvasInteractionMode.Panning, ctx.Controller.Mode);
        Assert.Empty(ctx.SelectionState.SelectedIds);
    }

    // ----- Zoom -----

    [Fact]
    public void Wheel_PositiveDelta_ZoomsIn()
    {
        var ctx = Create();

        ctx.Controller.OnWheel(new CanvasPoint(400, 300), 1);

        Assert.True(ctx.CanvasState.Viewport.Zoom > 1.0);
    }

    [Fact]
    public void Wheel_NegativeDelta_ZoomsOut()
    {
        var ctx = Create();

        ctx.Controller.OnWheel(new CanvasPoint(400, 300), -1);

        Assert.True(ctx.CanvasState.Viewport.Zoom < 1.0);
    }

    [Fact]
    public void Wheel_RepeatedZoomIn_NeverExceedsMaxZoom()
    {
        var ctx = Create();

        for (var i = 0; i < 200; i++)
        {
            ctx.Controller.OnWheel(new CanvasPoint(400, 300), 1);
        }

        Assert.Equal(CanvasViewport.MaxZoom, ctx.CanvasState.Viewport.Zoom);
    }

    [Fact]
    public void Wheel_RepeatedZoomOut_NeverGoesBelowMinZoom()
    {
        var ctx = Create();

        for (var i = 0; i < 200; i++)
        {
            ctx.Controller.OnWheel(new CanvasPoint(400, 300), -1);
        }

        Assert.Equal(CanvasViewport.MinZoom, ctx.CanvasState.Viewport.Zoom);
    }

    [Fact]
    public void Wheel_KeepsWorldPointUnderCursorFixed()
    {
        var ctx = Create();
        var cursor = new CanvasPoint(250, 180);
        var worldBefore = ctx.CanvasState.Viewport.ScreenToWorld(cursor);

        ctx.Controller.OnWheel(cursor, 3);

        var worldAfter = ctx.CanvasState.Viewport.ScreenToWorld(cursor);
        Assert.Equal(worldBefore.X, worldAfter.X, precision: 6);
        Assert.Equal(worldBefore.Y, worldAfter.Y, precision: 6);
    }

    // ----- Changed event -----

    [Fact]
    public void Changed_IsRaised_AfterPointerHandlers()
    {
        var ctx = Create();
        var raised = 0;
        ctx.Controller.Changed += (_, _) => raised++;

        ctx.Controller.OnPointerMoved(new CanvasPoint(1, 1), isCtrlHeld: false);

        Assert.Equal(1, raised);
    }
}
