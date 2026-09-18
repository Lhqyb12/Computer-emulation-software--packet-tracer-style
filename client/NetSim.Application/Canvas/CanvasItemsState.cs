using System;
using System.Collections.Generic;
using System.Linq;
using NetSim.Application.State;
using NetSim.Core.Common;

namespace NetSim.Application.Canvas;

public sealed class CanvasItemsState : ICanvasItemsState
{
    private List<CanvasItem> _items;

    public CanvasItemsState(IApplicationState applicationState)
    {
        ArgumentNullException.ThrowIfNull(applicationState);

        _items = [];

        // Same reasoning as CanvasState: a project is a separate workspace, so switching
        // (create/open/close) starts with an empty canvas rather than carrying devices/items
        // over from the previous project.
        applicationState.CurrentProjectChanged += (_, _) => Clear();
    }

    public IReadOnlyList<CanvasItem> Items => _items;

    public event EventHandler? ItemsChanged;

    public CanvasItem? HitTest(CanvasPoint worldPoint)
    {
        // Reverse iteration: later items are drawn on top, so they should win hit-testing too.
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            if (_items[i].Contains(worldPoint))
            {
                return _items[i];
            }
        }

        return null;
    }

    public void MoveItem(EntityId id, CanvasPoint newPosition)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            return;
        }

        item.MoveTo(newPosition);
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddItem(CanvasItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _items.Add(item);
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RenameItem(EntityId id, string newLabel)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            return;
        }

        item.Rename(newLabel);
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }

        _items = [];
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }
}
