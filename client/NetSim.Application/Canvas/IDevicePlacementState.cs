using System;
using NetSim.Core.Devices;

namespace NetSim.Application.Canvas;

/// <summary>
/// Tracks whether the Network Canvas is currently in "place a device" mode - the state that
/// exists between the user picking a device type in the Device Library (Phase 11) and actually
/// clicking the canvas to create it. Singleton, mirroring <see cref="ICanvasState"/>/
/// <see cref="ICanvasItemsState"/>/<see cref="ICanvasSelectionState"/>: kept separate from those
/// (rather than folded into <see cref="CanvasTool"/>) because placement needs to carry a
/// <see cref="DeviceType"/> payload, not just be an on/off tool switch.
/// </summary>
public interface IDevicePlacementState
{
    /// <summary>The device type awaiting placement, or null when not in placement mode.</summary>
    DeviceType? PendingDeviceType { get; }

    bool IsPlacing { get; }

    event EventHandler? Changed;

    /// <summary>Enters placement mode for the given device type, replacing any placement already in progress.</summary>
    void Begin(DeviceType deviceType);

    /// <summary>Leaves placement mode. No-op if not currently placing.</summary>
    void Cancel();
}
