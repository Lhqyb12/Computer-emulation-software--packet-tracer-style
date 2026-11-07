using System;                  // EventHandler
using NetSim.Core.Devices;     // DeviceType

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Remembers whether the canvas is in "place a device" mode: the time between picking a device
// kind in the Device Library and clicking the canvas to actually create it.
// An interface = a list of what the class can do, without how. Whoever uses it doesn't care which
// class is behind it - the DI container decides
public interface IDevicePlacementState
{
    // The device kind waiting to be placed, or null when nothing is waiting.
    // "DeviceType?" = a DeviceType, or null
    DeviceType? PendingDeviceType { get; }

    // true while a device is waiting to be placed
    bool IsPlacing { get; }

    // Raised every time the waiting device changes (picked, replaced, or cancelled)
    event EventHandler? Changed;

    // Purpose: Starts "place a device" mode for one device kind (replaces any kind already waiting).
    // Input:   deviceType - the kind to place.
    // Output:  nothing.
    void Begin(DeviceType deviceType);

    // Purpose: Leaves "place a device" mode.
    // Input:   nothing.
    // Output:  nothing (does nothing if nothing was waiting).
    void Cancel();
}
