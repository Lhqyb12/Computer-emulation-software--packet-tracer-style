using System;                        // EventHandler, EventArgs, ArgumentNullException
using System.Collections.Generic;    // IReadOnlyList<T>
using System.Linq;                   // Any, Where, Select, FirstOrDefault, ToList
using NetSim.Application.Services;   // IConnectionService - the one door for making cables
using NetSim.Application.State;      // IApplicationState - the open network
using NetSim.Core.Common;            // EntityId
using NetSim.Core.Devices;           // NetworkDevice
using NetSim.Core.Networking;        // NetworkInterface, ConnectionType, ConnectionState, CableRules

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The Connect tool (steps 6-7). It works in one of two ways, by the cable picked in the toolbar:
//   AUTOMATIC (step 6): click one device, then a second device - the first free ports that fit get the
//     RIGHT cable kind by themselves (CableRules.ChooseAutomatic), so the link always works.
//   A CHOSEN CABLE (step 7): click a device -> a list of its ports opens -> choose one; then click the
//     second device -> choose its port. Ports that can't take the cable are greyed out. A copper cable
//     of the wrong kind IS connected (it fits the plug) but its link stays down, with a red message
//     saying which cable was needed - like in Packet Tracer.
// It knows nothing about Avalonia - the canvas control calls it with world points, and the screen shows
// the port list from PortPickerOptions.
// "sealed" = no other class can inherit from it
public sealed class ConnectionToolController
{
    private readonly IApplicationState _applicationState;     // the open network
    private readonly ICanvasItemsState _itemsState;           // where every device is (to find the clicked one)
    private readonly IConnectionService _connectionService;   // makes the cable (and marks "unsaved")

    private NetworkInterface? _pendingPort;   // chosen-cable mode: the first end's port, once it was chosen
    private EntityId? _pickerDeviceId;        // the device whose port list is open, or null when it is closed

    // Purpose:       Builds the Connect tool.
    // Precondition:  applicationState, itemsState and connectionService are not null.
    // Postcondition: Automatic cable, nothing picked, no list open, no message.
    public ConnectionToolController(IApplicationState applicationState, ICanvasItemsState itemsState, IConnectionService connectionService)
    {
        // Stop right away with a clear message if something was not passed in
        ArgumentNullException.ThrowIfNull(applicationState);
        ArgumentNullException.ThrowIfNull(itemsState);
        ArgumentNullException.ThrowIfNull(connectionService);

        _applicationState = applicationState;     // keep the open network
        _itemsState = itemsState;                 // keep the canvas positions
        _connectionService = connectionService;   // keep the cable rules
    }

    // The cable picked in the toolbar, or null = Automatic (step 7)
    public ConnectionType? SelectedCableType { get; private set; }

    // The first device picked (the cable starts there), or null
    public EntityId? PendingDeviceId { get; private set; }

    // The device under the mouse while the tool is on, or null (drawn highlighted)
    public EntityId? HoveredDeviceId { get; private set; }

    // The mouse position in the world - the dashed "rubber band" goes from the first device to here
    public CanvasPoint PointerWorldPosition { get; private set; }

    // A line of text for the user: what to do next, what was connected, or why it did not work. null = none
    public string? StatusMessage { get; private set; }

    // true when StatusMessage is a problem (shown in red), false when it is a hint / a success (blue)
    public bool IsStatusError { get; private set; }

    // Is a first device picked (waiting for the second one)
    public bool HasPendingDevice => PendingDeviceId is not null;

    // ----- The "choose a port" list (step 7) -----

    // Is the port list open
    public bool IsPortPickerOpen => _pickerDeviceId is not null;

    // The title of the list, e.g. "Switch0 - choose a port"
    public string? PortPickerTitle { get; private set; }

    // The lines of the list (empty when it is closed). "[]" = an empty list
    public IReadOnlyList<PortOption> PortPickerOptions { get; private set; } = [];

    // Where the list opens: the device's bottom-left corner, in the world (the screen turns it into pixels)
    public CanvasPoint PortPickerAnchor { get; private set; }

    // Raised after anything here changed, so the canvas redraws and the messages / list update
    public event EventHandler? Changed;

    // Purpose:       Picks the cable for the next connections (the toolbar's cable list).
    // Precondition:  none (null = Automatic).
    // Postcondition: SelectedCableType is the given cable; a half-made cable is given up; Changed was raised.
    public void SelectCable(ConnectionType? cableType)
    {
        ResetPick();                    // a cable that was half made with the old kind is given up
        SetStatus(null, isError: false);// no old message
        SelectedCableType = cableType;  // the new kind (null = Automatic)
        Raise();                        // the hint and the toolbar update
    }

    // Purpose:       The mouse moved while the Connect tool is on - updates the rubber band and the highlight.
    // Precondition:  worldPoint is a world point.
    // Postcondition: PointerWorldPosition and HoveredDeviceId are up to date; Changed was raised.
    public void OnPointerMoved(CanvasPoint worldPoint)
    {
        PointerWorldPosition = worldPoint;                       // the end of the rubber band
        HoveredDeviceId = _itemsState.HitTest(worldPoint)?.Id;   // the device under the mouse ("?." = null if none)
        Raise();                                                 // redraw
    }

    // Purpose:       A left click on the canvas while the Connect tool is on.
    // Precondition:  worldPoint is a world point.
    // Postcondition: - the port list was open: it closes (a first port already chosen stays chosen);
    //                - empty canvas: everything is given up;
    //                - the first device clicked again: everything is given up;
    //                - AUTOMATIC: first click picks the device, second click makes the cable;
    //                - CHOSEN CABLE: the device's port list opens (or a red message says it has no fitting port).
    public void OnClick(CanvasPoint worldPoint)
    {
        PointerWorldPosition = worldPoint;   // remember where

        // A click outside the open list closes it (without choosing)
        if (IsPortPickerOpen)
        {
            ClosePortPicker();
            Raise();
            return;   // stop here
        }

        // Which device was clicked (null = empty canvas)
        var item = _itemsState.HitTest(worldPoint);

        // Empty canvas - give up the cable that was being made
        if (item is null)
        {
            Cancel();
            return;   // stop here
        }

        // The open network and the clicked device (stop when there is none)
        if (_applicationState.CurrentNetwork is not { } network || network.GetDevice(item.Id) is not { } device)
        {
            return;   // stop here - nothing to connect
        }

        // The first device clicked again - a natural "never mind"
        if (PendingDeviceId == device.Id)
        {
            Cancel();
            return;   // stop here
        }

        // A chosen cable: open the port list of this device
        if (SelectedCableType is { } cableType)
        {
            OpenPortPicker(device, cableType, item);
            return;   // stop here
        }

        // AUTOMATIC: no first device yet - this click picks it
        if (PendingDeviceId is not { } pendingId)
        {
            PickFirstDevice(device);
            return;   // stop here
        }

        // AUTOMATIC: second click - connect the two (the first may have been deleted meanwhile)
        if (network.GetDevice(pendingId) is not { } firstDevice)
        {
            Cancel();
            return;   // stop here
        }

        ConnectAutomatically(firstDevice, device);
    }

    // Purpose:       A port was chosen in the open list (step 7).
    // Precondition:  the list is open; portName is the full name of one of its lines.
    // Postcondition: first end - the port is remembered and the user is asked for the second device;
    //                second end - the cable was made (or a red message says why not); the list is closed.
    //                A greyed-out port, or no list, changes nothing.
    public void ChoosePort(string portName)
    {
        // The list must be open, a cable picked, the network and the device still there
        if (_pickerDeviceId is not { } deviceId
            || SelectedCableType is not { } cableType
            || _applicationState.CurrentNetwork?.GetDevice(deviceId) is not { } device)
        {
            return;   // stop here - nothing to choose from
        }

        // The line that was clicked - only a selectable one counts
        var option = PortPickerOptions.FirstOrDefault(o => o.Name == portName);
        if (option is null || !option.IsSelectable)
        {
            return;   // stop here - a greyed-out port
        }

        // The port itself
        var port = device.Interfaces.FirstOrDefault(i => i.Name == portName);
        if (port is null)
        {
            return;   // stop here - it is gone
        }

        ClosePortPicker();   // the choice is made

        // No first end yet - this port is it
        if (_pendingPort is null)
        {
            _pendingPort = port;           // remember the port
            PendingDeviceId = device.Id;   // and its device (for the highlight and the rubber band)
            SetStatus($"Now click the device to connect {device.Name} {port.ShortName} to (Esc to cancel).", isError: false);
            Raise();
            return;   // stop here
        }

        // The second end - make the cable with the chosen kind
        MakeCable(_pendingPort, port, cableType);
    }

    // Purpose:       Gives up the cable that was being made and closes the list (Escape, empty canvas, leaving the tool).
    // Precondition:  none.
    // Postcondition: nothing is picked, no list is open, no message; Changed was raised if anything changed.
    public void Cancel()
    {
        // Nothing to cancel - stay quiet
        if (PendingDeviceId is null && !IsPortPickerOpen && StatusMessage is null)
        {
            return;   // stop here
        }

        ResetPick();                       // no first end, no list
        SetStatus(null, isError: false);   // no message
        Raise();                           // redraw
    }

    // ----- Inside helpers -----

    // Purpose:       AUTOMATIC: picks the first device of a cable, if it has a free port.
    // Precondition:  device is in the open network.
    // Postcondition: PendingDeviceId is the device and a hint says what to do next - or a red message says it has no free port.
    private void PickFirstDevice(NetworkDevice device)
    {
        // "Available" = turned on and with no cable yet
        if (!device.Interfaces.Any(i => i.IsAvailable))
        {
            SetStatus($"{device.Name} has no free port.", isError: true);
            Raise();
            return;   // stop here
        }

        PendingDeviceId = device.Id;   // the cable starts here
        SetStatus($"Now click the device to connect {device.Name} to (Esc to cancel).", isError: false);
        Raise();
    }

    // Purpose:       AUTOMATIC: connects two devices through the first free ports that some cable kind fits,
    //                with the right kind for them.
    // Precondition:  first and second are two different devices of the open network.
    // Postcondition: success - the cable exists (its link is up) and the message names the ports and the cable;
    //                failure - a red message, and the first device stays picked.
    private void ConnectAutomatically(NetworkDevice first, NetworkDevice second)
    {
        // The second device must have a free port at all
        if (!second.Interfaces.Any(i => i.IsAvailable))
        {
            SetStatus($"{second.Name} has no free port.", isError: true);
            Raise();
            return;   // stop here - the first device stays picked
        }

        // Every free port of the first device against every free port of the second, in the devices' own order:
        // the first pair that some cable fits wins, with the cable "Automatic" picks for it
        foreach (var portA in first.Interfaces.Where(i => i.IsAvailable))
        {
            foreach (var portB in second.Interfaces.Where(i => i.IsAvailable))
            {
                if (CableRules.ChooseAutomatic(portA, portB) is { } cableType)
                {
                    MakeCable(portA, portB, cableType);   // found - make it
                    return;                               // done
                }
            }
        }

        // No pair fits any cable (e.g. only a router's Serial port is free, and the other side is a PC)
        SetStatus($"{first.Name} and {second.Name} have no free ports that fit each other.", isError: true);
        Raise();
    }

    // Purpose:       CHOSEN CABLE: opens the port list of a device, every port marked can / can't.
    // Precondition:  device is in the open network; item is its canvas item.
    // Postcondition: the list is open (with a hint) - or, when no port can take the cable, a red message
    //                and no list.
    private void OpenPortPicker(NetworkDevice device, ConnectionType cableType, CanvasItem item)
    {
        // One line per port, in the device's own order
        var options = device.Interfaces.Select(port => DescribePort(port, cableType)).ToList();

        // No port can take this cable - say so instead of opening an all-grey list
        if (!options.Any(o => o.IsSelectable))
        {
            SetStatus($"{device.Name} has no free port for a {CableRules.DisplayName(cableType)} cable.", isError: true);
            Raise();
            return;   // stop here (a first end that was chosen stays chosen)
        }

        _pickerDeviceId = device.Id;                                                // the list belongs to this device
        PortPickerTitle = $"{device.Name} - choose a port";                         // its title
        PortPickerOptions = options;                                                // its lines
        PortPickerAnchor = new CanvasPoint(item.Position.X, item.Position.Y + item.Size.Height);   // under the device
        SetStatus($"Choose a port on {device.Name} for the {CableRules.DisplayName(cableType)} cable.", isError: false);
        Raise();
    }

    // Purpose:       Builds one line of the port list.
    // Precondition:  port is not null.
    // Postcondition: the line says whether the port can be chosen, and why / why not. Nothing is changed.
    private static PortOption DescribePort(NetworkInterface port, ConnectionType cableType)
    {
        // Already has a cable
        if (port.IsConnected)
        {
            return new PortOption(port.Name, port.ShortName, false, "connected");
        }

        // Turned off by the user
        if (!port.IsEnabled)
        {
            return new PortOption(port.Name, port.ShortName, false, "turned off");
        }

        // The wrong plug for this cable
        if (!CableRules.CanPlug(cableType, port, out _))
        {
            return new PortOption(port.Name, port.ShortName, false, $"can't take {CableRules.DisplayName(cableType)}");
        }

        // Free, on, and the right plug
        return new PortOption(port.Name, port.ShortName, true, "free");
    }

    // Purpose:       Makes a cable between two ports through ConnectionService, and reports how it went.
    // Precondition:  a and b are two ports of the open network.
    // Postcondition: success with a working link - a blue message; success with a link that is down (the wrong
    //                copper kind) - a red message naming the cable that was needed; failure - a red message.
    //                Either way the pick is over.
    private void MakeCable(NetworkInterface a, NetworkInterface b, ConnectionType cableType)
    {
        // Make it - ConnectionService checks the rules, marks "unsaved", and tells the canvas
        var result = _connectionService.Connect(a, b, cableType);

        ResetPick();   // the pick is over either way

        // The words for "X Fa0/1 to Y G0/0 with a Copper Straight-Through cable"
        var what = $"{a.Device.Name} {a.ShortName} to {b.Device.Name} {b.ShortName} with a {CableRules.DisplayName(cableType)} cable";

        if (!result.IsSuccess)
        {
            SetStatus(result.ErrorMessage, isError: true);              // not connected - why
        }
        else if (result.Value?.State == ConnectionState.Up)
        {
            SetStatus($"Connected {what}.", isError: false);            // connected and working
        }
        else
        {
            // Connected, but the link is down - say which cable was needed ("out var" = the reason)
            CableRules.WillLinkWork(cableType, a, b, out var reason);
            SetStatus($"Connected {what}, but the link is down: {reason}", isError: true);
        }

        Raise();   // redraw (the new cable appears)
    }

    // Purpose:       Closes the port list (without choosing).
    // Precondition:  none.
    // Postcondition: no list is open (a first end that was chosen stays chosen). Not raised - the caller raises.
    private void ClosePortPicker()
    {
        _pickerDeviceId = null;      // no list
        PortPickerTitle = null;      // no title
        PortPickerOptions = [];      // no lines
    }

    // Purpose:       Forgets everything about the cable being made.
    // Precondition:  none.
    // Postcondition: no first device, no first port, no list. Not raised - the caller raises.
    private void ResetPick()
    {
        PendingDeviceId = null;   // no first device
        _pendingPort = null;      // no first port
        ClosePortPicker();        // no list
    }

    // Purpose:       Sets the message line.
    // Precondition:  none.
    // Postcondition: StatusMessage and IsStatusError are set (not raised - the caller raises once at the end).
    private void SetStatus(string? message, bool isError)
    {
        StatusMessage = message;   // the text (null = none)
        IsStatusError = isError;   // red or not
    }

    // Purpose:       Tells the listeners that something changed.
    // Precondition:  none.
    // Postcondition: Changed was raised (if anyone listens).
    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
