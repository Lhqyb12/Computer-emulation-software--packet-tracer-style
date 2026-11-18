using System;                      // brings in EventHandler, EventArgs, ArgumentNullException, ArgumentException
using System.Collections.Generic;  // brings in List<T> (a growing list) and IReadOnlyList<T> (a read-only view of it)
using NetSim.Application.State;    // brings in IApplicationState - it tells us when a different network is opened
using NetSim.Core.Common;          // brings in EntityId - a device's unique id

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// The class that does the work of ICanvasItemsState: the real list of everything on the canvas.
// "sealed" = no other class can inherit from it.
// ": ICanvasItemsState" = it promises to do everything the interface lists
public sealed class CanvasItemsState : ICanvasItemsState
{
    // The real list of items, in drawing order (the last one is drawn on top).
    // "private"  = only this class can see it.
    // "readonly" = this variable always points to the SAME list (its contents change, the list is never replaced).
    // "[]"       = start with an empty list
    private readonly List<CanvasItem> _items = [];

    // Purpose:       Builds the empty canvas state, and makes it empty itself whenever another network is opened.
    // Precondition:  receives applicationState - the app's "what is open" state (the DI container passes it in; not null).
    // Postcondition: returns nothing (a constructor) - Items is empty and the state listens to CurrentNetworkChanged.
    public CanvasItemsState(IApplicationState applicationState)
    {
        // If nothing was passed in (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(applicationState);

        // "+=" = start LISTENING to the event: from now on, every time a different network is opened (or the network
        // is closed), the small function on the right runs. "(_, _)" = the event gives two values (who raised it,
        // and extra details) - "_" means we don't need them. "=> Clear()" = what runs: empty the canvas.
        // Change from the reference project: there the canvas emptied itself when the PROJECT changed. Here it follows
        // the NETWORK, because the positions belong to the network's devices - so "Save As" (same network, new
        // project) keeps the drawing, and a new or closed network clears it
        applicationState.CurrentNetworkChanged += (_, _) => Clear();
    }

    // All the items, read-only. "=>" = computed on every read. AsReadOnly() wraps the real list so whoever
    // reads it can look at the items but can't add or remove any
    public IReadOnlyList<CanvasItem> Items => _items.AsReadOnly();

    // The event raised after every change. "?" = may have no listeners (null)
    public event EventHandler? ItemsChanged;

    // Purpose:       Finds the item of one device.
    // Precondition:  receives id - the device's id.
    // Postcondition: returns the item with this id, or null when there is none. Nothing is changed.
    public CanvasItem? GetItem(EntityId id)
    {
        // Find the item's place (index) in the list; IndexOf (below) returns -1 when it is not there
        var index = IndexOf(id);

        // "a ? b : c" = if a then b, otherwise c: a place of 0 or more = found -> hand back the item at that place;
        // -1 = not found -> hand back null
        return index >= 0 ? _items[index] : null;
    }

    // Purpose:       Finds the top-most item under a world point.
    // Precondition:  receives worldPoint - the point to check, as a world point.
    // Postcondition: returns the top-most item that contains the point, or null when nothing is there. Nothing is changed.
    public CanvasItem? HitTest(CanvasPoint worldPoint)
    {
        // Go over the list from the END to the start ("i--" = one step back each time).
        // Why from the end: the last items are drawn ON TOP, so when two devices overlap, the one the user SEES wins.
        // "_items.Count - 1" = the last place in the list (places start at 0)
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            // Is the point inside this item's box? (CanvasItem.Contains)
            if (_items[i].Contains(worldPoint))
            {
                return _items[i];   // yes - this is the one that was clicked; stop and hand it back
            }
        }

        // We went over every item and none contains the point - the user clicked empty canvas
        return null;
    }

    // Purpose:       Puts a new item on the canvas, on top of the others.
    // Precondition:  receives item - the new item (not null; no item with the same id is on the canvas yet).
    // Postcondition: returns nothing - the item is last in Items and ItemsChanged was raised.
    //                Throws ArgumentException when an item with this id is already there.
    public void AddItem(CanvasItem item)
    {
        // If no item was passed in (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(item);

        // The same device must not be drawn twice - IndexOf >= 0 means an item with this id is already in the list.
        // That would be a bug in the caller, so we stop loudly instead of hiding it
        if (IndexOf(item.Id) >= 0)
        {
            // "$" lets us put the id inside the text; nameof(item) = "item", the name of the bad parameter
            throw new ArgumentException($"An item with id '{item.Id}' is already on the canvas.", nameof(item));
        }

        _items.Add(item);    // add it at the END of the list = it is drawn on top of everything
        RaiseChanged();      // tell the listeners (the canvas view model) that the items changed
    }

    // Purpose:       Moves one item to a new place.
    // Precondition:  receives id - which item; newPosition - its new top-left corner, as a world point.
    // Postcondition: returns true (and raises ItemsChanged) when it was moved; false (no change) when the id is not found.
    public bool MoveItem(EntityId id, CanvasPoint newPosition)
    {
        // Find the item (null = it is not on the canvas)
        var item = GetItem(id);

        // "is null" = there is no such item - nothing to move
        if (item is null)
        {
            return false;   // stop here and answer "not found"
        }

        item.MoveTo(newPosition);   // put the item at the new place (CanvasItem.MoveTo)
        RaiseChanged();             // tell the listeners
        return true;                // answer "moved"
    }

    // Purpose:       Changes the text under one item.
    // Precondition:  receives id - which item; newLabel - the new text (not empty).
    // Postcondition: returns true (and raises ItemsChanged) when it was renamed; false (no change) when the id is not found.
    public bool RenameItem(EntityId id, string newLabel)
    {
        // Find the item (null = it is not on the canvas)
        var item = GetItem(id);

        // No such item - nothing to rename
        if (item is null)
        {
            return false;   // stop here and answer "not found"
        }

        item.Rename(newLabel);   // change the text (CanvasItem.Rename also checks the text is not empty)
        RaiseChanged();          // tell the listeners
        return true;             // answer "renamed"
    }

    // Purpose:       Takes one item off the canvas.
    // Precondition:  receives id - which item.
    // Postcondition: returns true (and raises ItemsChanged) when it was removed; false (no change) when the id is not found.
    public bool RemoveItem(EntityId id)
    {
        // Find the item's place in the list (-1 = not there)
        var index = IndexOf(id);

        // "< 0" = -1 = not found - nothing to remove
        if (index < 0)
        {
            return false;   // stop here and answer "not found"
        }

        _items.RemoveAt(index);   // take the item out of the list by its place
        RaiseChanged();           // tell the listeners
        return true;              // answer "removed"
    }

    // Purpose:       Empties the canvas (called when a different network is opened, or the network is closed).
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - Items is empty; ItemsChanged was raised only if there was something to remove.
    private void Clear()
    {
        // Already empty - nothing changes, so don't raise the event for no reason (the canvas would redraw for nothing)
        if (_items.Count == 0)
        {
            return;   // stop here - nothing more to do
        }

        _items.Clear();   // remove every item from the list
        RaiseChanged();   // tell the listeners
    }

    // Purpose:       Finds the place (index) of an item in the list by its id.
    // Precondition:  receives id - the device's id.
    // Postcondition: returns the place (0 and up), or -1 when there is no item with this id. Nothing is changed.
    // FindIndex goes over the list and returns the place of the first item for which the test is true.
    // "item => item.Id == id" = the test: "the item whose Id equals the id we look for"
    private int IndexOf(EntityId id) => _items.FindIndex(item => item.Id == id);

    // Purpose:       Tells everyone who listens that the items changed.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - ItemsChanged was raised (if anyone listens).
    // "?." = only if someone is listening (otherwise nothing happens and nothing crashes).
    // Invoke(this, EventArgs.Empty) = raise the event: "this" = who raised it (this state), EventArgs.Empty = no extra details.
    // A separate function, so the same line is not written in five places
    private void RaiseChanged() => ItemsChanged?.Invoke(this, EventArgs.Empty);
}
