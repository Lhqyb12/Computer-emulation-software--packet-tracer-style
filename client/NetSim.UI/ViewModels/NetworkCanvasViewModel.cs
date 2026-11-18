using System;                              // brings in EventHandler, EventArgs, ArgumentNullException
using System.Collections.Generic;          // brings in IReadOnlyList<T> - a list that can only be read
using System.Linq;                         // brings in Min, Max (smallest / biggest value), FirstOrDefault, ToList
using CommunityToolkit.Mvvm.Input;         // brings in [RelayCommand] - turns a method into a Command a button can call
using NetSim.Application.Canvas;           // brings in the canvas states, the two mouse controllers, CanvasPoint, CanvasViewport...
using NetSim.Application.Services;         // brings in IDeviceService and IConnectionService - the doors for changing devices / cables
using NetSim.Application.State;            // brings in IApplicationState - the open network
using NetSim.Core.Common;                  // brings in EntityId - the unique id of a device or a cable
using NetSim.Core.Devices;                 // brings in DeviceType - the kind of a device
using NetSim.Core.Networking;              // brings in ConnectionType and CableRules (step 7)
using NetSim.UI.Common;                    // brings in ViewModelBase - gives this class ErrorMessage, IsBusy and "property changed"

namespace NetSim.UI.ViewModels;   // the "address" of this file: the ViewModels part of the UI layer

// The Network Canvas, as the screen sees it: what to draw (devices, cables, the selection, the grid, the ghost of the
// device being placed, the cable being made) and the toolbar's commands.
// It is the go-between of the canvas control (NetSim.UI/Controls/NetworkCanvasControl - pixels and mouse) and the
// Application layer (the states, the two mouse controllers and the services - the rules).
//   Step 3: placing a device.   Step 4: selecting, moving, deleting.
//   Step 5: zoom, scroll, grid and the toolbar.   Step 6: the Connect tool.   Step 7: cable kinds and the port list.
// Changes from the reference project: placing is one call to DeviceService.AddDevice; Delete removes selected devices
// too (the reference removed only cables); the Connect tool works device-to-device.
// "sealed" = no other class can inherit from it.
// "partial" = CommunityToolkit writes a second half of this class while building (the commands for [RelayCommand]).
// ": ViewModelBase" = it inherits ErrorMessage, IsBusy and the ability to tell the screen "this property changed"
public sealed partial class NetworkCanvasViewModel : ViewModelBase
{
    // Each toolbar zoom click changes the zoom by 25% (x 1.25). "const" = a fixed number
    private const double ZoomStep = 1.25;

    // The things this view model works with. "readonly" = set once in the constructor, never replaced
    private readonly IDeviceService _deviceService;             // adds / moves / removes devices
    private readonly IConnectionService _connectionService;     // removes cables (and tells us when cables change)
    private readonly ICanvasItemsState _itemsState;             // where every device is
    private readonly IDevicePlacementState _placementState;     // the device kind waiting to be placed (from the Device Library)
    private readonly ICanvasState _canvasState;                 // the camera, the grid and the tool
    private readonly ICanvasSelectionState _selectionState;     // what is selected
    private readonly IApplicationState _applicationState;       // the open network

    // The canvas's size on the screen, as last reported by the control (needed for "Fit to View")
    private double _viewportWidth;    // its width
    private double _viewportHeight;   // its height

    // Purpose:       Builds the canvas view model, its two mouse controllers, and starts listening to every source of change.
    // Precondition:  receives deviceService, connectionService, itemsState, placementState, canvasState, selectionState,
    //                applicationState - the seven things it works with (passed in by the DI container; none may be null).
    // Postcondition: returns nothing (a constructor) - the view model is ready and listens to the states until Detach().
    public NetworkCanvasViewModel(
        IDeviceService deviceService,
        IConnectionService connectionService,
        ICanvasItemsState itemsState,
        IDevicePlacementState placementState,
        ICanvasState canvasState,
        ICanvasSelectionState selectionState,
        IApplicationState applicationState)
    {
        // If any of the seven is missing (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(deviceService);
        ArgumentNullException.ThrowIfNull(connectionService);
        ArgumentNullException.ThrowIfNull(itemsState);
        ArgumentNullException.ThrowIfNull(placementState);
        ArgumentNullException.ThrowIfNull(canvasState);
        ArgumentNullException.ThrowIfNull(selectionState);
        ArgumentNullException.ThrowIfNull(applicationState);

        // Keep each one, to use in the methods below
        _deviceService = deviceService;
        _connectionService = connectionService;
        _itemsState = itemsState;
        _placementState = placementState;
        _canvasState = canvasState;
        _selectionState = selectionState;
        _applicationState = applicationState;

        // The two mouse controllers (Application layer). They belong to THIS view model only, so they are built here
        // with "new" and not taken from the DI container
        Interaction = new CanvasInteractionController(canvasState, itemsState, selectionState, deviceService, applicationState);   // select / drag / pan / zoom
        ConnectionTool = new ConnectionToolController(applicationState, itemsState, connectionService);                            // the Connect tool

        // Listen to our own controllers. They die together with this view model, so there is no need to stop listening
        // to them later. "(_, _) => X()" = when the event is raised, run X (the event's two values are not needed)
        Interaction.Changed += (_, _) => OnInteractionChanged();
        ConnectionTool.Changed += (_, _) => OnConnectionToolChanged();

        // Listen to the shared states. They live for the whole sign-in, longer than this view model, so these
        // listeners are removed again in Detach(). "+=" = start listening with the named method on the right
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

    // The mouse logic of the Select / Pan tools (steps 4-5). The canvas control calls it. Only "get" = never replaced
    public CanvasInteractionController Interaction { get; }

    // The Connect tool (steps 6-7). The canvas control calls it while the Connect tool is on
    public ConnectionToolController ConnectionTool { get; }

    // ----- What to draw -----

    // All the devices, in drawing order (the last one is drawn on top). "=>" = read from the items state every time
    public IReadOnlyList<CanvasItem> Items => _itemsState.Items;

    // All the cables as lines - worked out again on EVERY read (CanvasConnectionGeometry.Build), so they always follow the devices
    public IReadOnlyList<CanvasConnectionView> Connections => CanvasConnectionGeometry.Build(_applicationState.CurrentNetwork, _itemsState);

    // The camera (scroll + zoom), read from the canvas state
    public CanvasViewport Viewport => _canvasState.Viewport;

    // The grid: is it shown? the distance between lines? is snap on? (all read from the canvas state)
    public bool IsGridVisible => _canvasState.IsGridVisible;
    public double GridSpacing => _canvasState.GridSpacing;
    public bool IsSnapToGridEnabled => _canvasState.IsSnapToGridEnabled;

    // The tool picked in the toolbar, and three yes / no versions of it - one for each toggle button in the toolbar
    public CanvasTool CurrentTool => _canvasState.CurrentTool;
    public bool IsSelectToolActive => CurrentTool == CanvasTool.Select;    // true while the Select tool is picked
    public bool IsPanToolActive => CurrentTool == CanvasTool.Pan;          // true while the Pan tool is picked
    public bool IsConnectToolActive => CurrentTool == CanvasTool.Connect;  // true while the Connect tool is picked

    // The zoom as text for the toolbar, e.g. "125%". Zoom 1.25 * 100 = 125; ":0" = no digits after the point
    public string ZoomPercentText => $"{Viewport.Zoom * 100:0}%";

    // The world point under the mouse, for the small box in the corner, e.g. "X: 240   Y: 120"
    public string PointerCoordinatesText => $"X: {Interaction.PointerWorldPosition.X:0}   Y: {Interaction.PointerWorldPosition.Y:0}";

    // Is anything selected? (the Delete button is enabled only then). Count > 0 = at least one id in the selection
    public bool HasSelection => _selectionState.SelectedIds.Count > 0;

    // true when there is no device on the canvas
    public bool IsEmpty => _itemsState.Items.Count == 0;

    // Show the "Your network is empty" hint? Only with no devices AND ("&&") nothing being placed ("!" = NOT)
    public bool ShowEmptyHint => IsEmpty && !IsPlacingDevice;

    // The device kind waiting to be placed (its ghost follows the mouse), or null when none is waiting
    public DeviceType? PendingPlacementDeviceType => _placementState.PendingDeviceType;

    // true while a device kind is waiting to be placed
    public bool IsPlacingDevice => _placementState.IsPlacing;

    // A hint for the user (not an error), shown in a blue box. Read it as two nested "if / otherwise":
    //   if the Connect tool has a message that is NOT an error -> show that message (its next step / its success);
    //   otherwise, if the Connect tool is on and has no message at all -> show how to start (ConnectToolStartHint);
    //   otherwise -> null = no blue box
    public string? HintMessage =>
        ConnectionTool.StatusMessage is { } message && !ConnectionTool.IsStatusError
            ? message
            : IsConnectToolActive && ConnectionTool.StatusMessage is null
                ? ConnectToolStartHint
                : null;

    // How to start, for the cable picked in the toolbar (step 7): a chosen cable -> "choose a port"; Automatic -> "click two devices"
    private string ConnectToolStartHint => ConnectionTool.SelectedCableType is { } cableType
        ? $"{CableRules.DisplayName(cableType)} cable: click a device and choose a port, then do the same on the second device."
        : "Automatic cable: click a device, then a second device - the right cable and free ports are picked for you.";

    // ----- The cable picker in the toolbar (step 7) -----

    // The lines of the toolbar's cable list. Each CableOption = the text + the cable kind ("Automatic" has no kind: null).
    // "{ get; } = [ ... ]" = built once, here, and never replaced
    public IReadOnlyList<CableOption> CableOptions { get; } =
    [
        new("Automatic", null),                                                 // the right cable is picked by itself
        new("Copper Straight-Through", ConnectionType.CopperStraightThrough),   // between different sides (PC - switch)
        new("Copper Cross-Over", ConnectionType.CopperCrossover),               // between the same side (PC - PC, router - PC)
        new("Fiber", ConnectionType.Fiber),                                     // Gigabit ports only
        new("Serial", ConnectionType.Serial),                                   // router Serial ports only
    ];

    // The line picked in the cable list. The list is bound TwoWay: it READS "get" to show the pick, and WRITES "set"
    // when the user picks another line
    public CableOption SelectedCableOption
    {
        // The line whose kind is the Connect tool's cable. FirstOrDefault = the first match, or null; "?? CableOptions[0]" =
        // if no line matches, use the first line (Automatic)
        get => CableOptions.FirstOrDefault(o => o.Type == ConnectionTool.SelectedCableType) ?? CableOptions[0];
        set
        {
            // "value" = the line the user picked. The list can hand back null for a moment - ignore that
            if (value is null)
            {
                return;   // stop here
            }

            ConnectionTool.SelectCable(value.Type);                 // the Connect tool uses this cable from now on
            _canvasState.SetCurrentTool(CanvasTool.Connect);        // picking a cable = "I want to connect" - turn the Connect tool on (like Packet Tracer)
        }
    }

    // ----- The "choose a port" list (step 7) -----

    // Is the list open? (read from the Connect tool)
    public bool IsPortPickerOpen => ConnectionTool.IsPortPickerOpen;

    // Its title (e.g. "Switch0 - choose a port") and its lines (one PortOption per port)
    public string? PortPickerTitle => ConnectionTool.PortPickerTitle;
    public IReadOnlyList<PortOption> PortPickerOptions => ConnectionTool.PortPickerOptions;

    // Where it opens on the SCREEN: the device's bottom-left corner (a world point) turned into a screen point through the
    // camera (Viewport.WorldToScreen) - so the list follows the device when the user zooms or scrolls
    public double PortPickerLeft => Viewport.WorldToScreen(ConnectionTool.PortPickerAnchor).X;        // its left edge
    public double PortPickerTop => Viewport.WorldToScreen(ConnectionTool.PortPickerAnchor).Y + 4;     // its top edge, 4 units below the device

    // Purpose:       A port was clicked in the list (the port buttons call this command).
    // Precondition:  receives portName - the full name of the clicked line (e.g. "FastEthernet0/1").
    // Postcondition: returns nothing - the Connect tool used it (as the first end, or to make the cable).
    // [RelayCommand] = CommunityToolkit makes a "ChoosePortCommand" from this method, which a button can call
    [RelayCommand]
    private void ChoosePort(string portName) => ConnectionTool.ChoosePort(portName);

    // Purpose:       The list's "Cancel" button - gives up the cable being made.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - no list is open and nothing is picked. (Becomes "CancelConnectionCommand")
    [RelayCommand]
    private void CancelConnection() => ConnectionTool.Cancel();

    // Purpose:       Answers whether a device or a cable is selected (the canvas control draws it highlighted).
    // Precondition:  receives id - the device's or the cable's id.
    // Postcondition: returns true when it is selected, otherwise false. (The control only knows this view model, so we pass the question on)
    public bool IsSelected(EntityId id) => _selectionState.IsSelected(id);

    // ----- Placing a device (step 3) -----

    // Purpose:       Places the waiting device so its middle is at the point the user chose.
    // Precondition:  receives worldPoint - where to put the device's middle, as a WORLD point (the control already
    //                turned the mouse into one, through the camera).
    // Postcondition: returns nothing. If a device was waiting: it was added (snapped to the grid when snap is on) and is
    //                now the only selected thing, or ErrorMessage says why not; "place a device" mode is over.
    //                If nothing was waiting: nothing changed.
    public void ConfirmPlacement(CanvasPoint worldPoint)
    {
        // "is not { } deviceType" = if no kind is waiting (null) -> stop; otherwise keep the kind in "deviceType"
        if (_placementState.PendingDeviceType is not { } deviceType)
        {
            return;   // stop here - nothing to place
        }

        // Start with the point exactly as given
        var center = worldPoint;

        // With snap on, line the device's TOP-LEFT corner up with the grid (the same rule as dragging, step 5):
        // top-left = middle - half the size -> snap it to the grid -> back to the middle = snapped corner + half the size
        if (_canvasState.IsSnapToGridEnabled)
        {
            var size = CanvasItem.DefaultDeviceSize;                                                       // the size of a device: 96 x 72
            var topLeft = new CanvasPoint(worldPoint.X - size.Width / 2, worldPoint.Y - size.Height / 2);  // the corner, from the middle
            var snapped = CanvasGridSnap.Snap(topLeft, _canvasState.GridSpacing);                          // the corner moved onto the grid
            center = new CanvasPoint(snapped.X + size.Width / 2, snapped.Y + size.Height / 2);             // back to the middle
        }

        // Add the device - through DeviceService, step 2's one door: it goes into the network AND onto the canvas,
        // with a free name ("Router0"...). It hands back a result: success + the device, or failure + why
        var result = _deviceService.AddDevice(deviceType, center);

        // Show the reason in the red box when it did not work, or clear an old error when it did ("a ? b : c" = if / otherwise)
        ErrorMessage = result.IsSuccess ? null : result.ErrorMessage;

        // The new device becomes the only selected thing, so it can be moved or deleted right away (step 4).
        // "result.Value is { } device" = the new device is there - keep it in "device"
        if (result.IsSuccess && result.Value is { } device)
        {
            _selectionState.Replace([device.Id]);   // "[x]" = a list with one id
        }

        // One pick places one device - leave "place a device" mode (the Device Library un-selects its card too)
        _placementState.Cancel();
    }

    // Purpose:       Leaves "place a device" mode without placing anything (Escape).
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - nothing is waiting to be placed.
    public void CancelPlacement() => _placementState.Cancel();

    // Purpose:       Remembers the canvas's size on the screen (the control calls it every time its size changes).
    // Precondition:  receives width, height - the canvas size in screen units.
    // Postcondition: returns nothing - "Fit to View" will use this size.
    public void SetViewportSize(double width, double height)
    {
        _viewportWidth = width;     // remember the width
        _viewportHeight = height;   // and the height
    }

    // ----- Toolbar commands (step 5). Each [RelayCommand] becomes "<Name>Command", which a toolbar button calls -----

    // Purpose:       Zoom in by 25% (around the top-left corner).
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - the zoom grew by 25% (up to 400%).
    [RelayCommand]
    private void ZoomIn() => _canvasState.SetZoom(_canvasState.Viewport.Zoom * ZoomStep);   // the current zoom x 1.25

    // Purpose:       Zoom out by 25%.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - the zoom shrank by 25% (down to 25%).
    [RelayCommand]
    private void ZoomOut() => _canvasState.SetZoom(_canvasState.Viewport.Zoom / ZoomStep);  // the current zoom / 1.25

    // Purpose:       Back to the starting view: world (0, 0) at the top-left corner, 100%.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - the camera is the default one.
    [RelayCommand]
    private void ResetView() => _canvasState.ResetViewport();

    // Purpose:       Zooms and scrolls so that every device is in view ("Fit to View").
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - all devices are visible and centered; with no devices (or no known size) - the default view.
    [RelayCommand]
    private void FitToView()
    {
        var items = _itemsState.Items;   // all the devices on the canvas

        // No devices, or the canvas size is not known yet - just go back to the default view
        if (items.Count == 0 || _viewportWidth <= 0 || _viewportHeight <= 0)
        {
            _canvasState.ResetViewport();
            return;   // stop here
        }

        // The rectangle around ALL the devices: Min = the smallest left / top of any device;
        // Max = the biggest right (left + width) / bottom (top + height) of any device
        var min = new CanvasPoint(items.Min(i => i.Position.X), items.Min(i => i.Position.Y));
        var max = new CanvasPoint(items.Max(i => i.Position.X + i.Size.Width), items.Max(i => i.Position.Y + i.Size.Height));

        // A camera that shows exactly that rectangle, with some room around it (CanvasViewport.Fit), made the current one
        _canvasState.SetViewport(CanvasViewport.Fit(min, max, _viewportWidth, _viewportHeight));
    }

    // Purpose:       Shows / hides the grid.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - IsGridVisible flipped ("!" = the opposite of what it is now).
    [RelayCommand]
    private void ToggleGrid() => _canvasState.SetGridVisible(!_canvasState.IsGridVisible);

    // Purpose:       Turns "snap to grid" on / off.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - IsSnapToGridEnabled flipped.
    [RelayCommand]
    private void ToggleSnapToGrid() => _canvasState.SetSnapToGridEnabled(!_canvasState.IsSnapToGridEnabled);

    // Purpose:       Picks the Select tool.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - CurrentTool is Select.
    [RelayCommand]
    private void SelectTool() => _canvasState.SetCurrentTool(CanvasTool.Select);

    // Purpose:       Picks the Pan tool.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - CurrentTool is Pan.
    [RelayCommand]
    private void PanTool() => _canvasState.SetCurrentTool(CanvasTool.Pan);

    // Purpose:       Picks the Connect tool.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - CurrentTool is Connect.
    [RelayCommand]
    private void ConnectTool() => _canvasState.SetCurrentTool(CanvasTool.Connect);

    // Purpose:       Selects every device (Ctrl + A).
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - every device is selected.
    [RelayCommand]
    private void SelectAll() => Interaction.OnSelectAll();

    // Purpose:       Deletes everything selected - cables and devices (the Delete key / the toolbar button).
    // Precondition:  receives nothing (something must be selected - the button is disabled otherwise, see CanExecute).
    // Postcondition: returns nothing - every selected cable is unplugged and gone, every selected device is gone (with all
    //                its cables), nothing is selected, and the project is marked "unsaved" (by the services).
    // "CanExecute = nameof(HasSelection)" = the command (and its button) works only while HasSelection is true
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteSelection()
    {
        // No open network - nothing to delete from
        if (_applicationState.CurrentNetwork is not { } network)
        {
            return;   // stop here
        }

        // A COPY of the selected ids ("ToList"), because deleting changes the selection while we go over it
        var selectedIds = _selectionState.SelectedIds.ToList();

        // FIRST the selected cables. GetConnection returns the cable with this id, or null when the id is a device - then skip it.
        // (Cables first: deleting a device first would delete its cables too, and deleting them again would fail)
        foreach (var id in selectedIds)
        {
            if (network.GetConnection(id) is { } connection)
            {
                _connectionService.Disconnect(connection);   // unplug and remove it (and mark "unsaved")
            }
        }

        // THEN the selected devices. DeviceService removes each one with all its cables, from the network AND the canvas
        foreach (var id in selectedIds)
        {
            if (network.GetDevice(id) is { } device)
            {
                _deviceService.RemoveDevice(device);
            }
        }

        // Nothing that was selected exists any more - empty the selection
        _selectionState.Clear();
    }

    // ----- Leaving the screen -----

    // Purpose:       Stops listening to the shared states, when the screen is left.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - this view model no longer reacts to them (so it can be freed from memory).
    // Why: the states live for the whole sign-in, but a NEW view model is made on every visit to the screen - without this,
    // old view models would keep listening and stay in memory forever
    public void Detach()
    {
        // "-=" = stop listening - exactly the seven "+=" from the constructor
        _itemsState.ItemsChanged -= OnItemsChanged;
        _placementState.Changed -= OnPlacementChanged;
        _canvasState.ViewportChanged -= OnViewportChanged;
        _canvasState.GridSettingsChanged -= OnGridSettingsChanged;
        _canvasState.CurrentToolChanged -= OnCurrentToolChanged;
        _selectionState.SelectionChanged -= OnSelectionChanged;
        _connectionService.ConnectionsChanged -= OnConnectionsChanged;
    }

    // ----- Reacting to changes. Each method below runs when one of the events above is raised -----
    // (object? sender, EventArgs e) = the two values every event gives: who raised it, and extra details (unused here)

    // Purpose:       Devices were added / moved / renamed / removed.
    // Precondition:  receives sender, e - the event's values (not used); called by ItemsChanged.
    // Postcondition: returns nothing - the screen was told IsEmpty / ShowEmptyHint may have changed, and the canvas redraws.
    private void OnItemsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));          // tell the screen: the number of devices may have reached / left 0
        OnPropertyChanged(nameof(ShowEmptyHint));    // so the "empty" hint may appear / disappear
        Redraw();                                    // devices (and their cables) moved - draw again
    }

    // Purpose:       A device kind was picked or cancelled in the Device Library.
    // Precondition:  receives sender, e - the event's values (not used); called by the placement Changed event.
    // Postcondition: returns nothing - the placement properties were refreshed and the canvas redraws (the ghost).
    private void OnPlacementChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PendingPlacementDeviceType));   // the waiting kind changed
        OnPropertyChanged(nameof(IsPlacingDevice));              // so may "is something waiting"
        OnPropertyChanged(nameof(ShowEmptyHint));                // the hint hides while placing, and comes back after
        Redraw();                                                // the ghost appears / disappears
    }

    // Purpose:       The camera changed (zoom / scroll).
    // Precondition:  receives sender, e - the event's values (not used); called by ViewportChanged.
    // Postcondition: returns nothing - the zoom text and the port list's place were refreshed, and the canvas redraws.
    private void OnViewportChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Viewport));          // the camera itself
        OnPropertyChanged(nameof(ZoomPercentText));   // the toolbar's "125%"
        OnPropertyChanged(nameof(PortPickerLeft));    // an open port list moves with its device
        OnPropertyChanged(nameof(PortPickerTop));
        Redraw();                                     // everything moves / grows
    }

    // Purpose:       The grid was shown / hidden, or snap changed.
    // Precondition:  receives sender, e - the event's values (not used); called by GridSettingsChanged.
    // Postcondition: returns nothing - the toolbar toggles were refreshed and the canvas redraws.
    private void OnGridSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsGridVisible));          // the grid toggle button
        OnPropertyChanged(nameof(IsSnapToGridEnabled));    // the snap toggle button
        Redraw();                                          // the grid appears / disappears
    }

    // Purpose:       Another tool was picked.
    // Precondition:  receives sender, e - the event's values (not used); called by CurrentToolChanged.
    // Postcondition: returns nothing - the tool buttons and the hint were refreshed; leaving the Connect tool gave up a
    //                half-made cable; the canvas redraws.
    private void OnCurrentToolChanged(object? sender, EventArgs e)
    {
        // Leaving the Connect tool must not leave a half-made cable (or its message, or an open port list) behind
        if (!IsConnectToolActive)
        {
            ConnectionTool.Cancel();
        }

        OnPropertyChanged(nameof(CurrentTool));            // the tool
        OnPropertyChanged(nameof(IsSelectToolActive));     // and the three toggle buttons
        OnPropertyChanged(nameof(IsPanToolActive));
        OnPropertyChanged(nameof(IsConnectToolActive));
        OnPropertyChanged(nameof(HintMessage));            // the Connect tool shows its "how to start" hint
        Redraw();                                          // the cursor / highlights change
    }

    // Purpose:       The selection changed.
    // Precondition:  receives sender, e - the event's values (not used); called by SelectionChanged.
    // Postcondition: returns nothing - HasSelection and the Delete command were refreshed; the canvas redraws.
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasSelection));            // is anything selected
        DeleteSelectionCommand.NotifyCanExecuteChanged();   // ask the Delete button to check CanExecute again (turn on / off)
        Redraw();                                           // the highlights change
    }

    // Purpose:       A cable was added or removed.
    // Precondition:  receives sender, e - the event's values (not used); called by ConnectionsChanged.
    // Postcondition: returns nothing - the canvas redraws.
    private void OnConnectionsChanged(object? sender, EventArgs e) => Redraw();

    // Purpose:       The Select / Pan mouse logic changed something (hover, rectangle, mouse position...).
    // Precondition:  receives nothing; called by Interaction.Changed.
    // Postcondition: returns nothing - the coordinates text was refreshed and the canvas redraws.
    private void OnInteractionChanged()
    {
        OnPropertyChanged(nameof(PointerCoordinatesText));   // the corner box "X: ...  Y: ..."
        Redraw();                                            // hover / rectangle
    }

    // Purpose:       The Connect tool changed (a picked device, a message, a new cable, the port list).
    // Precondition:  receives nothing; called by ConnectionTool.Changed.
    // Postcondition: returns nothing - its red message went to ErrorMessage, its hint to HintMessage, the cable list and
    //                the port list were refreshed, and the canvas redraws.
    private void OnConnectionToolChanged()
    {
        // A problem goes into the red box; anything else clears the red box ("a ? b : c" = if / otherwise)
        ErrorMessage = ConnectionTool.IsStatusError ? ConnectionTool.StatusMessage : null;

        OnPropertyChanged(nameof(HintMessage));          // the blue box
        OnPropertyChanged(nameof(SelectedCableOption));  // the toolbar's cable list (step 7)
        OnPropertyChanged(nameof(IsPortPickerOpen));     // the port list: open?
        OnPropertyChanged(nameof(PortPickerTitle));      // its title
        OnPropertyChanged(nameof(PortPickerOptions));    // its lines
        OnPropertyChanged(nameof(PortPickerLeft));       // its place
        OnPropertyChanged(nameof(PortPickerTop));
        Redraw();                                        // the rubber band / the new cable
    }

    // Purpose:       Asks the canvas control to draw again.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - RedrawRequested was raised (if anyone listens; "?." = only then).
    private void Redraw() => RedrawRequested?.Invoke(this, EventArgs.Empty);
}

// One line in the toolbar's cable list (step 7): Label = the text shown; Type = the cable kind (null = Automatic).
// "record" = holds data only, two with the same values are equal; "sealed" = nothing inherits from it.
// "(string Label, ConnectionType? Type)" = "new CableOption("Fiber", ConnectionType.Fiber)" builds one
public sealed record CableOption(string Label, ConnectionType? Type);
