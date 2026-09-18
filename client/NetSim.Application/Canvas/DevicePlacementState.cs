using System;
using NetSim.Application.State;
using NetSim.Core.Devices;

namespace NetSim.Application.Canvas;

public sealed class DevicePlacementState : IDevicePlacementState
{
    public DevicePlacementState(IApplicationState applicationState)
    {
        ArgumentNullException.ThrowIfNull(applicationState);

        // A project switch should never leave a stale placement armed against the new project's
        // (empty) canvas - same reasoning as CanvasState/CanvasItemsState/CanvasSelectionState.
        applicationState.CurrentProjectChanged += (_, _) => Cancel();
    }

    public DeviceType? PendingDeviceType { get; private set; }

    public bool IsPlacing => PendingDeviceType is not null;

    public event EventHandler? Changed;

    public void Begin(DeviceType deviceType)
    {
        PendingDeviceType = deviceType;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Cancel()
    {
        if (PendingDeviceType is null)
        {
            return;
        }

        PendingDeviceType = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
