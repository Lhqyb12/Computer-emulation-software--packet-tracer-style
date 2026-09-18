using System;
using System.Collections.Generic;
using NetSim.Core.Common;

namespace NetSim.Application.Canvas;

/// <summary>
/// Owns the set of <see cref="CanvasItem"/>s placed on the Network Canvas. Singleton, mirroring
/// <see cref="ICanvasState"/>/<see cref="State.ISelectionState"/> - see
/// docs/architecture/canvas-interaction.md. Starts empty and is cleared whenever the current
/// project changes, the same way <see cref="ICanvasState"/> resets viewport/grid/tool - a new
/// project starts from a clean workspace. Phase 9 seeded a fixed set of placeholder items here
/// (there was no device placement yet); Phase 11 (Device Library) is the real content those stood
/// in for - see <see cref="AddItem"/>.
/// </summary>
public interface ICanvasItemsState
{
    IReadOnlyList<CanvasItem> Items { get; }

    event EventHandler? ItemsChanged;

    /// <summary>Topmost item (last in z-order) whose bounds contain <paramref name="worldPoint"/>, or null.</summary>
    CanvasItem? HitTest(CanvasPoint worldPoint);

    /// <summary>Moves the item with the given id to <paramref name="newPosition"/> (world coordinates). No-op if the id is not found.</summary>
    void MoveItem(EntityId id, CanvasPoint newPosition);

    /// <summary>Adds a new item (e.g. a just-placed device) to the canvas.</summary>
    void AddItem(CanvasItem item);

    /// <summary>Updates the label of the item with the given id (e.g. after a device rename via
    /// the Device Properties panel). No-op if the id is not found.</summary>
    void RenameItem(EntityId id, string newLabel);
}
