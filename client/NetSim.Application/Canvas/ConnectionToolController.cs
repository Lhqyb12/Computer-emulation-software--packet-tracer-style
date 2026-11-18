using System;                        // brings in EventHandler, EventArgs, ArgumentNullException
using System.Collections.Generic;    // brings in IReadOnlyList<T> - a list that can only be read
using System.Linq;                   // brings in Any (is there at least one...), Where (keep...), Select (turn each into...), FirstOrDefault, ToList
using NetSim.Application.Services;   // brings in IConnectionService - the one door for making cables
using NetSim.Application.State;      // brings in IApplicationState - the open network
using NetSim.Core.Common;            // brings in EntityId - a device's unique id
using NetSim.Core.Devices;           // brings in NetworkDevice - a device
using NetSim.Core.Networking;        // brings in NetworkInterface (a port), ConnectionType, ConnectionState, CableRules

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// The Connect tool (steps 6-7). It works in one of two ways, by the cable picked in the toolbar:
//   AUTOMATIC (step 6): click one device, then a second device - the first free ports that fit get the
//     RIGHT cable kind by themselves (CableRules.ChooseAutomatic), so the link always works.
//   A CHOSEN CABLE (step 7): click a device -> a list of its ports opens -> choose one; then click the
//     second device -> choose its port. Ports that can't take the cable are greyed out. A copper cable
//     of the wrong kind IS connected (it fits the plug) but its link stays down, with a red message
//     saying which cable was needed - like in Packet Tracer.
// It knows nothing about Avalonia - the canvas control calls it with world points, and the screen shows the
// port list from PortPickerOptions.
// "sealed" = no other class can inherit from it
public sealed class ConnectionToolController
{
    // The things this tool works with. "readonly" = set once in the constructor, never replaced
    private readonly IApplicationState _applicationState;     // the open network
    private readonly ICanvasItemsState _itemsState;           // where every device is (to find the clicked one)
    private readonly IConnectionService _connectionService;   // makes the cable (and marks "unsaved")

    private NetworkInterface? _pendingPort;   // chosen-cable mode: the FIRST end's port, once it was chosen (null = not yet)
    private EntityId? _pickerDeviceId;        // the device whose port list is open, or null when the list is closed

    // Purpose:       Builds the Connect tool.
    // Precondition:  receives applicationState, itemsState, connectionService - the three things it works with (none null).
    // Postcondition: returns nothing (a constructor) - Automatic cable, nothing picked, no list open, no message.
    public ConnectionToolController(IApplicationState applicationState, ICanvasItemsState itemsState, IConnectionService connectionService)
    {
        // If any of the three is missing (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(applicationState);
        ArgumentNullException.ThrowIfNull(itemsState);
        ArgumentNullException.ThrowIfNull(connectionService);

        _applicationState = applicationState;     // keep the open network
        _itemsState = itemsState;                 // keep the canvas positions
        _connectionService = connectionService;   // keep the cable rules
    }

    // The cable picked in the toolbar, or null = Automatic (step 7). "ConnectionType?" = a cable kind OR null
    public ConnectionType? SelectedCableType { get; private set; }

    // The first device picked (the cable starts there), or null when none is picked yet
    public EntityId? PendingDeviceId { get; private set; }

    // The device under the mouse while the tool is on, or null (the canvas draws it highlighted)
    public EntityId? HoveredDeviceId { get; private set; }

    // The mouse position in the world - the dashed "rubber band" line goes from the first device to here
    public CanvasPoint PointerWorldPosition { get; private set; }

    // A line of text for the user: what to do next, what was connected, or why it did not work. null = no message
    public string? StatusMessage { get; private set; }

    // true when StatusMessage is a problem (shown in red), false when it is a hint / a success (shown in blue)
    public bool IsStatusError { get; private set; }

    // Is a first device picked (waiting for the second one)? "=>" = computed on every read
    public bool HasPendingDevice => PendingDeviceId is not null;

    // ----- The "choose a port" list (step 7) -----

    // Is the port list open? (it is open exactly when it belongs to some device)
    public bool IsPortPickerOpen => _pickerDeviceId is not null;

    // The title of the list, e.g. "Switch0 - choose a port"
    public string? PortPickerTitle { get; private set; }

    // The lines of the list (empty when it is closed). "= []" = it starts as an empty list
    public IReadOnlyList<PortOption> PortPickerOptions { get; private set; } = [];

    // Where the list opens: the device's bottom-left corner, in the world (the screen turns it into pixels)
    public CanvasPoint PortPickerAnchor { get; private set; }

    // Raised after anything here changed, so the canvas redraws and the messages / list update
    public event EventHandler? Changed;

    // Purpose:       Picks the cable for the next connections (the toolbar's cable list).
    // Precondition:  receives cableType - the cable kind, or null for Automatic.
    // Postcondition: returns nothing - SelectedCableType is the new cable; a half-made cable was given up; Changed was raised.
    public void SelectCable(ConnectionType? cableType)
    {
        ResetPick();                     // a cable that was half made with the OLD kind is given up
        SetStatus(null, isError: false); // no old message ("isError: false" = a named argument, for clarity)
        SelectedCableType = cableType;   // keep the new kind (null = Automatic)
        Raise();                         // the hint and the toolbar update
    }

    // Purpose:       The mouse moved while the Connect tool is on - updates the rubber band and the highlight.
    // Precondition:  receives worldPoint - the mouse, as a world point.
    // Postcondition: returns nothing - PointerWorldPosition and HoveredDeviceId are up to date; Changed was raised.
    public void OnPointerMoved(CanvasPoint worldPoint)
    {
        PointerWorldPosition = worldPoint;                       // the end of the rubber band follows the mouse
        HoveredDeviceId = _itemsState.HitTest(worldPoint)?.Id;   // the device under the mouse ("?." = null when nothing is there)
        Raise();                                                 // redraw
    }

    // Purpose:       A left click on the canvas while the Connect tool is on.
    // Precondition:  receives worldPoint - where the click was, as a world point.
    // Postcondition: returns nothing. What happened depends on the case:
    //                - the port list was open: it closes (a first port already chosen stays chosen);
    //                - empty canvas: everything is given up;
    //                - the first device clicked again: everything is given up;
    //                - AUTOMATIC: the first click picks the device, the second click makes the cable;
    //                - CHOSEN CABLE: the device's port list opens (or a red message says it has no fitting port).
    public void OnClick(CanvasPoint worldPoint)
    {
        PointerWorldPosition = worldPoint;   // remember where the click was

        // A click outside the open list closes it (without choosing a port)
        if (IsPortPickerOpen)
        {
            ClosePortPicker();   // close the list
            Raise();             // redraw / update
            return;              // stop here - this click only closed the list
        }

        // Which device was clicked? (CanvasItemsState.HitTest; null = empty canvas)
        var item = _itemsState.HitTest(worldPoint);

        // Empty canvas - give up the cable that was being made
        if (item is null)
        {
            Cancel();
            return;   // stop here
        }

        // The open network, and the clicked device in it. "||" = OR: if there is no network OR the device is not found,
        // stop. Otherwise both are kept: "network" and "device"
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

        // A CHOSEN cable ("is { } cableType" = it is not null - keep it): open the port list of this device
        if (SelectedCableType is { } cableType)
        {
            OpenPortPicker(device, cableType, item);
            return;   // stop here
        }

        // AUTOMATIC, and no first device yet - this click picks it
        if (PendingDeviceId is not { } pendingId)
        {
            PickFirstDevice(device);
            return;   // stop here
        }

        // AUTOMATIC, second click. Find the first device again - it may have been deleted meanwhile
        if (network.GetDevice(pendingId) is not { } firstDevice)
        {
            Cancel();   // it is gone - give up
            return;     // stop here
        }

        // Connect the two devices automatically
        ConnectAutomatically(firstDevice, device);
    }

    // Purpose:       A port was chosen in the open list (step 7).
    // Precondition:  receives portName - the full name of the clicked line (e.g. "FastEthernet0/1").
    // Postcondition: returns nothing. First end: the port is remembered and the user is asked for the second device.
    //                Second end: the cable was made (or a red message says why not). The list is closed.
    //                A greyed-out port, or no open list, changes nothing.
    public void ChoosePort(string portName)
    {
        // Three things must be true, otherwise there is nothing to choose from:
        //   the list is open (keep its device id), a cable is picked (keep it), and the device is still in the network
        if (_pickerDeviceId is not { } deviceId
            || SelectedCableType is not { } cableType
            || _applicationState.CurrentNetwork?.GetDevice(deviceId) is not { } device)
        {
            return;   // stop here
        }

        // The line that was clicked. FirstOrDefault = the first line whose Name matches, or null when there is none
        var option = PortPickerOptions.FirstOrDefault(o => o.Name == portName);

        // No such line, or a greyed-out one ("!" = NOT selectable) - ignore the click
        if (option is null || !option.IsSelectable)
        {
            return;   // stop here
        }

        // The port itself, on the device (found by its name)
        var port = device.Interfaces.FirstOrDefault(i => i.Name == portName);
        if (port is null)
        {
            return;   // stop here - it is gone
        }

        ClosePortPicker();   // the choice is made - close the list

        // No first end yet - this port is it
        if (_pendingPort is null)
        {
            _pendingPort = port;           // remember the port
            PendingDeviceId = device.Id;   // and its device (for the highlight and the rubber band)
            // A blue hint: what to do next. port.ShortName = e.g. "Fa0/1"
            SetStatus($"Now click the device to connect {device.Name} {port.ShortName} to (Esc to cancel).", isError: false);
            Raise();                       // update the screen
            return;                        // stop here
        }

        // This is the second end - make the cable with the chosen kind
        MakeCable(_pendingPort, port, cableType);
    }

    // Purpose:       Gives up the cable that was being made and closes the list (Escape, empty canvas, leaving the tool).
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - nothing is picked, no list is open, no message; Changed was raised if anything changed.
    public void Cancel()
    {
        // Nothing to cancel (no first device, no list, no message) - stay quiet, don't redraw for nothing
        if (PendingDeviceId is null && !IsPortPickerOpen && StatusMessage is null)
        {
            return;   // stop here
        }

        ResetPick();                       // no first end, no list
        SetStatus(null, isError: false);   // no message
        Raise();                           // redraw / update
    }

    // ----- Inside helpers -----

    // Purpose:       AUTOMATIC: picks the first device of a cable, if it has a free port.
    // Precondition:  receives device - the clicked device (in the open network).
    // Postcondition: returns nothing - PendingDeviceId is the device and a blue hint says what to do next,
    //                or a red message says it has no free port.
    private void PickFirstDevice(NetworkDevice device)
    {
        // "Available" = turned on and with no cable yet. "!" + Any = "is there NOT even one available port?"
        if (!device.Interfaces.Any(i => i.IsAvailable))
        {
            SetStatus($"{device.Name} has no free port.", isError: true);   // a red message
            Raise();
            return;   // stop here
        }

        PendingDeviceId = device.Id;   // the cable starts at this device
        SetStatus($"Now click the device to connect {device.Name} to (Esc to cancel).", isError: false);   // a blue hint
        Raise();
    }

    // Purpose:       AUTOMATIC: connects two devices through the first free ports that some cable kind fits,
    //                with the right kind for them.
    // Precondition:  receives first, second - two different devices of the open network.
    // Postcondition: returns nothing. Success: the cable exists (its link is up) and the message names the ports and the
    //                cable. Failure: a red message, and the first device stays picked (so another second device can be tried).
    private void ConnectAutomatically(NetworkDevice first, NetworkDevice second)
    {
        // The second device must have a free port at all
        if (!second.Interfaces.Any(i => i.IsAvailable))
        {
            SetStatus($"{second.Name} has no free port.", isError: true);
            Raise();
            return;   // stop here - the first device stays picked
        }

        // Every free port of the first device (Where = keep only the available ones)...
        foreach (var portA in first.Interfaces.Where(i => i.IsAvailable))
        {
            // ...against every free port of the second device, in the devices' own order (so a router's Gigabit ports
            // come before its Serial port)
            foreach (var portB in second.Interfaces.Where(i => i.IsAvailable))
            {
                // Which cable "Automatic" picks for these two ports (null = none fits both). "is { } cableType" = found one
                if (CableRules.ChooseAutomatic(portA, portB) is { } cableType)
                {
                    MakeCable(portA, portB, cableType);   // found - make the cable
                    return;                               // and stop - the first pair that fits wins
                }
            }
        }

        // We tried every pair and no cable fits any of them (e.g. only a router's Serial port is free, and the other side is a PC)
        SetStatus($"{first.Name} and {second.Name} have no free ports that fit each other.", isError: true);
        Raise();
    }

    // Purpose:       CHOSEN CABLE: opens the port list of a device, every port marked can / can't.
    // Precondition:  receives device - the clicked device (in the open network); cableType - the chosen cable;
    //                item - the device's canvas item (to know where to open the list).
    // Postcondition: returns nothing - the list is open with a blue hint, or (when no port can take the cable) a red
    //                message and no list.
    private void OpenPortPicker(NetworkDevice device, ConnectionType cableType, CanvasItem item)
    {
        // One line per port, in the device's own order: Select turns each port into a PortOption (DescribePort, below);
        // ToList keeps the lines in a list
        var options = device.Interfaces.Select(port => DescribePort(port, cableType)).ToList();

        // No line is selectable - say so in red instead of opening a list that is all grey
        if (!options.Any(o => o.IsSelectable))
        {
            // CableRules.DisplayName = the cable's screen name, e.g. "Serial"
            SetStatus($"{device.Name} has no free port for a {CableRules.DisplayName(cableType)} cable.", isError: true);
            Raise();
            return;   // stop here (a first end that was already chosen stays chosen)
        }

        _pickerDeviceId = device.Id;                         // the list belongs to this device (so the list is now "open")
        PortPickerTitle = $"{device.Name} - choose a port";  // its title
        PortPickerOptions = options;                         // its lines
        // Where it opens: the device's bottom-left corner = its left X, and its top Y + its height
        PortPickerAnchor = new CanvasPoint(item.Position.X, item.Position.Y + item.Size.Height);
        // A blue hint
        SetStatus($"Choose a port on {device.Name} for the {CableRules.DisplayName(cableType)} cable.", isError: false);
        Raise();
    }

    // Purpose:       Builds one line of the port list.
    // Precondition:  receives port - the port (not null); cableType - the chosen cable.
    // Postcondition: returns a PortOption saying whether the port can be chosen, and why / why not.
    // "static" = it doesn't use this object's fields, only what it receives
    private static PortOption DescribePort(NetworkInterface port, ConnectionType cableType)
    {
        // Already has a cable -> greyed out (the 3rd value, false = not selectable), "connected"
        if (port.IsConnected)
        {
            return new PortOption(port.Name, port.ShortName, false, "connected");
        }

        // Turned off by the user -> greyed out, "turned off"
        if (!port.IsEnabled)
        {
            return new PortOption(port.Name, port.ShortName, false, "turned off");
        }

        // The wrong plug for this cable (CableRules.CanPlug; "out _" = we don't need the reason here) -> greyed out
        if (!CableRules.CanPlug(cableType, port, out _))
        {
            return new PortOption(port.Name, port.ShortName, false, $"can't take {CableRules.DisplayName(cableType)}");
        }

        // Free, turned on, and the right plug -> it can be chosen (true)
        return new PortOption(port.Name, port.ShortName, true, "free");
    }

    // Purpose:       Makes a cable between two ports through ConnectionService, and reports how it went.
    // Precondition:  receives a, b - the two ports (in the open network); cableType - the cable kind to use.
    // Postcondition: returns nothing. Success with a working link -> a blue message; success with a link that is down
    //                (the wrong copper kind) -> a red message naming the cable that was needed; failure -> a red message.
    //                Either way the pick is over.
    private void MakeCable(NetworkInterface a, NetworkInterface b, ConnectionType cableType)
    {
        // Make it - ConnectionService checks the rules, marks "unsaved" and tells the canvas. It hands back a result
        var result = _connectionService.Connect(a, b, cableType);

        ResetPick();   // the pick is over either way

        // The words "X Fa0/1 to Y G0/0 with a Copper Straight-Through cable", used in both messages below
        var what = $"{a.Device.Name} {a.ShortName} to {b.Device.Name} {b.ShortName} with a {CableRules.DisplayName(cableType)} cable";

        if (!result.IsSuccess)
        {
            SetStatus(result.ErrorMessage, isError: true);              // NOT connected - show why, in red
        }
        // result.Value = the new cable; "?." = if it is null the whole thing is null; State = its link state (set by Network)
        else if (result.Value?.State == ConnectionState.Up)
        {
            SetStatus($"Connected {what}.", isError: false);            // connected AND working - blue
        }
        else
        {
            // Connected, but the link is down - ask the rules why ("out var reason" = the sentence they fill in)
            CableRules.WillLinkWork(cableType, a, b, out var reason);
            SetStatus($"Connected {what}, but the link is down: {reason}", isError: true);   // red, with the needed cable
        }

        Raise();   // redraw (the new cable appears) and show the message
    }

    // Purpose:       Closes the port list (without choosing).
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - no list is open (a first end that was chosen stays chosen). Not raised - the caller raises.
    private void ClosePortPicker()
    {
        _pickerDeviceId = null;      // the list belongs to no device = it is closed
        PortPickerTitle = null;      // no title
        PortPickerOptions = [];      // no lines
    }

    // Purpose:       Forgets everything about the cable being made.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - no first device, no first port, no list. Not raised - the caller raises.
    private void ResetPick()
    {
        PendingDeviceId = null;   // no first device
        _pendingPort = null;      // no first port
        ClosePortPicker();        // no list
    }

    // Purpose:       Sets the message line.
    // Precondition:  receives message - the text (null = no message); isError - true = red, false = blue.
    // Postcondition: returns nothing - StatusMessage and IsStatusError are set (not raised - the caller raises once at the end).
    private void SetStatus(string? message, bool isError)
    {
        StatusMessage = message;   // the text
        IsStatusError = isError;   // red or blue
    }

    // Purpose:       Tells the listeners that something changed.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - Changed was raised (if anyone listens; "?." = only then).
    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
