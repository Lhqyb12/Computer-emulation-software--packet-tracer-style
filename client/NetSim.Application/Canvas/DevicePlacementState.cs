using System;                      // brings in EventHandler, EventArgs, ArgumentNullException
using NetSim.Application.State;    // brings in IApplicationState - knows which project is open
using NetSim.Core.Devices;         // brings in DeviceType - the kind of device waiting to be placed

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// The class that does the work of IDevicePlacementState.
// "sealed" = no other class can inherit from it; ": IDevicePlacementState" = it does everything the interface lists
public sealed class DevicePlacementState : IDevicePlacementState
{
    // Purpose:       Builds the placement state and cancels it whenever the open project changes.
    // Precondition:  receives applicationState - the app's "what is open" state (passed in by the DI container; not null).
    // Postcondition: returns nothing (a constructor) - nothing is waiting, and the state listens to CurrentProjectChanged.
    public DevicePlacementState(IApplicationState applicationState)
    {
        // If nothing was passed in (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(applicationState);

        // "+=" = start listening: when another project is opened (or the project is closed), a device picked for the old
        // project must not stay waiting - so cancel it. "(_, _)" = the event's two values, not needed here
        applicationState.CurrentProjectChanged += (_, _) => Cancel();
    }

    // The device kind waiting to be placed, or null. "private set" = everyone can read it, only this class changes it
    public DeviceType? PendingDeviceType { get; private set; }

    // true when a kind is waiting. "=>" = computed every time it is read; "is not null" = there is a waiting kind
    public bool IsPlacing => PendingDeviceType is not null;

    // Raised every time PendingDeviceType changes. "?" = may have no listeners
    public event EventHandler? Changed;

    // Purpose:       Starts "place a device" mode for one device kind.
    // Precondition:  receives deviceType - the kind to place.
    // Postcondition: returns nothing - PendingDeviceType is the given kind and Changed was raised.
    public void Begin(DeviceType deviceType)
    {
        PendingDeviceType = deviceType;   // remember the kind that is waiting

        // Tell everyone who listens (the canvas draws the ghost, the library marks the card). "?." = only if someone listens
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Purpose:       Leaves "place a device" mode.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - PendingDeviceType is null; Changed was raised only if something was waiting.
    public void Cancel()
    {
        // Nothing is waiting - nothing changes, so don't raise the event for no reason
        if (PendingDeviceType is null)
        {
            return;   // stop here - nothing more to do
        }

        PendingDeviceType = null;   // nothing is waiting any more

        // Tell everyone who listens (the ghost disappears, the library un-selects its card)
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
