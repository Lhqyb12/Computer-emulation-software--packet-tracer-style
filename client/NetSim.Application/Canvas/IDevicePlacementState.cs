using System;                  // brings in EventHandler - the type of an event
using NetSim.Core.Devices;     // brings in DeviceType - the kind of device waiting to be placed

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// Remembers whether the canvas is in "place a device" mode: the time between picking a device kind in the
// Device Library and clicking the canvas (or dropping on it) to actually create it.
// "interface" = a list of WHAT the class can do, without HOW. Whoever uses it doesn't care which class is behind
// it - the DI container decides (DevicePlacementState)
public interface IDevicePlacementState
{
    // The device kind waiting to be placed, or null when nothing is waiting. "DeviceType?" = a DeviceType OR null
    DeviceType? PendingDeviceType { get; }

    // true while a device is waiting to be placed
    bool IsPlacing { get; }

    // Raised every time the waiting device changes (picked, replaced, or cancelled)
    event EventHandler? Changed;

    // Purpose:       Starts "place a device" mode for one device kind (replaces any kind already waiting).
    // Precondition:  receives deviceType - the kind to place (PC, Switch...).
    // Postcondition: returns nothing - PendingDeviceType is the given kind and Changed was raised.
    void Begin(DeviceType deviceType);

    // Purpose:       Leaves "place a device" mode.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - PendingDeviceType is null; Changed was raised only if something was waiting.
    void Cancel();
}
