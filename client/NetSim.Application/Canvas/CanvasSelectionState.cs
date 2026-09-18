using System;
using System.Collections.Generic;
using NetSim.Application.State;
using NetSim.Core.Common;

namespace NetSim.Application.Canvas;

public sealed class CanvasSelectionState : ICanvasSelectionState
{
    private HashSet<EntityId> _selectedIds = [];

    public CanvasSelectionState(IApplicationState applicationState)
    {
        ArgumentNullException.ThrowIfNull(applicationState);

        applicationState.CurrentProjectChanged += (_, _) => Clear();
    }

    public IReadOnlySet<EntityId> SelectedIds => _selectedIds;

    public event EventHandler? SelectionChanged;

    public bool IsSelected(EntityId id) => _selectedIds.Contains(id);

    public void Replace(IEnumerable<EntityId> ids)
    {
        var next = new HashSet<EntityId>(ids);
        if (next.SetEquals(_selectedIds))
        {
            return;
        }

        _selectedIds = next;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle(EntityId id)
    {
        var next = new HashSet<EntityId>(_selectedIds);
        if (!next.Remove(id))
        {
            next.Add(id);
        }

        _selectedIds = next;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        _selectedIds = [];
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
