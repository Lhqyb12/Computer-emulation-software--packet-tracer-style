using System;
using System.Collections.Generic;
using System.Linq;
using NetSim.Core.Common;

namespace NetSim.Application.Canvas;

/// <summary>
/// The Network Canvas's "Canvas Interaction Layer": translates already-decoded pointer/keyboard
/// intent (screen point, which button, which modifiers) into changes to <see cref="ICanvasState"/>
/// (viewport/tool), <see cref="ICanvasSelectionState"/> and <see cref="ICanvasItemsState"/>. See
/// docs/architecture/canvas-interaction.md for the full input-to-state flow this implements:
///
///   User Input (Avalonia events, NetworkCanvasControl)
///     -> Canvas Interaction Layer (this class)
///     -> Current Tool / Interaction Mode (ICanvasState.CurrentTool, Mode)
///     -> Canvas State (ICanvasState)
///     -> Viewport / Selection / Canvas Objects (ICanvasState.Viewport, ICanvasSelectionState, ICanvasItemsState)
///
/// Deliberately has zero Avalonia dependency - every method takes plain <see cref="CanvasPoint"/>/
/// bool/enum parameters - so the entire interaction state machine is unit-testable without
/// starting the UI (see tests/NetSim.Application.Tests/Canvas/CanvasInteractionControllerTests.cs).
/// NetworkCanvasControl only translates Avalonia event args into calls on this type and reads
/// <see cref="Mode"/>/<see cref="HoveredItemId"/>/<see cref="SelectionRectangle"/> back for
/// cursor/overlay rendering - it holds no interaction logic of its own.
/// </summary>
public sealed class CanvasInteractionController
{
    private const double WheelZoomStep = 1.1;
    private const double DragThresholdSquared = 4.0 * 4.0;

    private enum PendingGesture
    {
        None,
        DragItems,
        SelectRect,
    }

    private readonly ICanvasState _canvasState;
    private readonly ICanvasItemsState _itemsState;
    private readonly ICanvasSelectionState _selectionState;
    private readonly IConnectionHitTester? _connectionHitTester;

    private PendingGesture _pendingGesture = PendingGesture.None;
    private CanvasPoint _pointerDownScreen;
    private CanvasPoint _pointerDownWorld;
    private CanvasPoint _lastPointerScreen;
    private Dictionary<EntityId, CanvasPoint> _dragStartPositions = new();
    private HashSet<EntityId> _dragBaseSelection = [];
    private bool _isSpaceHeld;
    private bool _panInitiatedBySpace;

    public CanvasInteractionController(
        ICanvasState canvasState,
        ICanvasItemsState itemsState,
        ICanvasSelectionState selectionState,
        IConnectionHitTester? connectionHitTester = null)
    {
        ArgumentNullException.ThrowIfNull(canvasState);
        ArgumentNullException.ThrowIfNull(itemsState);
        ArgumentNullException.ThrowIfNull(selectionState);

        _canvasState = canvasState;
        _itemsState = itemsState;
        _selectionState = selectionState;
        _connectionHitTester = connectionHitTester;
    }

    public CanvasInteractionMode Mode { get; private set; } = CanvasInteractionMode.Idle;

    /// <summary>Connection line currently under the pointer (Select mode), or null - drives hover styling.</summary>
    public EntityId? HoveredConnectionId { get; private set; }

    /// <summary>Last known pointer position, in world coordinates - drives the canvas coordinate readout.</summary>
    public CanvasPoint PointerWorldPosition { get; private set; }

    public EntityId? HoveredItemId { get; private set; }

    public CanvasSelectionRectangle? SelectionRectangle { get; private set; }

    public bool IsSpaceHeld => _isSpaceHeld;

    /// <summary>Raised after any input handler runs, whether or not it visibly changed anything - the UI redraws unconditionally rather than diffing.</summary>
    public event EventHandler? Changed;

    public void OnWheel(CanvasPoint screenPoint, double wheelDeltaY)
    {
        if (wheelDeltaY == 0)
        {
            return;
        }

        var factor = Math.Pow(WheelZoomStep, wheelDeltaY);
        var newZoom = _canvasState.Viewport.Zoom * factor;
        _canvasState.SetViewport(_canvasState.Viewport.ZoomAround(screenPoint, newZoom));

        RaiseChanged();
    }

    public void OnPointerPressed(CanvasPoint screenPoint, CanvasPointerButton button, bool isCtrlHeld)
    {
        _pointerDownScreen = screenPoint;
        _lastPointerScreen = screenPoint;
        _pointerDownWorld = _canvasState.Viewport.ScreenToWorld(screenPoint);
        _pendingGesture = PendingGesture.None;

        var isPanGesture = button == CanvasPointerButton.Middle
            || (button == CanvasPointerButton.Left && (_isSpaceHeld || _canvasState.CurrentTool == CanvasTool.Pan));

        if (isPanGesture)
        {
            _panInitiatedBySpace = button == CanvasPointerButton.Left && _isSpaceHeld;
            Mode = CanvasInteractionMode.Panning;
            RaiseChanged();
            return;
        }

        if (button == CanvasPointerButton.Right)
        {
            var rightHit = _itemsState.HitTest(_pointerDownWorld);
            if (rightHit is not null && !_selectionState.IsSelected(rightHit.Id))
            {
                _selectionState.Replace([rightHit.Id]);
            }

            RaiseChanged();
            return;
        }

        if (button != CanvasPointerButton.Left)
        {
            return;
        }

        var hitItem = _itemsState.HitTest(_pointerDownWorld);
        if (hitItem is not null)
        {
            if (isCtrlHeld)
            {
                _selectionState.Toggle(hitItem.Id);
            }
            else if (!_selectionState.IsSelected(hitItem.Id))
            {
                _selectionState.Replace([hitItem.Id]);
            }
            // else: already part of a multi-selection - keep it as-is so the whole group can be dragged.

            if (_selectionState.IsSelected(hitItem.Id))
            {
                _pendingGesture = PendingGesture.DragItems;
                _dragStartPositions = _itemsState.Items
                    .Where(i => _selectionState.IsSelected(i.Id))
                    .ToDictionary(i => i.Id, i => i.Position);
            }
        }
        else if (_connectionHitTester?.HitTestConnection(_pointerDownWorld, ConnectionHitRadius()) is { } connectionId)
        {
            // Clicked a cable, not a device and not empty space - select the Connection it
            // represents through the same id-based selection devices use. No drag/marquee starts.
            if (isCtrlHeld)
            {
                _selectionState.Toggle(connectionId);
            }
            else
            {
                _selectionState.Replace([connectionId]);
            }
        }
        else
        {
            _dragBaseSelection = isCtrlHeld ? [.. _selectionState.SelectedIds] : [];
            if (!isCtrlHeld)
            {
                _selectionState.Clear();
            }

            _pendingGesture = PendingGesture.SelectRect;
        }

        RaiseChanged();
    }

    /// <summary>Click tolerance for connection lines, in world units (~6px regardless of zoom).</summary>
    private double ConnectionHitRadius() => 6.0 / _canvasState.Viewport.Zoom;

    public void OnPointerMoved(CanvasPoint screenPoint, bool isCtrlHeld)
    {
        var world = _canvasState.Viewport.ScreenToWorld(screenPoint);
        PointerWorldPosition = world;

        switch (Mode)
        {
            case CanvasInteractionMode.Panning:
                ApplyPan(screenPoint);
                _lastPointerScreen = screenPoint;
                RaiseChanged();
                return;

            case CanvasInteractionMode.DraggingItems:
                ApplyDrag(world);
                RaiseChanged();
                return;

            case CanvasInteractionMode.SelectingRect:
                UpdateSelectionRect(world);
                RaiseChanged();
                return;
        }

        if (_pendingGesture != PendingGesture.None && HasPassedDragThreshold(screenPoint))
        {
            if (_pendingGesture == PendingGesture.DragItems)
            {
                Mode = CanvasInteractionMode.DraggingItems;
                ApplyDrag(world);
            }
            else if (_pendingGesture == PendingGesture.SelectRect)
            {
                Mode = CanvasInteractionMode.SelectingRect;
                UpdateSelectionRect(world);
            }
        }

        var hit = _itemsState.HitTest(world);
        HoveredItemId = hit?.Id;

        // A device body always wins the hover over a cable that happens to pass behind it.
        HoveredConnectionId = hit is null
            ? _connectionHitTester?.HitTestConnection(world, ConnectionHitRadius())
            : null;

        RaiseChanged();
    }

    public void OnPointerReleased(CanvasPoint screenPoint, CanvasPointerButton button)
    {
        if (Mode == CanvasInteractionMode.Panning)
        {
            if (button == CanvasPointerButton.Middle || button == CanvasPointerButton.Left)
            {
                Mode = CanvasInteractionMode.Idle;
                _panInitiatedBySpace = false;
                RaiseChanged();
            }

            return;
        }

        if (button != CanvasPointerButton.Left)
        {
            return;
        }

        if (Mode == CanvasInteractionMode.SelectingRect)
        {
            SelectionRectangle = null;
        }

        Mode = CanvasInteractionMode.Idle;
        _pendingGesture = PendingGesture.None;
        _dragStartPositions = new Dictionary<EntityId, CanvasPoint>();
        RaiseChanged();
    }

    public void OnPointerExited()
    {
        if (HoveredItemId is null && HoveredConnectionId is null)
        {
            return;
        }

        HoveredItemId = null;
        HoveredConnectionId = null;
        RaiseChanged();
    }

    public void SetSpaceHeld(bool isHeld)
    {
        if (_isSpaceHeld == isHeld)
        {
            return;
        }

        _isSpaceHeld = isHeld;

        if (!isHeld && Mode == CanvasInteractionMode.Panning && _panInitiatedBySpace)
        {
            Mode = CanvasInteractionMode.Idle;
            _panInitiatedBySpace = false;
        }

        RaiseChanged();
    }

    /// <summary>Cancels whatever is currently in progress; clears selection if nothing was in progress.</summary>
    public void OnEscape()
    {
        switch (Mode)
        {
            case CanvasInteractionMode.DraggingItems:
                foreach (var (id, startPosition) in _dragStartPositions)
                {
                    _itemsState.MoveItem(id, startPosition);
                }

                Mode = CanvasInteractionMode.Idle;
                _pendingGesture = PendingGesture.None;
                break;

            case CanvasInteractionMode.SelectingRect:
                _selectionState.Replace(_dragBaseSelection);
                SelectionRectangle = null;
                Mode = CanvasInteractionMode.Idle;
                _pendingGesture = PendingGesture.None;
                break;

            case CanvasInteractionMode.Panning:
                Mode = CanvasInteractionMode.Idle;
                _panInitiatedBySpace = false;
                break;

            default:
                _selectionState.Clear();
                break;
        }

        RaiseChanged();
    }

    public void OnSelectAll()
    {
        _selectionState.Replace(_itemsState.Items.Select(i => i.Id));
        RaiseChanged();
    }

    private void ApplyPan(CanvasPoint screenPoint)
    {
        var zoom = _canvasState.Viewport.Zoom;
        var deltaScreenX = screenPoint.X - _lastPointerScreen.X;
        var deltaScreenY = screenPoint.Y - _lastPointerScreen.Y;

        // Content should follow the cursor, so the origin (world point at the screen's top-left)
        // moves the opposite way the pointer moved.
        _canvasState.Pan(new CanvasPoint(-deltaScreenX / zoom, -deltaScreenY / zoom));
    }

    private void ApplyDrag(CanvasPoint world)
    {
        var deltaX = world.X - _pointerDownWorld.X;
        var deltaY = world.Y - _pointerDownWorld.Y;
        var snap = _canvasState.IsSnapToGridEnabled;
        var spacing = _canvasState.GridSpacing;

        foreach (var (id, startPosition) in _dragStartPositions)
        {
            var newPosition = new CanvasPoint(startPosition.X + deltaX, startPosition.Y + deltaY);
            if (snap)
            {
                newPosition = CanvasGridSnap.Snap(newPosition, spacing);
            }

            _itemsState.MoveItem(id, newPosition);
        }
    }

    private void UpdateSelectionRect(CanvasPoint world)
    {
        var min = new CanvasPoint(Math.Min(_pointerDownWorld.X, world.X), Math.Min(_pointerDownWorld.Y, world.Y));
        var max = new CanvasPoint(Math.Max(_pointerDownWorld.X, world.X), Math.Max(_pointerDownWorld.Y, world.Y));
        SelectionRectangle = new CanvasSelectionRectangle(min, max);

        var combined = new HashSet<EntityId>(_dragBaseSelection);
        combined.UnionWith(_itemsState.Items.Where(i => i.Intersects(min, max)).Select(i => i.Id));
        _selectionState.Replace(combined);
    }

    private bool HasPassedDragThreshold(CanvasPoint screenPoint)
    {
        var dx = screenPoint.X - _pointerDownScreen.X;
        var dy = screenPoint.Y - _pointerDownScreen.Y;
        return dx * dx + dy * dy >= DragThresholdSquared;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
