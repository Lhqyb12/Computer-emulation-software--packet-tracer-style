using System;                      // EventHandler, EventArgs, ArgumentNullException
using NetSim.Application.State;    // IApplicationState - knows which project is open
using NetSim.Core.Devices;         // DeviceType

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The class that does the work of IDevicePlacementState.
// "sealed" = no other class can inherit from it
public sealed class DevicePlacementState : IDevicePlacementState
{
    // Purpose: Builds the placement state and cancels it whenever the open project changes.
    // Input:   applicationState - passed in by the DI container.
    // Output:  none (a constructor).
    public DevicePlacementState(IApplicationState applicationState)
    {
        // Stop right away with a clear message if nothing was passed in
        ArgumentNullException.ThrowIfNull(applicationState);

        // When another project is opened (or the project is closed), a device picked for the old
        // project must not stay waiting - so cancel it
        applicationState.CurrentProjectChanged += (_, _) => Cancel();
    }

    // The device kind waiting to be placed. "private set" = only this class can change it
    public DeviceType? PendingDeviceType { get; private set; }

    // true when there is a waiting kind. "=>" = computed every time it is read
    public bool IsPlacing => PendingDeviceType is not null;

    // Raised every time PendingDeviceType changes
    public event EventHandler? Changed;

    // Purpose: Starts "place a device" mode for one device kind.
    // Input:   deviceType - the kind to place.
    // Output:  nothing.
    public void Begin(DeviceType deviceType)
    {
        PendingDeviceType = deviceType;   // remember the kind

        // Tell everyone who listens. "?." = only if someone is listening
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Purpose: Leaves "place a device" mode.
    // Input:   nothing.
    // Output:  nothing.
    public void Cancel()
    {
        // Nothing is waiting - nothing changes, so don't raise the event for no reason
        if (PendingDeviceType is null)
        {
            return;   // stop here - nothing more to do
        }

        PendingDeviceType = null;   // nothing is waiting any more

        // Tell everyone who listens
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
