using System;                              // EventHandler, EventArgs, ArgumentNullException
using System.Collections.Generic;          // IReadOnlyList<T>
using NetSim.Application.Canvas;           // ICanvasItemsState, IDevicePlacementState, CanvasItem, CanvasPoint
using NetSim.Application.Services;         // IDeviceService - the one door for adding devices
using NetSim.Core.Devices;                 // DeviceType
using NetSim.UI.Common;                    // ViewModelBase - IsBusy, ErrorMessage

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The Network Canvas, as the screen sees it: what to draw (the devices and the "ghost" of the device
// waiting to be placed) and what to do when the user places a device.
// It is the go-between of the canvas control (NetSim.UI/Controls/NetworkCanvasControl - pixels and
// mouse) and the Application layer (DeviceService, CanvasItemsState, DevicePlacementState - the rules).
// Change from the reference project: there this class added the device to the network AND put its
// item on the canvas by itself. Here DeviceService.AddDevice already does both (step 2), so placing a
// device is one call.
// "sealed" = no other class can inherit from it; "partial" = CommunityToolkit may add a second half
public sealed partial class NetworkCanvasViewModel : ViewModelBase
{
    private readonly IDeviceService _deviceService;             // adds the device to the network + the canvas
    private readonly ICanvasItemsState _itemsState;             // where every device sits on the canvas
    private readonly IDevicePlacementState _placementState;     // the device kind waiting to be placed

    // Purpose:       Builds the canvas view model and starts listening to its two sources of change.
    // Precondition:  deviceService, itemsState and placementState are not null (the DI container passes them in).
    // Postcondition: the view model is ready; it listens to ItemsChanged and to the placement Changed
    //                event until Detach() is called.
    public NetworkCanvasViewModel(IDeviceService deviceService, ICanvasItemsState itemsState, IDevicePlacementState placementState)
    {
        // Stop right away with a clear message if something was not passed in
        ArgumentNullException.ThrowIfNull(deviceService);
        ArgumentNullException.ThrowIfNull(itemsState);
        ArgumentNullException.ThrowIfNull(placementState);

        _deviceService = deviceService;     // keep the device rules
        _itemsState = itemsState;           // keep the canvas positions
        _placementState = placementState;   // keep the waiting device

        // "+=" = start listening. The methods below run every time these events are raised
        _itemsState.ItemsChanged += OnItemsChanged;                // a device was added / moved / renamed / removed
        _placementState.Changed += OnPlacementChanged;             // a device kind was picked / cancelled
    }

    // Raised every time the canvas must be drawn again. The canvas control listens to it
    public event EventHandler? RedrawRequested;

    // All the devices on the canvas, in drawing order (the last one is drawn on top). Read from the state
    public IReadOnlyList<CanvasItem> Items => _itemsState.Items;

    // true when there is no device on the canvas - the screen then shows the "Your network is empty" hint
    public bool IsEmpty => _itemsState.Items.Count == 0;

    // Show the "Your network is empty" hint? Only when there are no devices AND nothing is being placed -
    // so the hint does not cover the ghost while the user is choosing where to put the first device
    public bool ShowEmptyHint => IsEmpty && !IsPlacingDevice;

    // The device kind waiting to be placed, or null. The control draws its "ghost" under the mouse
    public DeviceType? PendingPlacementDeviceType => _placementState.PendingDeviceType;

    // true while a device kind is waiting to be placed
    public bool IsPlacingDevice => _placementState.IsPlacing;

    // Purpose:       Places the waiting device on the canvas, centered on the point the user chose.
    // Precondition:  worldPoint is a world point on the canvas (until zoom / scroll arrive in step 5,
    //                a world point is simply the mouse position inside the canvas).
    // Postcondition: if a device was waiting - it was added (to the network and the canvas) or
    //                ErrorMessage says why not, and "place a device" mode is over either way.
    //                If nothing was waiting - nothing changed.
    public void ConfirmPlacement(CanvasPoint worldPoint)
    {
        // Nothing is waiting to be placed - nothing to do. "is not { } deviceType" = if it is null
        if (_placementState.PendingDeviceType is not { } deviceType)
        {
            return;   // stop here - nothing more to do
        }

        // Add the device: DeviceService builds it, puts it in the network and puts its item on the
        // canvas, centered on the point. A free name ("Router0", "PC1"...) is picked by itself
        var result = _deviceService.AddDevice(deviceType, worldPoint);

        // Show the reason when it did not work (e.g. no project is open), or clear an old error when it did.
        // "a ? b : c" = if a then b, otherwise c
        ErrorMessage = result.IsSuccess ? null : result.ErrorMessage;

        // Leave "place a device" mode - one pick places one device (like Packet Tracer).
        // The Device Library hears this too, and un-selects its card
        _placementState.Cancel();
    }

    // Purpose:       Leaves "place a device" mode without placing anything (the Escape key).
    // Precondition:  none.
    // Postcondition: nothing is waiting to be placed. Does nothing if nothing was waiting.
    public void CancelPlacement() => _placementState.Cancel();   // the placement state tells everyone who listens

    // Purpose:       Stops listening to the Application layer, when the screen is left.
    // Precondition:  none.
    // Postcondition: this view model no longer reacts to the canvas or the placement state.
    //                Why: the states live for the whole sign-in, but a new canvas view model is made
    //                every time the workspace screen is opened - without this, old ones would keep
    //                listening (and stay in memory) forever.
    public void Detach()
    {
        // "-=" = stop listening
        _itemsState.ItemsChanged -= OnItemsChanged;          // no more "items changed"
        _placementState.Changed -= OnPlacementChanged;       // no more "placement changed"
    }

    // Purpose:       Runs when a device was added / moved / renamed / removed - redraws and updates IsEmpty.
    // Precondition:  none (called by the ItemsChanged event).
    // Postcondition: RedrawRequested was raised and the screen was told IsEmpty may have changed.
    private void OnItemsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));                   // the number of devices may have reached / left 0
        OnPropertyChanged(nameof(ShowEmptyHint));             // so the "empty" hint may need to appear / disappear
        RedrawRequested?.Invoke(this, EventArgs.Empty);       // draw the canvas again ("?." = only if someone listens)
    }

    // Purpose:       Runs when a device kind was picked or cancelled - updates the ghost and the properties.
    // Precondition:  none (called by the placement Changed event).
    // Postcondition: the screen was told the placement properties changed, and RedrawRequested was raised.
    private void OnPlacementChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PendingPlacementDeviceType));   // the kind waiting to be placed changed
        OnPropertyChanged(nameof(IsPlacingDevice));              // and so may "is a device waiting"
        OnPropertyChanged(nameof(ShowEmptyHint));                // the hint hides while placing, and comes back after
        RedrawRequested?.Invoke(this, EventArgs.Empty);          // draw again: the ghost appears / disappears
    }
}
