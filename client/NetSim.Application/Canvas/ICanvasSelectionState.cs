using System;
using System.Collections.Generic;
using NetSim.Core.Common;

namespace NetSim.Application.Canvas;

/// <summary>
/// Tracks which <see cref="CanvasItem"/>s (by id) are currently selected on the Network Canvas.
/// Singleton, mirroring <see cref="ICanvasState"/>. Deliberately separate from
/// <see cref="State.ISelectionState"/> (Phase 7): that type tracks a single selected
/// device/interface/connection for a future properties panel, and has no notion of a set - it
/// cannot represent "3 devices marquee-selected at once". This type is id-based (not tied to any
/// domain type) precisely so a later phase can select devices/connections through it too, once
/// they carry an <c>EntityId</c> and a canvas position - see docs/architecture/canvas-interaction.md.
/// </summary>
public interface ICanvasSelectionState
{
    IReadOnlySet<EntityId> SelectedIds { get; }

    event EventHandler? SelectionChanged;

    bool IsSelected(EntityId id);

    /// <summary>Replaces the entire selection with <paramref name="ids"/>. Empty clears it.</summary>
    void Replace(IEnumerable<EntityId> ids);

    /// <summary>Adds <paramref name="id"/> to the selection if absent, removes it if present.</summary>
    void Toggle(EntityId id);

    void Clear();
}
