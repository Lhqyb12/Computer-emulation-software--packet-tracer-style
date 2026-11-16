using System;                      // EventHandler, EventArgs, ArgumentNullException, ArgumentException
using System.Collections.Generic;  // List<T>, IReadOnlyList<T>
using NetSim.Application.State;    // IApplicationState - tells us when a different network is opened
using NetSim.Core.Common;          // EntityId

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The class that does the work of ICanvasItemsState.
// "sealed" = no other class can inherit from it
public sealed class CanvasItemsState : ICanvasItemsState
{
    // The real list of items, in drawing order (the last one is drawn on top)
    private readonly List<CanvasItem> _items = [];

    // Purpose:       Builds the empty canvas state and makes it empty itself whenever another network is opened.
    // Precondition:  applicationState is not null (the DI container passes it in).
    // Postcondition: Items is empty, and the state listens to CurrentNetworkChanged.
    public CanvasItemsState(IApplicationState applicationState)
    {
        // Stop right away with a clear message if nothing was passed in
        ArgumentNullException.ThrowIfNull(applicationState);

        // Change from the reference project: there the canvas emptied itself when the PROJECT changed.
        // Here it follows the NETWORK, because the positions belong to the network's devices - so
        // "Save As" (same network, new project) keeps the drawing, and a new or closed network clears it
        applicationState.CurrentNetworkChanged += (_, _) => Clear();
    }

    // All the items, read-only (whoever reads can't add or remove behind our back)
    public IReadOnlyList<CanvasItem> Items => _items.AsReadOnly();

    // Raised after every change
    public event EventHandler? ItemsChanged;

    // Purpose:       Finds the item of one device.
    // Precondition:  none.
    // Postcondition: returns the item with this id, or null. Nothing is changed.
    public CanvasItem? GetItem(EntityId id)
    {
        // Find the place of the item in the list (-1 = not there)
        var index = IndexOf(id);

        // Hand back the item, or null when it was not found ("? :" = if / else in one line)
        return index >= 0 ? _items[index] : null;
    }

    // Purpose:       Finds the top-most item under a world point.
    // Precondition:  worldPoint is a world point.
    // Postcondition: returns the top-most item that contains the point, or null. Nothing is changed.
    public CanvasItem? HitTest(CanvasPoint worldPoint)
    {
        // Go over the list from the END to the start: the last items are drawn on top,
        // so when two devices overlap, the one the user SEES wins
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            // Is the point inside this item?
            if (_items[i].Contains(worldPoint))
            {
                return _items[i];   // yes - this is the one that was clicked
            }
        }

        // No item under the point (the user clicked empty canvas)
        return null;
    }

    // Purpose:       Puts a new item on the canvas, on top of the others.
    // Precondition:  item is not null, and no item with the same id is on the canvas.
    // Postcondition: the item is last in Items and ItemsChanged was raised.
    //                Throws ArgumentException when an item with this id is already there.
    public void AddItem(CanvasItem item)
    {
        // null is not allowed - stop with a clear message
        ArgumentNullException.ThrowIfNull(item);

        // The same device can't be drawn twice - that is a bug in the caller, so stop loudly
        if (IndexOf(item.Id) >= 0)
        {
            throw new ArgumentException($"An item with id '{item.Id}' is already on the canvas.", nameof(item));
        }

        _items.Add(item);    // add it at the end = drawn on top
        RaiseChanged();      // tell the listeners
    }

    // Purpose:       Moves one item to a new place.
    // Precondition:  newPosition is the new top-left corner, as a world point.
    // Postcondition: true + ItemsChanged when it was moved; false and no change when the id is not found.
    public bool MoveItem(EntityId id, CanvasPoint newPosition)
    {
        // Find the item (null = not on the canvas)
        var item = GetItem(id);

        // Not found - nothing to move
        if (item is null)
        {
            return false;   // stop here and say "not found"
        }

        item.MoveTo(newPosition);   // put it at the new place
        RaiseChanged();             // tell the listeners
        return true;                // say "moved"
    }

    // Purpose:       Changes the text under one item.
    // Precondition:  newLabel is not empty.
    // Postcondition: true + ItemsChanged when it was renamed; false and no change when the id is not found.
    public bool RenameItem(EntityId id, string newLabel)
    {
        // Find the item (null = not on the canvas)
        var item = GetItem(id);

        // Not found - nothing to rename
        if (item is null)
        {
            return false;   // stop here and say "not found"
        }

        item.Rename(newLabel);   // change the text (it checks the text is not empty)
        RaiseChanged();          // tell the listeners
        return true;             // say "renamed"
    }

    // Purpose:       Takes one item off the canvas.
    // Precondition:  none.
    // Postcondition: true + ItemsChanged when it was removed; false and no change when the id is not found.
    public bool RemoveItem(EntityId id)
    {
        // Find the place of the item in the list (-1 = not there)
        var index = IndexOf(id);

        // Not found - nothing to remove
        if (index < 0)
        {
            return false;   // stop here and say "not found"
        }

        _items.RemoveAt(index);   // take it out of the list
        RaiseChanged();           // tell the listeners
        return true;              // say "removed"
    }

    // Purpose:       Empties the canvas (called when a different network is opened, or the network is closed).
    // Precondition:  none.
    // Postcondition: Items is empty. ItemsChanged was raised only if there was something to remove.
    private void Clear()
    {
        // Already empty - nothing changes, so don't raise the event for no reason
        if (_items.Count == 0)
        {
            return;   // stop here - nothing more to do
        }

        _items.Clear();   // remove everything
        RaiseChanged();   // tell the listeners
    }

    // Purpose:       Finds the place (index) of an item in the list by its id.
    // Precondition:  none.
    // Postcondition: returns the index (0 and up), or -1 when there is no item with this id. Nothing is changed.
    private int IndexOf(EntityId id) => _items.FindIndex(item => item.Id == id);   // the first item whose id matches

    // Purpose:       Tells everyone who listens that the items changed.
    // Precondition:  none.
    // Postcondition: ItemsChanged was raised (if anyone listens).
    private void RaiseChanged() => ItemsChanged?.Invoke(this, EventArgs.Empty);   // "?." = only if someone is listening
}
