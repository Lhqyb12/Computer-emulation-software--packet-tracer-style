using System;                      // brings in EventHandler, EventArgs, Math (Pow, Min, Max), ArgumentNullException
using System.Collections.Generic;  // brings in Dictionary<TKey, TValue> (id -> start position) and HashSet<T> (a set of ids)
using System.Linq;                 // brings in Where (keep items that pass a test), Select (turn each item into something), ToDictionary
using NetSim.Application.Services; // brings in IDeviceService - the one door for moving devices
using NetSim.Application.State;    // brings in IApplicationState - the open network
using NetSim.Core.Common;          // brings in EntityId - the unique id of a device or a cable

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// The mouse logic of the Select and Pan tools (steps 4-5): what a click, a drag, the wheel and a few keys
// MEAN on the canvas. It gets plain calls ("pressed at this point, with this button") from the canvas control,
// decides, and changes the states (selection, camera) or moves devices through DeviceService.
// It knows nothing about Avalonia, so all of it can be tested without a screen.
//   - Click a device           -> select it (Ctrl + click adds / removes it).
//   - Drag a selected device   -> move all the selected devices together (snapping to the grid if on).
//   - Click a cable            -> select it.
//   - Drag on empty canvas     -> draw a rectangle; everything it touches gets selected.
//   - Wheel                    -> zoom around the mouse.
//   - Middle button, Space+drag, or the Pan tool -> scroll the canvas.
// Changes from the reference project: devices are moved through DeviceService.MoveDevice (so a move marks the
// project "unsaved" - step 2's rule), and cables are found with CanvasConnectionGeometry.
// "sealed" = no other class can inherit from it
public sealed class CanvasInteractionController
{
    // One wheel "click" zooms by 10% (x 1.1). "const" = a fixed number that never changes
    private const double WheelZoomStep = 1.1;

    // The mouse must move 4 screen units before a press becomes a drag. 4 * 4 = 16: we compare SQUARED distances,
    // which saves a square root. Without this, a tiny shake of the hand during a click would move the device a little
    private const double DragThresholdSquared = 4.0 * 4.0;

    // How close to a cable a click must be to pick it: 6 screen units
    private const double CableHitScreenRadius = 6.0;

    // What a left press MAY turn into once the mouse moves far enough. "private enum" = a closed list used only inside this class
    private enum PendingGesture
    {
        None,         // nothing is planned
        DragItems,    // pressed on a selected device - moving will drag the selection
        SelectRect,   // pressed on empty canvas - moving will draw a selection rectangle
    }

    // The things this controller works with. "readonly" = set once in the constructor, never replaced
    private readonly ICanvasState _canvasState;               // the camera, grid and tool
    private readonly ICanvasItemsState _itemsState;           // where every device is
    private readonly ICanvasSelectionState _selectionState;   // what is selected
    private readonly IDeviceService _deviceService;           // moves devices (and marks "unsaved")
    private readonly IApplicationState _applicationState;     // the open network (to find devices and cables)

    // What the controller remembers between mouse events
    private PendingGesture _pendingGesture = PendingGesture.None;          // what the current press may become (nothing yet)
    private CanvasPoint _pointerDownScreen;                                 // where the button was pressed, in screen units
    private CanvasPoint _pointerDownWorld;                                  // the same place, in the world
    private CanvasPoint _lastPointerScreen;                                 // the last mouse position (panning measures from it)
    private Dictionary<EntityId, CanvasPoint> _dragStartPositions = [];    // device id -> where it was when the drag started (to undo with Escape)
    private HashSet<EntityId> _rectBaseSelection = [];                      // what was selected before the rectangle started (kept when Ctrl is held)
    private bool _isSpaceHeld;                                              // is the Space key held down right now
    private bool _panStartedBySpace;                                        // did the current pan start because Space was held

    // Purpose:       Builds the controller.
    // Precondition:  receives canvasState, itemsState, selectionState, deviceService, applicationState -
    //                the five things it works with (none may be null).
    // Postcondition: returns nothing (a constructor) - the controller is idle (nothing pressed, nothing hovered).
    public CanvasInteractionController(
        ICanvasState canvasState,
        ICanvasItemsState itemsState,
        ICanvasSelectionState selectionState,
        IDeviceService deviceService,
        IApplicationState applicationState)
    {
        // If any of the five is missing (null), stop right away with a clear error
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

    // What the mouse is doing right now (Idle / Panning / DraggingItems / SelectingRect). "private set" = only this class changes it
    public CanvasInteractionMode Mode { get; private set; } = CanvasInteractionMode.Idle;

    // The mouse position in the world (shown in the corner of the canvas)
    public CanvasPoint PointerWorldPosition { get; private set; }

    // The device under the mouse, or null (the canvas draws it with a light highlight). "EntityId?" = an id OR null
    public EntityId? HoveredItemId { get; private set; }

    // The cable under the mouse, or null (drawn with a light highlight)
    public EntityId? HoveredConnectionId { get; private set; }

    // The selection rectangle being drawn right now, or null when none
    public CanvasSelectionRectangle? SelectionRectangle { get; private set; }

    // Is Space held (the canvas shows a "hand" cursor). "=>" = computed on every read
    public bool IsSpaceHeld => _isSpaceHeld;

    // Raised after anything here changed, so the canvas redraws
    public event EventHandler? Changed;

    // Purpose:       The mouse wheel - zooms in or out around the mouse.
    // Precondition:  receives screenPoint - the mouse, from the canvas's top-left corner;
    //                wheelDeltaY - how many wheel steps (above 0 = wheel up = zoom in; below 0 = zoom out).
    // Postcondition: returns nothing - the camera zoomed by 10% per step, keeping the world point under the mouse in place.
    public void OnWheel(CanvasPoint screenPoint, double wheelDeltaY)
    {
        // No real wheel movement - nothing to do
        if (wheelDeltaY == 0)
        {
            return;   // stop here
        }

        // Math.Pow(1.1, steps) = 1.1 multiplied by itself "steps" times: +1 -> x1.1 (in), -1 -> x0.909 (out), +2 -> x1.21
        var factor = Math.Pow(WheelZoomStep, wheelDeltaY);

        // The new camera: the current zoom times the factor, "around" the mouse (CanvasViewport.ZoomAround), and store it
        _canvasState.SetViewport(_canvasState.Viewport.ZoomAround(screenPoint, _canvasState.Viewport.Zoom * factor));

        RaiseChanged();   // tell the listeners (the canvas redraws)
    }

    // Purpose:       A mouse button was pressed on the canvas.
    // Precondition:  receives screenPoint - where, from the canvas's top-left corner; button - which button;
    //                isCtrlHeld - is Ctrl held. (Placing a device and the Connect tool's clicks never reach here.)
    // Postcondition: returns nothing - a pan started, OR the selection was updated and a drag / rectangle may start
    //                on the next moves.
    public void OnPointerPressed(CanvasPoint screenPoint, CanvasPointerButton button, bool isCtrlHeld)
    {
        _pointerDownScreen = screenPoint;                                     // remember where the press was (screen)
        _lastPointerScreen = screenPoint;                                     // panning measures its first move from here
        _pointerDownWorld = _canvasState.Viewport.ScreenToWorld(screenPoint); // the same place in the WORLD (through the camera)
        _pendingGesture = PendingGesture.None;                                // nothing planned yet

        // Is this press a pan? The middle button always; the left button when Space is held OR the Pan tool is picked.
        // "||" = OR, "&&" = AND, the brackets decide what goes together
        var isPanGesture = button == CanvasPointerButton.Middle
            || (button == CanvasPointerButton.Left && (_isSpaceHeld || _canvasState.CurrentTool == CanvasTool.Pan));

        if (isPanGesture)
        {
            // Remember WHY we pan - if Space started it, letting go of Space must end it (SetSpaceHeld)
            _panStartedBySpace = button == CanvasPointerButton.Left && _isSpaceHeld;
            Mode = CanvasInteractionMode.Panning;   // panning from now on
            RaiseChanged();                         // redraw (the cursor changes)
            return;                                 // stop here - a pan selects nothing
        }

        // Only the left button selects (the right button has no job yet)
        if (button != CanvasPointerButton.Left)
        {
            return;   // stop here
        }

        // What was clicked? A device first - devices are drawn ON TOP of cables (CanvasItemsState.HitTest)
        var hitItem = _itemsState.HitTest(_pointerDownWorld);

        if (hitItem is not null)
        {
            // Ctrl + click: add the device to the selection, or take it out if it was in
            if (isCtrlHeld)
            {
                _selectionState.Toggle(hitItem.Id);
            }
            // A plain click on a device that is NOT selected: it becomes the ONLY selected thing ("[x]" = a list with one id)
            else if (!_selectionState.IsSelected(hitItem.Id))
            {
                _selectionState.Replace([hitItem.Id]);
            }
            // (A plain click on a device that IS already selected keeps the whole selection, so a group of selected
            // devices can be dragged together)

            // If the device is selected now, moving the mouse will drag ALL the selected devices
            if (_selectionState.IsSelected(hitItem.Id))
            {
                _pendingGesture = PendingGesture.DragItems;   // a drag is planned

                // Remember where every selected device starts: the drag adds the mouse movement to these, and Escape
                // puts them back here. Where = keep only selected items; ToDictionary = id -> its position
                _dragStartPositions = _itemsState.Items
                    .Where(i => _selectionState.IsSelected(i.Id))
                    .ToDictionary(i => i.Id, i => i.Position);
            }
        }
        // No device - maybe a cable? HitTestCable (below) returns its id or null; "is { } connectionId" = if not null, keep it
        else if (HitTestCable(_pointerDownWorld) is { } connectionId)
        {
            // Ctrl + click: add / remove the cable; a plain click: only this cable is selected
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
            // With Ctrl the rectangle ADDS to what is already selected; without Ctrl it starts from nothing.
            // "[.. x]" = a new set holding everything that is in x
            _rectBaseSelection = isCtrlHeld ? [.. _selectionState.SelectedIds] : [];

            // A plain click on empty canvas un-selects everything
            if (!isCtrlHeld)
            {
                _selectionState.Clear();
            }

            _pendingGesture = PendingGesture.SelectRect;   // moving the mouse will draw the rectangle
        }

        RaiseChanged();   // redraw (the selection may have changed)
    }

    // Purpose:       The mouse moved over the canvas.
    // Precondition:  receives screenPoint - the mouse, from the canvas's top-left corner.
    // Postcondition: returns nothing - the pan / drag / rectangle went on, or a planned gesture started; the hovered
    //                device / cable and PointerWorldPosition are up to date.
    public void OnPointerMoved(CanvasPoint screenPoint)
    {
        // The world point under the mouse (through the camera), kept for the coordinates box
        var world = _canvasState.Viewport.ScreenToWorld(screenPoint);
        PointerWorldPosition = world;

        // Already in the middle of something? "switch" picks the "case" that matches Mode
        switch (Mode)
        {
            case CanvasInteractionMode.Panning:
                ApplyPan(screenPoint);                 // scroll by how far the mouse moved since the last move
                _lastPointerScreen = screenPoint;      // the next move is measured from here
                RaiseChanged();                        // redraw
                return;                                // done with this move

            case CanvasInteractionMode.DraggingItems:
                ApplyDrag(world);                      // move the selected devices with the mouse
                RaiseChanged();                        // redraw
                return;                                // done with this move

            case CanvasInteractionMode.SelectingRect:
                UpdateSelectionRect(world);            // grow the rectangle and select what it touches
                RaiseChanged();                        // redraw
                return;                                // done with this move
        }

        // A press is waiting, and the mouse has now moved far enough - the drag / rectangle really starts
        if (_pendingGesture != PendingGesture.None && HasPassedDragThreshold(screenPoint))
        {
            if (_pendingGesture == PendingGesture.DragItems)
            {
                Mode = CanvasInteractionMode.DraggingItems;   // dragging devices from now on
                ApplyDrag(world);                             // and move them already on this move
            }
            else
            {
                Mode = CanvasInteractionMode.SelectingRect;   // drawing a rectangle from now on
                UpdateSelectionRect(world);                   // and draw it already on this move
            }
        }

        // What is under the mouse (for the light highlight)? A device wins over a cable behind it
        var hit = _itemsState.HitTest(world);
        HoveredItemId = hit?.Id;                                        // "?." = if hit is null, the whole thing is null
        HoveredConnectionId = hit is null ? HitTestCable(world) : null; // look for a cable only when no device is there

        RaiseChanged();   // redraw
    }

    // Purpose:       A mouse button was let go.
    // Precondition:  receives screenPoint - where; button - which button was let go.
    // Postcondition: returns nothing - the pan / drag / rectangle (if any) ended; the controller is idle again.
    public void OnPointerReleased(CanvasPoint screenPoint, CanvasPointerButton button)
    {
        // Ending a pan - with the middle or the left button ("is X or Y" = one of the two)
        if (Mode == CanvasInteractionMode.Panning)
        {
            if (button is CanvasPointerButton.Middle or CanvasPointerButton.Left)
            {
                Mode = CanvasInteractionMode.Idle;   // not panning any more
                _panStartedBySpace = false;          // forget why it started
                RaiseChanged();                      // redraw (the cursor changes back)
            }

            return;   // stop here
        }

        // Only the left button ends a drag / rectangle
        if (button != CanvasPointerButton.Left)
        {
            return;   // stop here
        }

        SelectionRectangle = null;                 // the rectangle (if any) disappears; what it selected STAYS selected
        Mode = CanvasInteractionMode.Idle;         // idle again
        _pendingGesture = PendingGesture.None;     // nothing planned
        _dragStartPositions = [];                  // the drag (if any) is finished - nothing to undo any more
        RaiseChanged();                            // redraw
    }

    // Purpose:       The mouse left the canvas - nothing is hovered any more.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - HoveredItemId and HoveredConnectionId are null.
    public void OnPointerExited()
    {
        // Nothing was hovered - nothing changes, so don't redraw for nothing
        if (HoveredItemId is null && HoveredConnectionId is null)
        {
            return;   // stop here
        }

        HoveredItemId = null;          // no device under the mouse
        HoveredConnectionId = null;    // no cable under the mouse
        RaiseChanged();                // redraw without the highlight
    }

    // Purpose:       The Space key was pressed or let go (hold Space + drag = scroll, like in many drawing apps).
    // Precondition:  receives isHeld - true = Space is now down, false = it was let go.
    // Postcondition: returns nothing - IsSpaceHeld equals isHeld; letting go of Space ends a pan that Space started.
    public void SetSpaceHeld(bool isHeld)
    {
        // No change - nothing to do (a held key repeats, so this is called many times)
        if (_isSpaceHeld == isHeld)
        {
            return;   // stop here
        }

        _isSpaceHeld = isHeld;   // remember the new state

        // Space let go in the middle of a pan that Space started - the pan ends
        if (!isHeld && Mode == CanvasInteractionMode.Panning && _panStartedBySpace)
        {
            Mode = CanvasInteractionMode.Idle;   // not panning any more
            _panStartedBySpace = false;          // forget why it started
        }

        RaiseChanged();   // redraw (the cursor changes)
    }

    // Purpose:       The Escape key - cancels what is happening, or un-selects everything.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - a drag is undone (devices back where they started), a rectangle is undone
    //                (the old selection is back), a pan stops - or, when idle, nothing is selected.
    public void OnEscape()
    {
        // Pick what to undo by what is happening now
        switch (Mode)
        {
            case CanvasInteractionMode.DraggingItems:
                // Put every dragged device back where it started. "var (id, startPosition)" = take each pair apart into two variables
                foreach (var (id, startPosition) in _dragStartPositions)
                {
                    MoveDevice(id, startPosition);   // back to its start (through DeviceService)
                }

                _dragStartPositions = [];   // nothing left to undo
                break;                      // leave the switch

            case CanvasInteractionMode.SelectingRect:
                _selectionState.Replace(_rectBaseSelection);   // the selection from before the rectangle comes back
                SelectionRectangle = null;                     // the rectangle disappears
                break;

            case CanvasInteractionMode.Panning:
                _panStartedBySpace = false;                    // just stop panning
                break;

            default:
                _selectionState.Clear();                       // idle ("default" = any other mode): un-select everything
                break;
        }

        Mode = CanvasInteractionMode.Idle;          // idle again
        _pendingGesture = PendingGesture.None;      // nothing planned
        RaiseChanged();                             // redraw
    }

    // Purpose:       Ctrl + A - selects every device on the canvas.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - every device is selected (cables are not).
    public void OnSelectAll()
    {
        // Select(i => i.Id) = turn every item into its id -> the list of all device ids, which becomes the selection
        _selectionState.Replace(_itemsState.Items.Select(i => i.Id));
        RaiseChanged();   // redraw
    }

    // ----- Inside helpers -----

    // Purpose:       Scrolls the canvas by how far the mouse moved since the last move.
    // Precondition:  receives screenPoint - the mouse now (Mode is Panning).
    // Postcondition: returns nothing - the camera moved so the world follows the mouse.
    private void ApplyPan(CanvasPoint screenPoint)
    {
        var zoom = _canvasState.Viewport.Zoom;                        // the current zoom
        var deltaScreenX = screenPoint.X - _lastPointerScreen.X;      // how far the mouse moved on X since the last move (screen units)
        var deltaScreenY = screenPoint.Y - _lastPointerScreen.Y;      // and on Y

        // The world should follow the mouse, so the camera moves the OTHER way ("-").
        // Dividing by the zoom turns screen units into world units (at 200%, 10 screen units = 5 world units)
        _canvasState.Pan(new CanvasPoint(-deltaScreenX / zoom, -deltaScreenY / zoom));
    }

    // Purpose:       Moves every dragged device by how far the mouse moved since the press.
    // Precondition:  receives world - the mouse now, in the world (_dragStartPositions holds the dragged devices).
    // Postcondition: returns nothing - each device is at its start + the mouse movement (snapped when snap is on).
    private void ApplyDrag(CanvasPoint world)
    {
        // How far the mouse moved in the world since the press
        var deltaX = world.X - _pointerDownWorld.X;
        var deltaY = world.Y - _pointerDownWorld.Y;

        // Move EACH dragged device by the same distance from where IT started
        foreach (var (id, startPosition) in _dragStartPositions)
        {
            // Where it should be now: its start + the movement
            var newPosition = new CanvasPoint(startPosition.X + deltaX, startPosition.Y + deltaY);

            // With "snap to grid" on, jump to the nearest grid crossing (CanvasGridSnap.Snap)
            if (_canvasState.IsSnapToGridEnabled)
            {
                newPosition = CanvasGridSnap.Snap(newPosition, _canvasState.GridSpacing);
            }

            // Move it - through DeviceService, so the project is marked "unsaved"
            MoveDevice(id, newPosition);
        }
    }

    // Purpose:       Moves one device through DeviceService (the one door for device changes).
    // Precondition:  receives deviceId - which device; newPosition - its new top-left corner (world).
    // Postcondition: returns nothing - the device's item is at newPosition, or nothing happened if the device is gone.
    private void MoveDevice(EntityId deviceId, CanvasPoint newPosition)
    {
        // Find the device itself: "?." = if there is no open network the result is null; GetDevice returns null if the
        // device is gone. "is { } device" = only when a device was found, keep it in "device"
        if (_applicationState.CurrentNetwork?.GetDevice(deviceId) is { } device)
        {
            _deviceService.MoveDevice(device, newPosition);   // move it on the canvas and mark "unsaved"
        }
    }

    // Purpose:       Updates the selection rectangle and selects everything it touches.
    // Precondition:  receives world - the mouse now, in the world (Mode is SelectingRect).
    // Postcondition: returns nothing - SelectionRectangle spans from the press to the mouse; the selection = the old
    //                one (only with Ctrl) + every device the rectangle touches.
    private void UpdateSelectionRect(CanvasPoint world)
    {
        // The corners, whichever way the mouse was dragged: min = the smaller X and Y, max = the bigger X and Y
        var min = new CanvasPoint(Math.Min(_pointerDownWorld.X, world.X), Math.Min(_pointerDownWorld.Y, world.Y));
        var max = new CanvasPoint(Math.Max(_pointerDownWorld.X, world.X), Math.Max(_pointerDownWorld.Y, world.Y));
        SelectionRectangle = new CanvasSelectionRectangle(min, max);   // the rectangle the canvas draws

        // Start from what was selected before the rectangle (empty without Ctrl)...
        var combined = new HashSet<EntityId>(_rectBaseSelection);

        // ...and add every device whose box touches the rectangle (CanvasItem.Intersects). UnionWith = add them all
        combined.UnionWith(_itemsState.Items.Where(i => i.Intersects(min, max)).Select(i => i.Id));

        _selectionState.Replace(combined);   // that is the selection now
    }

    // Purpose:       Answers whether the mouse moved far enough from the press to count as a drag.
    // Precondition:  receives screenPoint - the mouse now.
    // Postcondition: returns true when it moved at least 4 screen units from the press, otherwise false.
    private bool HasPassedDragThreshold(CanvasPoint screenPoint)
    {
        var dx = screenPoint.X - _pointerDownScreen.X;          // how far on X from the press
        var dy = screenPoint.Y - _pointerDownScreen.Y;          // how far on Y from the press
        return dx * dx + dy * dy >= DragThresholdSquared;       // dx² + dy² >= 16 (= distance >= 4), without a square root
    }

    // Purpose:       Finds the cable under a world point.
    // Precondition:  receives world - the point, in the world.
    // Postcondition: returns the cable's id, or null when no cable is close enough.
    private EntityId? HitTestCable(CanvasPoint world)
    {
        // The lines of all the cables right now (CanvasConnectionGeometry.Build)
        var cables = CanvasConnectionGeometry.Build(_applicationState.CurrentNetwork, _itemsState);

        // 6 SCREEN units turned into world units (divided by the zoom) - so a click feels the same at every zoom
        return CanvasConnectionGeometry.HitTest(cables, world, CableHitScreenRadius / _canvasState.Viewport.Zoom);
    }

    // Purpose:       Tells the listeners that something changed.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - Changed was raised (if anyone listens; "?." = only then).
    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
