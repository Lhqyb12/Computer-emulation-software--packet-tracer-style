using System;                      // EventHandler
using System.Collections.Generic;  // IReadOnlyList<T>
using NetSim.Core.Common;          // EntityId

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Remembers WHERE every device of the open network sits on the canvas.
// It only remembers - it knows no rules. The rules (and "unsaved changes") are in DeviceService,
// which is the only class that should change it; the canvas screen only READS from it.
// It empties itself every time a different network is opened, so one project's devices never
// show up in another project.
// An interface = a list of what the class can do, without how
public interface ICanvasItemsState
{
    // All the items, in drawing order: the last one is drawn on top. Read-only
    IReadOnlyList<CanvasItem> Items { get; }

    // Raised after every change (added, moved, renamed, removed, emptied), so the canvas redraws
    event EventHandler? ItemsChanged;

    // Purpose:       Finds the item of one device.
    // Precondition:  none.
    // Postcondition: returns the item with this id, or null when there is none. Nothing is changed.
    CanvasItem? GetItem(EntityId id);

    // Purpose:       Finds the top-most item under a world point (the one the user clicked).
    // Precondition:  worldPoint is a world point.
    // Postcondition: returns the top-most item that contains the point, or null. Nothing is changed.
    CanvasItem? HitTest(CanvasPoint worldPoint);

    // Purpose:       Puts a new item on the canvas, on top of the others.
    // Precondition:  item is not null, and no item with the same id is on the canvas.
    // Postcondition: the item is last in Items and ItemsChanged was raised.
    //                Throws ArgumentException when an item with this id is already there.
    void AddItem(CanvasItem item);

    // Purpose:       Moves one item to a new place.
    // Precondition:  newPosition is the new top-left corner, as a world point.
    // Postcondition: returns true and raises ItemsChanged when the item was found and moved;
    //                returns false (and changes nothing) when there is no item with this id.
    bool MoveItem(EntityId id, CanvasPoint newPosition);

    // Purpose:       Changes the text under one item.
    // Precondition:  newLabel is not empty.
    // Postcondition: returns true and raises ItemsChanged when the item was found and renamed;
    //                returns false (and changes nothing) when there is no item with this id.
    bool RenameItem(EntityId id, string newLabel);

    // Purpose:       Takes one item off the canvas.
    // Precondition:  none.
    // Postcondition: returns true and raises ItemsChanged when the item was found and removed;
    //                returns false (and changes nothing) when there is no item with this id.
    bool RemoveItem(EntityId id);
}
