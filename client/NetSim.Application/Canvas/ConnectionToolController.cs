using System;                        // EventHandler, EventArgs, ArgumentNullException
using System.Linq;                   // Any
using NetSim.Application.Services;   // IConnectionService - the one door for making cables
using NetSim.Application.State;      // IApplicationState - the open network
using NetSim.Core.Common;            // EntityId
using NetSim.Core.Devices;           // NetworkDevice
using NetSim.Core.Networking;        // NetworkInterface, ConnectionCompatibility

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The Connect tool (step 6): click one device, then a second device, and a cable is made between them.
// The cable goes into the FIRST FREE PORTS that fit each other on the two devices (e.g. a PC's Ethernet
// port and a switch's first free FastEthernet port), and its kind (copper / fiber / serial) is worked
// out from the two ports - like Packet Tracer's automatic cable.
// Choosing the cable kind and the exact ports by hand comes in step 7.
// Change from the reference project: there the user had to hit small "anchor" dots of single ports;
// here a click anywhere on a device is enough, as the plan for step 6 describes.
// It knows nothing about Avalonia - the canvas control calls it with world points.
// "sealed" = no other class can inherit from it
public sealed class ConnectionToolController
{
    private readonly IApplicationState _applicationState;     // the open network
    private readonly ICanvasItemsState _itemsState;           // where every device is (to find the clicked one)
    private readonly IConnectionService _connectionService;   // makes the cable (and marks "unsaved")

    // Purpose:       Builds the Connect tool.
    // Precondition:  applicationState, itemsState and connectionService are not null.
    // Postcondition: no device is picked yet and there is no message.
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

    // The first device picked (the cable starts there), or null
    public EntityId? PendingDeviceId { get; private set; }

    // The device under the mouse while the tool is on, or null (drawn highlighted)
    public EntityId? HoveredDeviceId { get; private set; }

    // The mouse position in the world - the "rubber band" line goes from the first device to here
    public CanvasPoint PointerWorldPosition { get; private set; }

    // A line of text for the user: what to do next, what was connected, or why it did not work. null = none
    public string? StatusMessage { get; private set; }

    // true when StatusMessage is a problem (shown in red), false when it is a hint / a success
    public bool IsStatusError { get; private set; }

    // Is a first device picked (waiting for the second click)
    public bool HasPendingDevice => PendingDeviceId is not null;

    // Raised after anything here changed, so the canvas redraws and the message updates
    public event EventHandler? Changed;

    // Purpose:       The mouse moved while the Connect tool is on - updates the rubber band and the highlight.
    // Precondition:  worldPoint is a world point.
    // Postcondition: PointerWorldPosition and HoveredDeviceId are up to date; Changed was raised.
    public void OnPointerMoved(CanvasPoint worldPoint)
    {
        PointerWorldPosition = worldPoint;                       // the end of the rubber band
        HoveredDeviceId = _itemsState.HitTest(worldPoint)?.Id;   // the device under the mouse ("?." = null if none)
        Raise();                                                 // redraw
    }

    // Purpose:       A left click while the Connect tool is on.
    // Precondition:  worldPoint is a world point.
    // Postcondition: - click on empty canvas: the pick (if any) is cancelled;
    //                - first click on a device: it is picked (if it has a free port), or a red message says why not;
    //                - a click on the picked device again: the pick is cancelled;
    //                - second click on another device: a cable was made (green-ish message), or a red message
    //                  says why not (then the first device stays picked, so another second device can be tried).
    public void OnClick(CanvasPoint worldPoint)
    {
        PointerWorldPosition = worldPoint;   // remember where

        // Which device was clicked (null = empty canvas)
        var item = _itemsState.HitTest(worldPoint);

        // Empty canvas - give up the cable that was being made
        if (item is null)
        {
            Cancel();
            return;   // stop here
        }

        // The open network (null = no project is open - nothing to connect)
        if (_applicationState.CurrentNetwork is not { } network)
        {
            return;   // stop here
        }

        // The device itself (null = it is gone)
        if (network.GetDevice(item.Id) is not { } device)
        {
            return;   // stop here - nothing to connect
        }

        // No first device yet - this click picks it
        if (PendingDeviceId is not { } pendingId)
        {
            PickFirstDevice(device);
            return;   // stop here
        }

        // The same device clicked again - a natural "never mind"
        if (pendingId == device.Id)
        {
            Cancel();
            return;   // stop here
        }

        // The first device, from the network (it may have been deleted meanwhile)
        if (network.GetDevice(pendingId) is not { } firstDevice)
        {
            Cancel();
            return;   // stop here
        }

        // Second click on another device - try to connect the two
        ConnectDevices(firstDevice, device);
    }

    // Purpose:       Gives up the cable that was being made (Escape, a click on empty canvas, leaving the tool).
    // Precondition:  none.
    // Postcondition: no device is picked and there is no message; Changed was raised if anything changed.
    public void Cancel()
    {
        // Nothing to cancel - stay quiet
        if (PendingDeviceId is null && StatusMessage is null)
        {
            return;   // stop here
        }

        PendingDeviceId = null;   // no first device
        SetStatus(null, isError: false);   // no message
        Raise();                  // redraw
    }

    // ----- Inside helpers -----

    // Purpose:       Picks the first device of a cable, if it has a free port.
    // Precondition:  device is in the open network.
    // Postcondition: PendingDeviceId is the device and a hint says what to do next - or a red message says it has no free port.
    private void PickFirstDevice(NetworkDevice device)
    {
        // A port is "available" when it is turned on and has no cable yet
        if (!device.Interfaces.Any(i => i.IsAvailable))
        {
            SetStatus($"{device.Name} has no free port.", isError: true);   // can't start a cable here
            Raise();
            return;   // stop here
        }

        PendingDeviceId = device.Id;   // the cable starts here
        SetStatus($"Now click the device to connect {device.Name} to (Esc to cancel).", isError: false);   // the next step
        Raise();
    }

    // Purpose:       Makes a cable between two devices, through the first free ports that fit each other.
    // Precondition:  first and second are two different devices of the open network.
    // Postcondition: success - the cable exists, the pick is over and a message says which ports were used;
    //                failure - no cable, a red message says why, and the first device stays picked.
    private void ConnectDevices(NetworkDevice first, NetworkDevice second)
    {
        // The second device must have a free port at all
        if (!second.Interfaces.Any(i => i.IsAvailable))
        {
            SetStatus($"{second.Name} has no free port.", isError: true);
            Raise();
            return;   // stop here - the first device stays picked
        }

        // The first pair of free ports that can take the same kind of cable (null = there is none)
        if (FindFreeCompatiblePair(first, second) is not { } pair)
        {
            SetStatus($"{first.Name} and {second.Name} have no free ports that fit each other.", isError: true);
            Raise();
            return;   // stop here - the first device stays picked
        }

        // Take the two ports out of the pair ("deconstruct" it into two variables)
        var (portA, portB) = pair;

        // The cable kind that fits both ports (copper / fiber / serial)
        var connectionType = ConnectionCompatibility.InferConnectionType(portA.InterfaceType, portB.InterfaceType);

        // Make the cable - through ConnectionService (checks the rules, marks "unsaved")
        var result = _connectionService.Connect(portA, portB, connectionType);

        // The pick is over either way
        PendingDeviceId = null;

        // Tell the user what happened
        if (result.IsSuccess)
        {
            SetStatus($"Connected {first.Name} {portA.ShortName} to {second.Name} {portB.ShortName}.", isError: false);
        }
        else
        {
            SetStatus(result.ErrorMessage, isError: true);
        }

        Raise();   // redraw (the new cable appears)
    }

    // Purpose:       Finds the first free port on each device that can take the same kind of cable.
    // Precondition:  a and b are not null.
    // Postcondition: returns the two ports, or null when there is no such pair. Nothing is changed.
    //                The ports are tried in the device's own order, so e.g. a router's Gigabit ports come
    //                before its Serial port.
    private static (NetworkInterface PortA, NetworkInterface PortB)? FindFreeCompatiblePair(NetworkDevice a, NetworkDevice b)
    {
        // Every free port of the first device...
        foreach (var portA in a.Interfaces.Where(i => i.IsAvailable))
        {
            // ...against every free port of the second device
            foreach (var portB in b.Interfaces.Where(i => i.IsAvailable))
            {
                // Do they share a cable kind? (the [Flags] check from step 1)
                if (ConnectionCompatibility.AreCompatible(portA, portB))
                {
                    return (portA, portB);   // found - the first pair that fits
                }
            }
        }

        // No pair fits
        return null;
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
