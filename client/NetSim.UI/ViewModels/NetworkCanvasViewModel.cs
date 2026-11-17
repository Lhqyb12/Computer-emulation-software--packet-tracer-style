using System;                              // EventHandler, EventArgs, ArgumentNullException
using System.Collections.Generic;          // IReadOnlyList<T>
using System.Linq;                         // Min, Max, ToList
using CommunityToolkit.Mvvm.Input;         // [RelayCommand] - turns a method into a Command a button can call
using NetSim.Application.Canvas;           // the canvas states, controllers, CanvasPoint, CanvasViewport...
using NetSim.Application.Services;         // IDeviceService, IConnectionService
using NetSim.Application.State;            // IApplicationState - the open network
using NetSim.Core.Common;                  // EntityId
using NetSim.Core.Devices;                 // DeviceType
using NetSim.UI.Common;                    // ViewModelBase - IsBusy, ErrorMessage

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The Network Canvas, as the screen sees it: what to draw (devices, cables, the selection, the grid, the
// ghost of the device being placed, the cable being made) and the toolbar's commands.
// It is the go-between of the canvas control (NetSim.UI/Controls/NetworkCanvasControl - pixels and mouse)
// and the Application layer (the states, the two mouse controllers and the services - the rules).
//   Step 3: placing a device.   Step 4: selecting, moving, deleting.
//   Step 5: zoom, scroll, grid and the toolbar.   Step 6: the Connect tool.
// Changes from the reference project: placing is one call to DeviceService.AddDevice; Delete removes
// selected devices too (the reference removed only cables); the Connect tool works device-to-device.
// "sealed" = no other class can inherit from it; "partial" = CommunityToolkit writes a second half (the commands)
public sealed partial class NetworkCanvasViewModel : ViewModelBase
{
    // Each toolbar zoom click changes the zoom by 25%
    private const double ZoomStep = 1.25;

    private readonly IDeviceService _deviceService;             // adds / moves / removes devices
    private readonly IConnectionService _connectionService;     // removes cables
    private readonly ICanvasItemsState _itemsState;             // where every device is
    private readonly IDevicePlacementState _placementState;     // the device kind waiting to be placed
    private readonly ICanvasState _canvasState;                 // the camera, grid and tool
    private readonly ICanvasSelectionState _selectionState;     // what is selected
    private readonly IApplicationState _applicationState;       // the open network

    // The canvas's size on the screen, as last reported by the control (needed for "Fit to View")
    private double _viewportWidth;
    private double _viewportHeight;

    // Purpose:       Builds the canvas view model, its two mouse controllers, and starts listening to every source of change.
    // Precondition:  all seven are not null (the DI container passes them in).
    // Postcondition: the view model is ready and listens to the states until Detach() is called.
    public NetworkCanvasViewModel(
        IDeviceService deviceService,
        IConnectionService connectionService,
        ICanvasItemsState itemsState,
        IDevicePlacementState placementState,
        ICanvasState canvasState,
        ICanvasSelectionState selectionState,
        IApplicationState applicationState)
    {
        // Stop right away with a clear message if something was not passed in
        ArgumentNullException.ThrowIfNull(deviceService);
        ArgumentNullException.ThrowIfNull(connectionService);
        ArgumentNullException.ThrowIfNull(itemsState);
        ArgumentNullException.ThrowIfNull(placementState);
        ArgumentNullException.ThrowIfNull(canvasState);
        ArgumentNullException.ThrowIfNull(selectionState);
        ArgumentNullException.ThrowIfNull(applicationState);

        _deviceService = deviceService;           // keep each one
        _connectionService = connectionService;
        _itemsState = itemsState;
        _placementState = placementState;
        _canvasState = canvasState;
        _selectionState = selectionState;
        _applicationState = applicationState;

        // The two mouse controllers (Application layer). They belong to this view model only, so they are
        // built here and not taken from the DI container
        Interaction = new CanvasInteractionController(canvasState, itemsState, selectionState, deviceService, applicationState);
        ConnectionTool = new ConnectionToolController(applicationState, itemsState, connectionService);

        // Our own controllers - they die with this view model, so no Detach is needed for them
        Interaction.Changed += (_, _) => OnInteractionChanged();
        ConnectionTool.Changed += (_, _) => OnConnectionToolChanged();

        // The shared states - they live for the whole sign-in, so these are removed again in Detach()
        _itemsState.ItemsChanged += OnItemsChanged;                       // a device was added / moved / removed
        _placementState.Changed += OnPlacementChanged;                    // a device kind was picked / cancelled
        _canvasState.ViewportChanged += OnViewportChanged;                // zoom / scroll
        _canvasState.GridSettingsChanged += OnGridSettingsChanged;        // grid shown / snap
        _canvasState.CurrentToolChanged += OnCurrentToolChanged;          // another tool
        _selectionState.SelectionChanged += OnSelectionChanged;           // the selection
        _connectionService.ConnectionsChanged += OnConnectionsChanged;    // a cable was added / removed
    }

    // Raised every time the canvas must be drawn again. The canvas control listens to it
    public event EventHandler? RedrawRequested;

    // The mouse logic of the Select / Pan tools (step 4-5). The control calls it
    public CanvasInteractionController Interaction { get; }

    // The Connect tool (step 6). The control calls it while the Connect tool is on
    public ConnectionToolController ConnectionTool { get; }

    // ----- What to draw -----

    // All the devices, in drawing order (the last one is drawn on top)
    public IReadOnlyList<CanvasItem> Items => _itemsState.Items;

    // All the cables as lines - worked out again on every read, so they always follow the devices
    public IReadOnlyList<CanvasConnectionView> Connections => CanvasConnectionGeometry.Build(_applicationState.CurrentNetwork, _itemsState);

    // The camera (scroll + zoom)
    public CanvasViewport Viewport => _canvasState.Viewport;

    // The grid: shown? the distance between lines? snap on?
    public bool IsGridVisible => _canvasState.IsGridVisible;
    public double GridSpacing => _canvasState.GridSpacing;
    public bool IsSnapToGridEnabled => _canvasState.IsSnapToGridEnabled;

    // The tool picked in the toolbar, and three yes/no versions of it for the toolbar's toggle buttons
    public CanvasTool CurrentTool => _canvasState.CurrentTool;
    public bool IsSelectToolActive => CurrentTool == CanvasTool.Select;
    public bool IsPanToolActive => CurrentTool == CanvasTool.Pan;
    public bool IsConnectToolActive => CurrentTool == CanvasTool.Connect;

    // The zoom as text for the toolbar, e.g. "125%". ":0" = no digits after the point
    public string ZoomPercentText => $"{Viewport.Zoom * 100:0}%";

    // The world point under the mouse, for the small box in the corner, e.g. "X: 240   Y: 120"
    public string PointerCoordinatesText => $"X: {Interaction.PointerWorldPosition.X:0}   Y: {Interaction.PointerWorldPosition.Y:0}";

    // Is anything selected (the Delete button is enabled only then)
    public bool HasSelection => _selectionState.SelectedIds.Count > 0;

    // true when there is no device on the canvas
    public bool IsEmpty => _itemsState.Items.Count == 0;

    // Show the "Your network is empty" hint? Only with no devices and nothing being placed
    public bool ShowEmptyHint => IsEmpty && !IsPlacingDevice;

    // The device kind waiting to be placed (its ghost follows the mouse), or null
    public DeviceType? PendingPlacementDeviceType => _placementState.PendingDeviceType;

    // true while a device kind is waiting to be placed
    public bool IsPlacingDevice => _placementState.IsPlacing;

    // A hint for the user (not an error), shown in a blue box: the Connect tool's next step or its success.
    // "a ?? b" = a, or b when a is null
    public string? HintMessage =>
        ConnectionTool.StatusMessage is { } message && !ConnectionTool.IsStatusError
            ? message                                                                  // the tool's own hint / success
            : IsConnectToolActive && ConnectionTool.StatusMessage is null
                ? "Connect tool: click a device, then a second device, to cable them."  // how to start
                : null;                                                                 // no hint

    // Purpose:       Answers whether a device or a cable is selected (the control draws it highlighted).
    // Precondition:  none.
    // Postcondition: returns true / false. Nothing is changed.
    public bool IsSelected(EntityId id) => _selectionState.IsSelected(id);

    // ----- Placing a device (step 3) -----

    // Purpose:       Places the waiting device so its middle is at the point the user chose.
    // Precondition:  worldPoint is a WORLD point (the control already turned the mouse into one).
    // Postcondition: if a device was waiting - it was added (snapped to the grid when snap is on) and is now
    //                the only selected thing, or ErrorMessage says why not; "place a device" mode is over.
    //                If nothing was waiting - nothing changed.
    public void ConfirmPlacement(CanvasPoint worldPoint)
    {
        // Nothing is waiting to be placed - nothing to do
        if (_placementState.PendingDeviceType is not { } deviceType)
        {
            return;   // stop here
        }

        // With snap on, line the device's top-left corner up with the grid (the same rule as dragging):
        // top-left = middle - half the size -> snap it -> back to the middle
        var center = worldPoint;
        if (_canvasState.IsSnapToGridEnabled)
        {
            var size = CanvasItem.DefaultDeviceSize;                                                       // 96 x 72
            var topLeft = new CanvasPoint(worldPoint.X - size.Width / 2, worldPoint.Y - size.Height / 2);  // the corner
            var snapped = CanvasGridSnap.Snap(topLeft, _canvasState.GridSpacing);                          // on the grid
            center = new CanvasPoint(snapped.X + size.Width / 2, snapped.Y + size.Height / 2);             // back to the middle
        }

        // Add the device (network + canvas, with a free name) - step 2's one door
        var result = _deviceService.AddDevice(deviceType, center);

        // Show the reason when it did not work, or clear an old error when it did
        ErrorMessage = result.IsSuccess ? null : result.ErrorMessage;

        // The new device becomes the selected one, so it can be moved or deleted right away (step 4)
        if (result.IsSuccess && result.Value is { } device)
        {
            _selectionState.Replace([device.Id]);
        }

        // One pick places one device
        _placementState.Cancel();
    }

    // Purpose:       Leaves "place a device" mode without placing anything (Escape).
    // Precondition:  none.
    // Postcondition: nothing is waiting to be placed.
    public void CancelPlacement() => _placementState.Cancel();

    // Purpose:       Remembers the canvas's size on the screen (the control calls it when its size changes).
    // Precondition:  width and height are the canvas size in screen units.
    // Postcondition: "Fit to View" will use this size.
    public void SetViewportSize(double width, double height)
    {
        _viewportWidth = width;     // remember the width
        _viewportHeight = height;   // and the height
    }

    // ----- Toolbar commands (step 5) -----

    // Purpose:       Zoom in by 25% (around the top-left corner).
    // Precondition:  none.
    // Postcondition: the zoom grew by 25% (up to 400%).
    [RelayCommand]
    private void ZoomIn() => _canvasState.SetZoom(_canvasState.Viewport.Zoom * ZoomStep);

    // Purpose:       Zoom out by 25%.
    // Precondition:  none.
    // Postcondition: the zoom shrank by 25% (down to 25%).
    [RelayCommand]
    private void ZoomOut() => _canvasState.SetZoom(_canvasState.Viewport.Zoom / ZoomStep);

    // Purpose:       Back to the starting view: world (0, 0) at the top-left corner, 100%.
    // Precondition:  none.
    // Postcondition: the camera is the default one.
    [RelayCommand]
    private void ResetView() => _canvasState.ResetViewport();

    // Purpose:       Zooms and scrolls so that every device is in view ("Fit to View").
    // Precondition:  none.
    // Postcondition: all devices are visible and centered; with no devices (or no known size) - the default view.
    [RelayCommand]
    private void FitToView()
    {
        var items = _itemsState.Items;   // all the devices

        // Nothing to fit, or the canvas size is not known yet - just go back to the default view
        if (items.Count == 0 || _viewportWidth <= 0 || _viewportHeight <= 0)
        {
            _canvasState.ResetViewport();
            return;   // stop here
        }

        // The rectangle around all the devices: the smallest left / top and the biggest right / bottom
        var min = new CanvasPoint(items.Min(i => i.Position.X), items.Min(i => i.Position.Y));
        var max = new CanvasPoint(items.Max(i => i.Position.X + i.Size.Width), items.Max(i => i.Position.Y + i.Size.Height));

        // A camera that shows exactly that rectangle (with some room around it)
        _canvasState.SetViewport(CanvasViewport.Fit(min, max, _viewportWidth, _viewportHeight));
    }

    // Purpose:       Shows / hides the grid.
    // Precondition:  none.
    // Postcondition: IsGridVisible flipped.
    [RelayCommand]
    private void ToggleGrid() => _canvasState.SetGridVisible(!_canvasState.IsGridVisible);   // "!" = the opposite

    // Purpose:       Turns "snap to grid" on / off.
    // Precondition:  none.
    // Postcondition: IsSnapToGridEnabled flipped.
    [RelayCommand]
    private void ToggleSnapToGrid() => _canvasState.SetSnapToGridEnabled(!_canvasState.IsSnapToGridEnabled);

    // Purpose:       Picks the Select tool / the Pan tool / the Connect tool.
    // Precondition:  none.
    // Postcondition: CurrentTool is that tool.
    [RelayCommand]
    private void SelectTool() => _canvasState.SetCurrentTool(CanvasTool.Select);

    [RelayCommand]
    private void PanTool() => _canvasState.SetCurrentTool(CanvasTool.Pan);

    [RelayCommand]
    private void ConnectTool() => _canvasState.SetCurrentTool(CanvasTool.Connect);

    // Purpose:       Selects every device (Ctrl + A).
    // Precondition:  none.
    // Postcondition: every device is selected.
    [RelayCommand]
    private void SelectAll() => Interaction.OnSelectAll();

    // Purpose:       Deletes everything selected - cables and devices (the Delete key / the toolbar button).
    // Precondition:  something is selected (the button is disabled otherwise - see CanExecute).
    // Postcondition: every selected cable is unplugged and gone, every selected device is gone (with all its
    //                cables), nothing is selected, and the project is marked "unsaved" (by the services).
    // "CanExecute = nameof(HasSelection)" = the command (and its button) works only while HasSelection is true
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteSelection()
    {
        // No open network - nothing to delete from
        if (_applicationState.CurrentNetwork is not { } network)
        {
            return;   // stop here
        }

        // A copy of the selected ids ("ToList"), because deleting changes the selection while we go over it
        var selectedIds = _selectionState.SelectedIds.ToList();

        // First the selected cables (an id that is not a cable gives null and is skipped)
        foreach (var id in selectedIds)
        {
            if (network.GetConnection(id) is { } connection)
            {
                _connectionService.Disconnect(connection);   // unplug and remove it
            }
        }

        // Then the selected devices - DeviceService removes each one with all its cables, from the network
        // AND from the canvas
        foreach (var id in selectedIds)
        {
            if (network.GetDevice(id) is { } device)
            {
                _deviceService.RemoveDevice(device);
            }
        }

        // Nothing that was selected exists any more
        _selectionState.Clear();
    }

    // ----- Leaving the screen -----

    // Purpose:       Stops listening to the shared states, when the screen is left.
    // Precondition:  none.
    // Postcondition: this view model no longer reacts to them (so it can be freed from memory).
    public void Detach()
    {
        // "-=" = stop listening - the same seven "+=" from the constructor
        _itemsState.ItemsChanged -= OnItemsChanged;
        _placementState.Changed -= OnPlacementChanged;
        _canvasState.ViewportChanged -= OnViewportChanged;
        _canvasState.GridSettingsChanged -= OnGridSettingsChanged;
        _canvasState.CurrentToolChanged -= OnCurrentToolChanged;
        _selectionState.SelectionChanged -= OnSelectionChanged;
        _connectionService.ConnectionsChanged -= OnConnectionsChanged;
    }

    // ----- Reacting to changes -----

    // Purpose:       Devices were added / moved / renamed / removed.
    // Precondition:  none (called by ItemsChanged).
    // Postcondition: the screen was told IsEmpty / ShowEmptyHint may have changed, and the canvas redraws.
    private void OnItemsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));          // the number of devices may have reached / left 0
        OnPropertyChanged(nameof(ShowEmptyHint));    // so the hint may appear / disappear
        Redraw();                                    // devices (and their cables) moved
    }

    // Purpose:       A device kind was picked or cancelled in the Device Library.
    // Precondition:  none (called by the placement Changed event).
    // Postcondition: the placement properties were refreshed and the canvas redraws (the ghost).
    private void OnPlacementChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PendingPlacementDeviceType));   // the waiting kind
        OnPropertyChanged(nameof(IsPlacingDevice));              // is something waiting
        OnPropertyChanged(nameof(ShowEmptyHint));                // the hint hides while placing
        Redraw();                                                // the ghost appears / disappears
    }

    // Purpose:       The camera changed (zoom / scroll).
    // Precondition:  none (called by ViewportChanged).
    // Postcondition: the zoom text was refreshed and the canvas redraws.
    private void OnViewportChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Viewport));          // the camera itself
        OnPropertyChanged(nameof(ZoomPercentText));   // "125%"
        Redraw();                                     // everything moves / grows
    }

    // Purpose:       The grid was shown / hidden or snap changed.
    // Precondition:  none (called by GridSettingsChanged).
    // Postcondition: the toolbar toggles were refreshed and the canvas redraws.
    private void OnGridSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsGridVisible));          // the grid toggle button
        OnPropertyChanged(nameof(IsSnapToGridEnabled));    // the snap toggle button
        Redraw();                                          // the grid appears / disappears
    }

    // Purpose:       Another tool was picked.
    // Precondition:  none (called by CurrentToolChanged).
    // Postcondition: the tool buttons and the hint were refreshed; leaving the Connect tool gave up a
    //                half-made cable; the canvas redraws.
    private void OnCurrentToolChanged(object? sender, EventArgs e)
    {
        // Leaving the Connect tool must not leave a half-made cable (or its message) behind
        if (!IsConnectToolActive)
        {
            ConnectionTool.Cancel();
        }

        OnPropertyChanged(nameof(CurrentTool));            // the tool
        OnPropertyChanged(nameof(IsSelectToolActive));     // and the three toggle buttons
        OnPropertyChanged(nameof(IsPanToolActive));
        OnPropertyChanged(nameof(IsConnectToolActive));
        OnPropertyChanged(nameof(HintMessage));            // the Connect tool shows a hint
        Redraw();                                          // the cursor / highlights change
    }

    // Purpose:       The selection changed.
    // Precondition:  none (called by SelectionChanged).
    // Postcondition: HasSelection and the Delete command were refreshed; the canvas redraws.
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasSelection));            // is anything selected
        DeleteSelectionCommand.NotifyCanExecuteChanged();   // the Delete button may turn on / off
        Redraw();                                           // the highlights change
    }

    // Purpose:       A cable was added or removed.
    // Precondition:  none (called by ConnectionsChanged).
    // Postcondition: the canvas redraws.
    private void OnConnectionsChanged(object? sender, EventArgs e) => Redraw();

    // Purpose:       The Select / Pan mouse logic changed something (hover, rectangle, mouse position...).
    // Precondition:  none (called by Interaction.Changed).
    // Postcondition: the coordinates text was refreshed and the canvas redraws.
    private void OnInteractionChanged()
    {
        OnPropertyChanged(nameof(PointerCoordinatesText));   // "X: ...  Y: ..."
        Redraw();                                            // hover / rectangle
    }

    // Purpose:       The Connect tool changed (picked device, message, new cable).
    // Precondition:  none (called by ConnectionTool.Changed).
    // Postcondition: its red message went to ErrorMessage, its hint to HintMessage, and the canvas redraws.
    private void OnConnectionToolChanged()
    {
        // A problem is shown in the red box; anything else clears it
        ErrorMessage = ConnectionTool.IsStatusError ? ConnectionTool.StatusMessage : null;

        OnPropertyChanged(nameof(HintMessage));   // the blue box
        Redraw();                                 // the rubber band / the new cable
    }

    // Purpose:       Asks the canvas control to draw again.
    // Precondition:  none.
    // Postcondition: RedrawRequested was raised (if anyone listens).
    private void Redraw() => RedrawRequested?.Invoke(this, EventArgs.Empty);
}
