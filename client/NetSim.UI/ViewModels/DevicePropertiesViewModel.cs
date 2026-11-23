using System;                              // brings in EventHandler, EventArgs, ArgumentNullException
using System.Collections.ObjectModel;      // brings in ObservableCollection<T> - a list that tells the screen when items are added / removed
using System.Linq;                         // brings in FirstOrDefault - "the first item that passes this test, or null" (step 10)
using CommunityToolkit.Mvvm.ComponentModel; // brings in [ObservableProperty] - writes a property that tells the screen when it changes
using CommunityToolkit.Mvvm.Input;         // brings in [RelayCommand] - turns a method into a Command a button can call
using NetSim.Application.Canvas;           // brings in ICanvasItemsState - tells us when devices are moved / renamed / removed
using NetSim.Application.Services;         // brings in IDeviceService (rename) and IConnectionService (tells us when cables change)
using NetSim.Application.State;            // brings in IApplicationState - the open network
using NetSim.Core.Devices;                 // brings in NetworkDevice and DeviceTypeRegistry (the kinds' screen names)
using NetSim.Core.Networking;              // brings in NetworkInterface and InterfaceType (step 10)
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

    // ----- IP settings (step 10) -----

    // Can this device have IP addresses at all? false for a switch - it passes frames by MAC address only, so the
    // IP SETTINGS section is hidden for it
    [ObservableProperty]
    private bool _canConfigureIPv4;

    // Does this device use a default gateway? Only end devices (PC, laptop, server) - a router does not need one per port,
    // it IS the gateway of the others. When false, the Gateway box is hidden
    [ObservableProperty]
    private bool _usesDefaultGateway;

    // The port picked in the IP SETTINGS list (one of IPv4Ports), or null when there is none.
    // When it changes, OnSelectedPortChanged (below) fills the three boxes with that port's values
    [ObservableProperty]
    private DeviceInterfaceItem? _selectedPort;

    // The three boxes. Bound TwoWay, so typing changes them; "Apply" (part 10.9) reads, checks and sends them.
    // Plain text, not IPv4Address - the user may type something that is not an address yet ("192.168.")
    [ObservableProperty]
    private string _addressInput = string.Empty;       // e.g. "192.168.1.10"

    [ObservableProperty]
    private string _subnetMaskInput = string.Empty;    // e.g. "255.255.255.0"

    [ObservableProperty]
    private string _defaultGatewayInput = string.Empty;   // e.g. "192.168.1.1", or empty for "no gateway"

    // Why the IP settings were refused (e.g. "The default gateway ... is not in this port's network"), or null
    [ObservableProperty]
    private string? _addressError;

    // The port lines (one DeviceInterfaceItem per port). "= []" = it starts empty and is never replaced - only filled again
    public ObservableCollection<DeviceInterfaceItem> Interfaces { get; } = [];

    // Step 10: the ports that can have an IP address - the choices of the IP SETTINGS list. The same lines as in
    // Interfaces, without the Console port (a management cable, not a network port - it never has an address)
    public ObservableCollection<DeviceInterfaceItem> IPv4Ports { get; } = [];

    // True only while LoadInterfaces rebuilds the lists. Emptying a list also empties the screen's choice for a moment -
    // this flag tells OnSelectedPortChanged "ignore that, it is only the reload" so the boxes keep what the user typed
    private bool _isReloading;

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
        _deviceService.InterfacesChanged += OnInterfacesChanged;              // step 10: a port's address / gateway changed -> show it
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
        CanConfigureIPv4 = device is not Switch;                                  // step 10: a switch has no IP addresses
        UsesDefaultGateway = device is EndDevice;                                 // step 10: only PC / laptop / server use a gateway
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
        LoadInterfaces();        // _device is null now -> empties both port lists and the three IP boxes (step 10)
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
    // Purpose:       The "Apply" button of IP SETTINGS - gives the picked port the typed address and mask, and (on end
    //                devices) the typed default gateway.
    // Precondition:  receives nothing (uses SelectedPort and the three boxes).
    // Postcondition: returns nothing - the port has the new settings and the boxes show them as saved, or AddressError
    //                says what is wrong (then nothing was saved - except when only the gateway was refused, which it says).
    // [RelayCommand] = CommunityToolkit also makes "ApplyIPv4Command", which the Apply button calls
    [RelayCommand]
    private void ApplyIPv4()
    {
        // "is not { } port" = no port is picked - nothing to apply; otherwise keep the picked line in "port"
        if (SelectedPort is not { } port)
        {
            return;   // stop here
        }

        // ----- 1. Turn the typed text into values (parts 10.1, 10.2). Nothing is changed before all of them pass -----

        // TryParse = "is this text an address?" - true + the address in "address", or false.
        // "!" = NOT: the text is NOT an address -> say so and stop
        if (!IPv4Address.TryParse(AddressInput, out var address))
        {
            AddressError = "Type the IP address as four numbers from 0 to 255 with dots, e.g. 192.168.1.10.";
            return;
        }

        // The same for the mask - SubnetMask.TryParse also refuses a mask whose 1-bits are not all on the left
        if (!SubnetMask.TryParse(SubnetMaskInput, out var subnetMask))
        {
            AddressError = "Type a valid subnet mask, e.g. 255.255.255.0.";
            return;
        }

        // The gateway: null = "no gateway" (an empty box). "IPv4Address?" = an address or null
        IPv4Address? gateway = null;

        // Only end devices use a gateway, and only when something was typed. IsNullOrWhiteSpace = empty or only spaces
        if (UsesDefaultGateway && !string.IsNullOrWhiteSpace(DefaultGatewayInput))
        {
            // Something was typed but it is not an address -> say so and stop (still nothing changed)
            if (!IPv4Address.TryParse(DefaultGatewayInput, out var parsedGateway))
            {
                AddressError = "Type the default gateway as an IP address, e.g. 192.168.1.1 - or leave it empty for none.";
                return;
            }

            gateway = parsedGateway;   // a real address - keep it
        }

        // ----- 2. Send them to the device service - the address FIRST, because the gateway is checked against it -----

        // The service checks the rules (part 10.3 - e.g. not the network address), saves, marks "unsaved" and rings
        // InterfacesChanged - so the port cards are rebuilt before this line even returns
        var result = _deviceService.SetInterfaceIPv4Configuration(port.Interface, address, subnetMask);

        // Refused - show why (the address was NOT saved, and the gateway was not touched)
        if (!result.IsSuccess)
        {
            AddressError = result.ErrorMessage;
            return;
        }

        // On an end device: set the gateway, or remove it when the box was empty (gateway = null)
        if (UsesDefaultGateway)
        {
            result = _deviceService.SetInterfaceIPv4DefaultGateway(port.Interface, gateway);

            // Refused (e.g. not in the port's network). The address IS already saved - say so, so the user is not confused
            if (!result.IsSuccess)
            {
                AddressError = $"The address was saved, but not the default gateway: {result.ErrorMessage}";
                return;
            }
        }

        // ----- 3. Everything was saved - show the values exactly as saved (e.g. "192.168.001.010" -> "192.168.1.10") -----
        FillIPv4Inputs();   // also clears AddressError
    }

    // Purpose:       The "Clear" button of IP SETTINGS - removes the picked port's address, mask and gateway.
    // Precondition:  receives nothing (uses SelectedPort).
    // Postcondition: returns nothing - the port has no address and no gateway and the boxes are empty, or AddressError says why not.
    // [RelayCommand] = CommunityToolkit also makes "ClearIPv4Command", which the Clear button calls
    [RelayCommand]
    private void ClearIPv4()
    {
        // No port picked - nothing to clear
        if (SelectedPort is not { } port)
        {
            return;   // stop here
        }

        // The service removes the address AND the gateway (part 10.4), marks "unsaved" and rings InterfacesChanged
        var result = _deviceService.ClearInterfaceIPv4Configuration(port.Interface);

        if (result.IsSuccess)
        {
            FillIPv4Inputs();                     // the boxes become empty (the port has nothing now), the error is cleared
        }
        else
        {
            AddressError = result.ErrorMessage;   // show why it was refused
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
        _deviceService.InterfacesChanged -= OnInterfacesChanged;   // step 10
    }

    // Purpose:       Builds the port lines of the shown device again, keeping the port picked in IP SETTINGS.
    // Precondition:  receives nothing (uses the shown device and the picked port).
    // Postcondition: returns nothing - Interfaces holds one fresh line per port and IPv4Ports the ones that can have an
    //                address (both empty when no device is shown); SelectedPort is the same port as before if it is still
    //                there, otherwise the first one. The three boxes are filled again ONLY when the picked port changed.
    private void LoadInterfaces()
    {
        // Remember WHICH port was picked - the real port, not its line (the lines are about to be replaced by new ones).
        // "?." = no line picked -> null
        var previousPort = SelectedPort?.Interface;

        _isReloading = true;   // from here, changes of SelectedPort are only the reload - OnSelectedPortChanged ignores them

        Interfaces.Clear();    // throw away the old lines
        IPv4Ports.Clear();     // of both lists

        // "is not null" = a device IS shown - build its lines (when it is null, the lists simply stay empty)
        if (_device is not null)
        {
            // One new line per port, in the device's own order (DeviceInterfaceItem turns the port into text - parts 9.1, 10.7)
            foreach (var networkInterface in _device.Interfaces)
            {
                var item = new DeviceInterfaceItem(networkInterface);   // the port's line
                Interfaces.Add(item);                                   // every port goes into the PORTS list

                // Every port except the Console port goes into the IP SETTINGS list too
                if (networkInterface.InterfaceType != InterfaceType.Console)
                {
                    IPv4Ports.Add(item);
                }
            }
        }

        // Pick the same port again. FirstOrDefault(test) = the first line whose port is the remembered one, or null if
        // none is (another device, or it is gone); "?? IPv4Ports.FirstOrDefault()" = then the first line (or null if empty)
        SelectedPort = IPv4Ports.FirstOrDefault(p => p.Interface == previousPort) ?? IPv4Ports.FirstOrDefault();

        _isReloading = false;   // the reload is over

        // A DIFFERENT port is picked now (another device was opened, or the panel was closed) - show its values.
        // The SAME port -> leave the boxes alone, so what the user typed is not lost
        if (SelectedPort?.Interface != previousPort)
        {
            FillIPv4Inputs();
        }
    }

    // Purpose:       Fills the three IP boxes with the picked port's current values, and clears the IP error.
    // Precondition:  receives nothing (uses SelectedPort).
    // Postcondition: returns nothing - the boxes hold the port's address, mask and gateway as text ("" for each one it
    //                doesn't have, or when no port is picked), and AddressError is null.
    private void FillIPv4Inputs()
    {
        var networkInterface = SelectedPort?.Interface;   // the picked port, or null

        // "?." all along = if anything on the way is null (no port, no address, no gateway) the result is null;
        // "?? string.Empty" = then an empty box. ToString = the dotted text (parts 10.1, 10.2)
        AddressInput = networkInterface?.IPv4Configuration?.Address.ToString() ?? string.Empty;
        SubnetMaskInput = networkInterface?.IPv4Configuration?.SubnetMask.ToString() ?? string.Empty;
        DefaultGatewayInput = networkInterface?.IPv4DefaultGateway?.ToString() ?? string.Empty;

        AddressError = null;   // a new port - no old error
    }

    // Purpose:       Runs every time SelectedPort changes (CommunityToolkit calls it - "partial" = the generated
    //                property's setter has a hook with this exact name, and this is our body for it).
    // Precondition:  receives value - the newly picked line, or null.
    // Postcondition: returns nothing - when the USER picked a port, the boxes show its values; during a reload, nothing.
    partial void OnSelectedPortChanged(DeviceInterfaceItem? value)
    {
        // "!" = NOT: only when it is not the reload (LoadInterfaces decides by itself whether to fill the boxes)
        if (!_isReloading)
        {
            FillIPv4Inputs();
        }
    }

    // Purpose:       Step 10 - runs when a port's settings changed (its address, mask or gateway).
    // Precondition:  receives sender, e - the event's values (not used); called by IDeviceService.InterfacesChanged.
    // Postcondition: returns nothing - when the panel is open, its port lines were built again (the cards show the new IP).
    private void OnInterfacesChanged(object? sender, EventArgs e)
    {
        if (IsOpen)
        {
            LoadInterfaces();
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
