using NetSim.Core.Common;
using NetSim.Core.Devices;

namespace NetSim.Application.Canvas;

/// <summary>
/// A selectable, draggable object positioned on the Network Canvas in world coordinates. Phase 9
/// (Canvas Interaction) introduced this as generic interaction-layer scaffolding (id/label/
/// position/size only) to give selection/multi-selection/drag/marquee something real to operate
/// on before any domain object carried a canvas position. Phase 11 (Device Library) is the "later
/// phase" that doc anticipated: <paramref name="deviceType"/> optionally links an item to the real
/// <c>NetworkDevice</c> it represents (<see cref="Id"/> matches the device's own <c>EntityId</c>)
/// so it can be rendered with the right icon - see docs/architecture/device-model.md, "Visual
/// identity". Null means a plain, device-less item; nothing in <see cref="ICanvasItemsState"/>/
/// <see cref="CanvasInteractionController"/> ever requires it, since both only depend on
/// <see cref="Id"/>/<see cref="Position"/>/<see cref="Size"/>/<see cref="Contains"/>/
/// <see cref="Intersects"/>.
/// </summary>
public sealed class CanvasItem(EntityId id, string label, CanvasPoint position, CanvasSize size, DeviceType? deviceType = null)
{
    /// <summary>The item's top-left corner, in world coordinates.</summary>
    public EntityId Id { get; } = id;

    public string Label { get; private set; } = label;

    public CanvasPoint Position { get; private set; } = position;

    public CanvasSize Size { get; } = size;

    /// <summary>The device type this item represents, or null for a plain/generic item.</summary>
    public DeviceType? DeviceType { get; } = deviceType;

    public void MoveTo(CanvasPoint position) => Position = position;

    /// <summary>Updates the label shown on the canvas - used to keep this item in sync when the
    /// underlying <c>NetworkDevice</c> it represents is renamed (see the Device Properties panel,
    /// <see cref="ICanvasItemsState.RenameItem"/>).</summary>
    public void Rename(string label) => Label = label;

    /// <summary>Whether the given world point falls within this item's bounds (used for click hit-testing).</summary>
    public bool Contains(CanvasPoint worldPoint) =>
        worldPoint.X >= Position.X && worldPoint.X <= Position.X + Size.Width &&
        worldPoint.Y >= Position.Y && worldPoint.Y <= Position.Y + Size.Height;

    /// <summary>Whether this item's bounds overlap the given world-space rectangle (used for marquee selection).</summary>
    public bool Intersects(CanvasPoint min, CanvasPoint max) =>
        Position.X < max.X && Position.X + Size.Width > min.X &&
        Position.Y < max.Y && Position.Y + Size.Height > min.Y;
}
