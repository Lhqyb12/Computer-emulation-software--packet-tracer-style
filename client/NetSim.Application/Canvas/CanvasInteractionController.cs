using System;                      // EventHandler, EventArgs, Math, ArgumentNullException
using System.Collections.Generic;  // Dictionary<TKey, TValue>, HashSet<T>
using System.Linq;                 // Where, Select, ToDictionary
using NetSim.Application.Services; // IDeviceService - the one door for moving devices
using NetSim.Application.State;    // IApplicationState - the open network
using NetSim.Core.Common;          // EntityId

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The mouse logic of the Select and Pan tools (steps 4-5): what a click, a drag, the wheel and a few
// keys MEAN on the canvas. It gets plain calls ("pressed at this point, with this button") from the
// canvas control, decides, and changes the states (selection, camera) or moves devices through DeviceService.
// It knows nothing about Avalonia, so all of it could be tested without a screen.
//   - Click a device           -> select it (Ctrl + click adds / removes it).
//   - Drag a selected device   -> move all the selected devices together (snapping to the grid if on).
//   - Click a cable            -> select it.
//   - Drag on empty canvas     -> draw a rectangle; everything it touches gets selected.
//   - Wheel                    -> zoom around the mouse.
//   - Middle button, Space+drag, or the Pan tool -> scroll the canvas.
// Changes from the reference project: devices are moved through DeviceService.MoveDevice (so a move
// marks the project "unsaved" - step 2's rule), and cables are found with CanvasConnectionGeometry.
// "sealed" = no other class can inherit from it
public sealed class CanvasInteractionController
{
    // One wheel "click" zooms by 10%
    private const double WheelZoomStep = 1.1;

    // The mouse must move 4 screen units before a press becomes a drag (4 * 4 = 16, compared without a square root).
    // Without this, a tiny shake of the hand during a click would move the device a little
    private const double DragThresholdSquared = 4.0 * 4.0;

    // How close to a cable (in screen units) a click must be to pick it
    private const double CableHitScreenRadius = 6.0;

    // What a left press MAY turn into once the mouse moves far enough
    private enum PendingGesture
    {
        None,         // nothing
        DragItems,    // pressed on a selected device - moving will drag the selection
        SelectRect,   // pressed on empty canvas - moving will draw a selection rectangle
    }

    private readonly ICanvasState _canvasState;               // the camera, grid and tool
    private readonly ICanvasItemsState _itemsState;           // where every device is
    private readonly ICanvasSelectionState _selectionState;   // what is selected
    private readonly IDeviceService _deviceService;           // moves devices (and marks "unsaved")
    private readonly IApplicationState _applicationState;     // the open network (to find devices and cables)

    private PendingGesture _pendingGesture = PendingGesture.None;   // what the current press may become
    private CanvasPoint _pointerDownScreen;                          // where the button was pressed (screen)
    private CanvasPoint _pointerDownWorld;                           // the same place, in the world
    private CanvasPoint _lastPointerScreen;                          // the last mouse position (for panning)
    private Dictionary<EntityId, CanvasPoint> _dragStartPositions = [];   // where each dragged device started (to undo with Escape)
    private HashSet<EntityId> _rectBaseSelection = [];               // what was selected before the rectangle started (kept with Ctrl)
    private bool _isSpaceHeld;                                       // is the Space key held down
    private bool _panStartedBySpace;                                 // did this pan start because Space was held

    // Purpose:       Builds the controller.
    // Precondition:  all five are not null.
    // Postcondition: the controller is idle (nothing pressed, nothing hovered).
    public CanvasInteractionController(
        ICanvasState canvasState,
        ICanvasItemsState itemsState,
        ICanvasSelectionState selectionState,
        IDeviceService deviceService,
        IApplicationState applicationState)
    {
        // Stop right away with a clear message if something was not passed in
        ArgumentNullException.ThrowIfNull(canvasState);
        ArgumentNullException.ThrowIfNull(itemsState);
        ArgumentNullException.ThrowIfNull(selectionState);
        ArgumentNullException.ThrowIfNull(deviceService);
        ArgumentNullException.ThrowIfNull(applicationState);

        _canvasState = canvasState;             // keep the camera / grid / tool
        _itemsState = itemsState;               // keep the canvas positions
        _selectionState = selectionState;       // keep the selection
        _deviceService = deviceService;         // keep the device rules
        _applicationState = applicationState;   // keep the open network
    }

    // What the mouse is doing right now
    public CanvasInteractionMode Mode { get; private set; } = CanvasInteractionMode.Idle;

    // The mouse position in the world (shown in the corner of the canvas)
    public CanvasPoint PointerWorldPosition { get; private set; }

    // The device under the mouse, or null (drawn with a light highlight)
    public EntityId? HoveredItemId { get; private set; }

    // The cable under the mouse, or null (drawn with a light highlight)
    public EntityId? HoveredConnectionId { get; private set; }

    // The selection rectangle being drawn, or null
    public CanvasSelectionRectangle? SelectionRectangle { get; private set; }

    // Is Space held (the canvas shows a "hand" cursor)
    public bool IsSpaceHeld => _isSpaceHeld;

    // Raised after anything here changed, so the canvas redraws
    public event EventHandler? Changed;

    // Purpose:       The mouse wheel - zooms in or out around the mouse.
    // Precondition:  screenPoint is the mouse (from the canvas's top-left); wheelDeltaY > 0 = wheel up (zoom in).
    // Postcondition: the camera zoomed by 10% per wheel step, keeping the world point under the mouse in place.
    public void OnWheel(CanvasPoint screenPoint, double wheelDeltaY)
    {
        // No real wheel movement - nothing to do
        if (wheelDeltaY == 0)
        {
            return;   // stop here
        }

        // 1.1 to the power of the steps: +1 -> x1.1 (in), -1 -> x0.909 (out), +2 -> x1.21...
        var factor = Math.Pow(WheelZoomStep, wheelDeltaY);

        // The new camera: the zoom changed around the mouse
        _canvasState.SetViewport(_canvasState.Viewport.ZoomAround(screenPoint, _canvasState.Viewport.Zoom * factor));

        RaiseChanged();   // redraw
    }

    // Purpose:       A mouse button was pressed on the canvas.
    // Precondition:  screenPoint is measured from the canvas's top-left; the Connect tool's clicks and
    //                device placement are handled elsewhere (they never reach here).
    // Postcondition: a pan started, OR the selection was updated and a drag / rectangle may start on the next moves.
    public void OnPointerPressed(CanvasPoint screenPoint, CanvasPointerButton button, bool isCtrlHeld)
    {
        _pointerDownScreen = screenPoint;                                    // remember where (screen)
        _lastPointerScreen = screenPoint;                                    // the starting point for panning
        _pointerDownWorld = _canvasState.Viewport.ScreenToWorld(screenPoint); // the same place, in the world
        _pendingGesture = PendingGesture.None;                               // nothing planned yet

        // Panning: the middle button always; the left button when Space is held or the Pan tool is picked
        var isPanGesture = button == CanvasPointerButton.Middle
            || (button == CanvasPointerButton.Left && (_isSpaceHeld || _canvasState.CurrentTool == CanvasTool.Pan));

        if (isPanGesture)
        {
            _panStartedBySpace = button == CanvasPointerButton.Left && _isSpaceHeld;   // remember why, for when Space is let go
            Mode = CanvasInteractionMode.Panning;                                      // start panning
            RaiseChanged();                                                            // redraw (the cursor changes)
            return;                                                                    // stop here
        }

        // Only the left button selects (the right button has no job yet)
        if (button != CanvasPointerButton.Left)
        {
            return;   // stop here
        }

        // What was clicked: a device first (they are drawn on top of cables)
        var hitItem = _itemsState.HitTest(_pointerDownWorld);

        if (hitItem is not null)
        {
            // Ctrl + click: add it to the selection or take it out
            if (isCtrlHeld)
            {
                _selectionState.Toggle(hitItem.Id);
            }
            // A plain click on a device that is not selected: it becomes the only selected thing
            else if (!_selectionState.IsSelected(hitItem.Id))
            {
                _selectionState.Replace([hitItem.Id]);
            }
            // (A plain click on a device that IS already selected keeps the whole selection, so a
            // group of selected devices can be dragged together)

            // If the device is selected now, moving the mouse will drag the selected devices
            if (_selectionState.IsSelected(hitItem.Id))
            {
                _pendingGesture = PendingGesture.DragItems;

                // Remember where every selected device starts: the drag adds the mouse movement to these,
                // and Escape puts them back here
                _dragStartPositions = _itemsState.Items
                    .Where(i => _selectionState.IsSelected(i.Id))    // only the selected devices
                    .ToDictionary(i => i.Id, i => i.Position);       // id -> starting position
            }
        }
        // No device - maybe a cable?
        else if (HitTestCable(_pointerDownWorld) is { } connectionId)
        {
            // Ctrl + click: add / remove; a plain click: only this cable
            if (isCtrlHeld)
            {
                _selectionState.Toggle(connectionId);
            }
            else
            {
                _selectionState.Replace([connectionId]);
            }
        }
        // Empty canvas: a drag will draw a selection rectangle
        else
        {
            // With Ctrl the rectangle ADDS to what is selected; without it, it starts from nothing.
            // "[.. x]" = a new set with everything in x
            _rectBaseSelection = isCtrlHeld ? [.. _selectionState.SelectedIds] : [];

            // A plain click on empty canvas un-selects everything
            if (!isCtrlHeld)
            {
                _selectionState.Clear();
            }

            _pendingGesture = PendingGesture.SelectRect;   // moving will draw the rectangle
        }

        RaiseChanged();   // redraw (the selection may have changed)
    }

    // Purpose:       The mouse moved over the canvas.
    // Precondition:  screenPoint is measured from the canvas's top-left.
    // Postcondition: the pan / drag / rectangle went on, or a planned gesture started; the hovered device /
    //                cable and PointerWorldPosition are up to date.
    public void OnPointerMoved(CanvasPoint screenPoint)
    {
        // The world point under the mouse
        var world = _canvasState.Viewport.ScreenToWorld(screenPoint);
        PointerWorldPosition = world;

        // Already in the middle of something - go on with it
        switch (Mode)
        {
            case CanvasInteractionMode.Panning:
                ApplyPan(screenPoint);                 // scroll by how far the mouse moved
                _lastPointerScreen = screenPoint;      // the next move is measured from here
                RaiseChanged();
                return;

            case CanvasInteractionMode.DraggingItems:
                ApplyDrag(world);                      // move the selected devices
                RaiseChanged();
                return;

            case CanvasInteractionMode.SelectingRect:
                UpdateSelectionRect(world);            // grow the rectangle and select what it touches
                RaiseChanged();
                return;
        }

        // A press is waiting, and the mouse has now moved far enough - the drag / rectangle really starts
        if (_pendingGesture != PendingGesture.None && HasPassedDragThreshold(screenPoint))
        {
            if (_pendingGesture == PendingGesture.DragItems)
            {
                Mode = CanvasInteractionMode.DraggingItems;   // dragging devices now
                ApplyDrag(world);
            }
            else
            {
                Mode = CanvasInteractionMode.SelectingRect;   // drawing a rectangle now
                UpdateSelectionRect(world);
            }
        }

        // What is under the mouse (for the light highlight). A device wins over a cable behind it
        var hit = _itemsState.HitTest(world);
        HoveredItemId = hit?.Id;                                       // "?." = null when nothing was hit
        HoveredConnectionId = hit is null ? HitTestCable(world) : null; // a cable only when no device is there

        RaiseChanged();   // redraw
    }

    // Purpose:       A mouse button was let go.
    // Precondition:  screenPoint is measured from the canvas's top-left.
    // Postcondition: the pan / drag / rectangle (if any) ended; the controller is idle again.
    public void OnPointerReleased(CanvasPoint screenPoint, CanvasPointerButton button)
    {
        // Ending a pan (with the button that started it)
        if (Mode == CanvasInteractionMode.Panning)
        {
            if (button is CanvasPointerButton.Middle or CanvasPointerButton.Left)
            {
                Mode = CanvasInteractionMode.Idle;   // not panning any more
                _panStartedBySpace = false;
                RaiseChanged();
            }

            return;   // stop here
        }

        // Only the left button ends a drag / rectangle
        if (button != CanvasPointerButton.Left)
        {
            return;   // stop here
        }

        SelectionRectangle = null;                 // the rectangle (if any) disappears; what it selected stays selected
        Mode = CanvasInteractionMode.Idle;         // idle again
        _pendingGesture = PendingGesture.None;     // nothing planned
        _dragStartPositions = [];                  // the drag (if any) is finished - nothing to undo
        RaiseChanged();                            // redraw
    }

    // Purpose:       The mouse left the canvas - nothing is hovered any more.
    // Precondition:  none.
    // Postcondition: HoveredItemId and HoveredConnectionId are null.
    public void OnPointerExited()
    {
        // Nothing was hovered - nothing changes
        if (HoveredItemId is null && HoveredConnectionId is null)
        {
            return;   // stop here
        }

        HoveredItemId = null;          // no device under the mouse
        HoveredConnectionId = null;    // no cable under the mouse
        RaiseChanged();                // redraw without the highlight
    }

    // Purpose:       The Space key was pressed or let go (hold Space + drag = scroll, like in many drawing apps).
    // Precondition:  none.
    // Postcondition: IsSpaceHeld equals isHeld; letting go of Space ends a pan that Space started.
    public void SetSpaceHeld(bool isHeld)
    {
        // No change - nothing to do (the key repeats while held)
        if (_isSpaceHeld == isHeld)
        {
            return;   // stop here
        }

        _isSpaceHeld = isHeld;   // remember

        // Space let go in the middle of a Space-pan - the pan ends
        if (!isHeld && Mode == CanvasInteractionMode.Panning && _panStartedBySpace)
        {
            Mode = CanvasInteractionMode.Idle;
            _panStartedBySpace = false;
        }

        RaiseChanged();   // redraw (the cursor changes)
    }

    // Purpose:       The Escape key - cancels what is happening, or un-selects everything.
    // Precondition:  none.
    // Postcondition: a drag is undone (devices back where they started), a rectangle is undone (the old
    //                selection is back), a pan stops - or, when idle, nothing is selected.
    public void OnEscape()
    {
        switch (Mode)
        {
            case CanvasInteractionMode.DraggingItems:
                // Put every dragged device back where it started
                foreach (var (id, startPosition) in _dragStartPositions)
                {
                    MoveDevice(id, startPosition);
                }

                _dragStartPositions = [];
                break;

            case CanvasInteractionMode.SelectingRect:
                _selectionState.Replace(_rectBaseSelection);   // the selection from before the rectangle
                SelectionRectangle = null;                     // the rectangle disappears
                break;

            case CanvasInteractionMode.Panning:
                _panStartedBySpace = false;                    // just stop panning
                break;

            default:
                _selectionState.Clear();                       // idle: un-select everything
                break;
        }

        Mode = CanvasInteractionMode.Idle;          // idle again
        _pendingGesture = PendingGesture.None;      // nothing planned
        RaiseChanged();                             // redraw
    }

    // Purpose:       Ctrl + A - selects every device on the canvas.
    // Precondition:  none.
    // Postcondition: every device is selected (cables are not).
    public void OnSelectAll()
    {
        _selectionState.Replace(_itemsState.Items.Select(i => i.Id));   // all the device ids
        RaiseChanged();                                                 // redraw
    }

    // ----- Inside helpers -----

    // Purpose:       Scrolls the canvas by how far the mouse moved since the last move.
    // Precondition:  Mode is Panning.
    // Postcondition: the camera moved so the world follows the mouse.
    private void ApplyPan(CanvasPoint screenPoint)
    {
        var zoom = _canvasState.Viewport.Zoom;                        // the current zoom
        var deltaScreenX = screenPoint.X - _lastPointerScreen.X;      // how far the mouse moved, on X
        var deltaScreenY = screenPoint.Y - _lastPointerScreen.Y;      // and on Y

        // The world should follow the mouse, so the camera moves the OTHER way ("-"), and screen units are
        // turned into world units by dividing by the zoom
        _canvasState.Pan(new CanvasPoint(-deltaScreenX / zoom, -deltaScreenY / zoom));
    }

    // Purpose:       Moves every dragged device by how far the mouse moved since the press.
    // Precondition:  _dragStartPositions holds the dragged devices and their starting positions.
    // Postcondition: each device is at its start + the mouse movement (snapped to the grid when snap is on).
    private void ApplyDrag(CanvasPoint world)
    {
        // How far the mouse moved in the world since the press
        var deltaX = world.X - _pointerDownWorld.X;
        var deltaY = world.Y - _pointerDownWorld.Y;

        // Move each dragged device by the same distance
        foreach (var (id, startPosition) in _dragStartPositions)
        {
            // Where it should be now
            var newPosition = new CanvasPoint(startPosition.X + deltaX, startPosition.Y + deltaY);

            // With "snap to grid" on, jump to the nearest grid crossing
            if (_canvasState.IsSnapToGridEnabled)
            {
                newPosition = CanvasGridSnap.Snap(newPosition, _canvasState.GridSpacing);
            }

            // Move it - through DeviceService, so the project is marked "unsaved"
            MoveDevice(id, newPosition);
        }
    }

    // Purpose:       Moves one device through DeviceService (the one door for device changes).
    // Precondition:  none.
    // Postcondition: the device's item is at newPosition, or nothing happened if the device is gone.
    private void MoveDevice(EntityId deviceId, CanvasPoint newPosition)
    {
        // The device itself (null = no project / the device was removed)
        if (_applicationState.CurrentNetwork?.GetDevice(deviceId) is { } device)
        {
            _deviceService.MoveDevice(device, newPosition);   // move it on the canvas, mark "unsaved"
        }
    }

    // Purpose:       Updates the selection rectangle and selects everything it touches.
    // Precondition:  Mode is SelectingRect.
    // Postcondition: SelectionRectangle spans from the press to the mouse; the selection = the old one
    //                (with Ctrl) + every device the rectangle touches.
    private void UpdateSelectionRect(CanvasPoint world)
    {
        // The corners, whichever way the mouse was dragged: min = smaller X / Y, max = bigger X / Y
        var min = new CanvasPoint(Math.Min(_pointerDownWorld.X, world.X), Math.Min(_pointerDownWorld.Y, world.Y));
        var max = new CanvasPoint(Math.Max(_pointerDownWorld.X, world.X), Math.Max(_pointerDownWorld.Y, world.Y));
        SelectionRectangle = new CanvasSelectionRectangle(min, max);

        // Start from what was selected before the rectangle, and add every device it touches
        var combined = new HashSet<EntityId>(_rectBaseSelection);
        combined.UnionWith(_itemsState.Items.Where(i => i.Intersects(min, max)).Select(i => i.Id));

        _selectionState.Replace(combined);   // that is the selection now
    }

    // Purpose:       Answers whether the mouse moved far enough from the press to count as a drag.
    // Precondition:  none.
    // Postcondition: true when it moved at least 4 screen units. Nothing is changed.
    private bool HasPassedDragThreshold(CanvasPoint screenPoint)
    {
        var dx = screenPoint.X - _pointerDownScreen.X;          // movement on X
        var dy = screenPoint.Y - _pointerDownScreen.Y;          // movement on Y
        return dx * dx + dy * dy >= DragThresholdSquared;       // compared squared - no square root needed
    }

    // Purpose:       Finds the cable under a world point.
    // Precondition:  none.
    // Postcondition: returns the cable's id, or null. Nothing is changed.
    private EntityId? HitTestCable(CanvasPoint world)
    {
        // The lines of all the cables right now
        var cables = CanvasConnectionGeometry.Build(_applicationState.CurrentNetwork, _itemsState);

        // 6 screen units, turned into world units - so it feels the same at every zoom
        return CanvasConnectionGeometry.HitTest(cables, world, CableHitScreenRadius / _canvasState.Viewport.Zoom);
    }

    // Purpose:       Tells the listeners that something changed.
    // Precondition:  none.
    // Postcondition: Changed was raised (if anyone listens).
    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
