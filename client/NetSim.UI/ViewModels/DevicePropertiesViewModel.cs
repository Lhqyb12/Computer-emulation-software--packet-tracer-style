using System;                              // brings in EventHandler, EventArgs, ArgumentNullException
using System.Collections.ObjectModel;      // brings in ObservableCollection<T> - a list that tells the screen when items are added / removed
using CommunityToolkit.Mvvm.ComponentModel; // brings in [ObservableProperty] - writes a property that tells the screen when it changes
using CommunityToolkit.Mvvm.Input;         // brings in [RelayCommand] - turns a method into a Command a button can call
using NetSim.Application.Canvas;           // brings in ICanvasItemsState - tells us when devices are moved / renamed / removed
using NetSim.Application.Services;         // brings in IDeviceService (rename) and IConnectionService (tells us when cables change)
using NetSim.Application.State;            // brings in IApplicationState - the open network
using NetSim.Core.Devices;                 // brings in NetworkDevice and DeviceTypeRegistry (the kinds' screen names)
using NetSim.UI.Common;                    // brings in ViewModelBase - the base class of every screen part

namespace NetSim.UI.ViewModels;   // the "address" of this file: the ViewModels part of the UI layer

// The Device Properties panel (step 9): opens on the right side when the user double-clicks a device, and shows that
// device - its name (which can be changed here), its kind, and the list of its ports.
// Taken from the reference project's DevicePropertiesViewModel (about 1,600 lines there - with power, VLANs, routing, DNS
// and more from later steps). Here: only what steps 9-10 need. Two changes:
//   1. There it showed whatever device was SELECTED; here it opens on a DOUBLE-CLICK (like Packet Tracer) and has a Close button.
//   2. Renaming goes only through DeviceService.RenameDevice - the reference also renamed the canvas item and marked the
//      project unsaved by itself, which DeviceService already does (step 2).
// "sealed" = nothing inherits from it; "partial" = CommunityToolkit writes a second half (properties and commands)
public sealed partial class DevicePropertiesViewModel : ViewModelBase
{
    // The things this panel works with. "readonly" = set once in the constructor, never replaced
    private readonly IApplicationState _applicationState;     // the open network
    private readonly IDeviceService _deviceService;           // renames the device (and marks the project unsaved)
    private readonly IConnectionService _connectionService;   // tells us when a cable is added / removed
    private readonly ICanvasItemsState _itemsState;           // tells us when a device is renamed / removed

    // The device whose properties are shown, or null when the panel is closed
    private NetworkDevice? _device;

    // Is the panel open? [ObservableProperty] = CommunityToolkit writes a public "IsOpen" property for this field, which
    // tells the screen every time it changes (the panel appears / disappears)
    [ObservableProperty]
    private bool _isOpen;

    // The name in the name box. The box is bound TwoWay, so typing changes this; "Apply" sends it to DeviceService
    [ObservableProperty]
    private string _deviceName = string.Empty;

    // The device's kind as text, e.g. "Router" (shown under the name)
    [ObservableProperty]
    private string _deviceTypeText = string.Empty;

    // Why the new name was refused (e.g. "A device named 'PC0' already exists."), or null when there is no problem
    [ObservableProperty]
    private string? _nameError;

    // The port lines (one DeviceInterfaceItem per port). "= []" = it starts empty and is never replaced - only filled again
    public ObservableCollection<DeviceInterfaceItem> Interfaces { get; } = [];

    // Purpose:       Builds the panel (closed) and starts listening to the changes that may affect the device it shows.
    // Precondition:  receives applicationState - the open network; deviceService - renames devices;
    //                connectionService - tells when cables change; itemsState - tells when devices change
    //                (all passed in by the DI container; none may be null).
    // Postcondition: returns nothing (a constructor) - the panel is closed and listens to the three events until Detach().
    public DevicePropertiesViewModel(
        IApplicationState applicationState,
        IDeviceService deviceService,
        IConnectionService connectionService,
        ICanvasItemsState itemsState)
    {
        // If any of the four is missing (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(applicationState);
        ArgumentNullException.ThrowIfNull(deviceService);
        ArgumentNullException.ThrowIfNull(connectionService);
        ArgumentNullException.ThrowIfNull(itemsState);

        _applicationState = applicationState;     // keep the open network
        _deviceService = deviceService;           // keep the device rules
        _connectionService = connectionService;   // keep the cable rules
        _itemsState = itemsState;                 // keep the canvas positions

        // "+=" = start listening. Each event runs the named method below
        _applicationState.CurrentNetworkChanged += OnCurrentNetworkChanged;   // another project opened / closed -> close the panel
        _connectionService.ConnectionsChanged += OnConnectionsChanged;        // a cable added / removed -> the "connected to" texts change
        _itemsState.ItemsChanged += OnItemsChanged;                           // the device may have been removed -> close the panel
    }

    // Purpose:       Opens the panel on one device (called on a double-click on the canvas).
    // Precondition:  receives device - the device to show (not null).
    // Postcondition: returns nothing - the panel is open and shows this device's name, kind and ports.
    public void Open(NetworkDevice device)
    {
        // If no device was passed in (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(device);

        _device = device;                                                         // remember which device is shown
        DeviceName = device.Name;                                                 // its name, into the name box
        DeviceTypeText = DeviceTypeRegistry.Get(device.DeviceType).DisplayName;   // its kind's screen name (step 1's registry), e.g. "PC"
        NameError = null;                                                         // no old error from another device
        LoadInterfaces();                                                         // build its port lines
        IsOpen = true;                                                            // show the panel
    }

    // Purpose:       Closes the panel (its X button, Escape, or when the device / the network is gone).
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - the panel is hidden and remembers no device.
    // [RelayCommand] = CommunityToolkit also makes "CloseCommand", which the X button calls
    [RelayCommand]
    private void Close()
    {
        _device = null;          // no device is shown any more
        Interfaces.Clear();      // empty the port list
        NameError = null;        // no error
        IsOpen = false;          // hide the panel
    }

    // Purpose:       The "Apply" button next to the name box - renames the device.
    // Precondition:  receives nothing (uses the name typed in DeviceName).
    // Postcondition: returns nothing - the device (and its label on the canvas) has the new name, or NameError says why not.
    [RelayCommand]
    private void ApplyName()
    {
        // No device shown - nothing to rename
        if (_device is null)
        {
            return;   // stop here
        }

        // Rename through DeviceService - step 2's one door: it checks the name (not empty, not taken), renames the device
        // in the network AND on the canvas, and marks the project unsaved. It hands back a result: success or failure + why
        var result = _deviceService.RenameDevice(_device, DeviceName);

        if (result.IsSuccess)
        {
            NameError = null;              // no problem
            DeviceName = _device.Name;     // show the name exactly as it was saved (e.g. without spaces at its ends)
        }
        else
        {
            NameError = result.ErrorMessage;   // show why it was refused, under the name box
        }
    }

    // Purpose:       Stops listening to the shared states, when the workspace screen is left.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - the panel no longer reacts to them (so it can be freed from memory).
    public void Detach()
    {
        // "-=" = stop listening - exactly the three "+=" from the constructor
        _applicationState.CurrentNetworkChanged -= OnCurrentNetworkChanged;
        _connectionService.ConnectionsChanged -= OnConnectionsChanged;
        _itemsState.ItemsChanged -= OnItemsChanged;
    }

    // Purpose:       Builds the port lines of the shown device again.
    // Precondition:  receives nothing (uses the shown device).
    // Postcondition: returns nothing - Interfaces holds one fresh line per port (empty when no device is shown).
    private void LoadInterfaces()
    {
        Interfaces.Clear();   // throw away the old lines

        // No device shown - leave the list empty
        if (_device is null)
        {
            return;   // stop here
        }

        // One new line per port, in the device's own order (DeviceInterfaceItem turns the port into text - part 9.1)
        foreach (var networkInterface in _device.Interfaces)
        {
            Interfaces.Add(new DeviceInterfaceItem(networkInterface));
        }
    }

    // Purpose:       Runs when another network becomes the open one (a project opened / closed).
    // Precondition:  receives sender, e - the event's values (not used); called by CurrentNetworkChanged.
    // Postcondition: returns nothing - the panel is closed (the device belonged to the old network).
    private void OnCurrentNetworkChanged(object? sender, EventArgs e) => Close();

    // Purpose:       Runs when a cable was added or removed.
    // Precondition:  receives sender, e - the event's values (not used); called by ConnectionsChanged.
    // Postcondition: returns nothing - when the panel is open, its port lines were built again ("connected to" / status).
    private void OnConnectionsChanged(object? sender, EventArgs e)
    {
        if (IsOpen)
        {
            LoadInterfaces();
        }
    }

    // Purpose:       Runs when devices on the canvas changed (moved, renamed, removed).
    // Precondition:  receives sender, e - the event's values (not used); called by ItemsChanged.
    // Postcondition: returns nothing - if the shown device was removed, the panel is closed; otherwise its port lines were
    //                built again (a removed NEIGHBOUR removes its cables too, which changes "connected to").
    private void OnItemsChanged(object? sender, EventArgs e)
    {
        // The panel is closed - nothing to update
        if (_device is null)
        {
            return;   // stop here
        }

        // "?." = if there is no open network the answer is null; "!= true" = so null and false both count as "not there".
        // The shown device is no longer in the network (it was deleted) - close the panel
        if (_applicationState.CurrentNetwork?.ContainsDevice(_device) != true)
        {
            Close();
            return;   // stop here
        }

        // Still there - refresh its port lines. (The name box is NOT refreshed here, so a move while the user types
        // does not wipe what they typed)
        LoadInterfaces();
    }
}
