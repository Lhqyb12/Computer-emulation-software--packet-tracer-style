using System;                      // brings in EventHandler - the type of an event ("something happened")
using System.Collections.Generic;  // brings in IReadOnlyList<T> - a list that can be read but not changed
using NetSim.Core.Common;          // brings in EntityId - a device's unique id

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// Remembers WHERE every device of the open network sits on the canvas.
// It only remembers - it knows no rules. The rules (and "unsaved changes") are in DeviceService, which is the
// only class that should change it; the canvas screen only READS from it.
// It empties itself every time a different network is opened, so one project's devices never show up in another.
// "interface" = a list of WHAT a class can do, without HOW. Whoever uses it does not care which class is
// behind it - the DI container decides (CanvasItemsState)
public interface ICanvasItemsState
{
    // All the items, in drawing order: the LAST one is drawn on top. Read-only - nobody can add or remove behind our back
    IReadOnlyList<CanvasItem> Items { get; }

    // Raised after EVERY change (added, moved, renamed, removed, emptied), so the canvas knows to draw again.
    // "?" = there may be nobody listening (then it is null)
    event EventHandler? ItemsChanged;

    // Purpose:       Finds the item of one device.
    // Precondition:  receives id - the device's id.
    // Postcondition: returns the item with this id, or null when there is none. Nothing is changed.
    //                "CanvasItem?" = an item OR null
    CanvasItem? GetItem(EntityId id);

    // Purpose:       Finds the top-most item under a world point (the one the user clicked).
    // Precondition:  receives worldPoint - the point, as a world point.
    // Postcondition: returns the top-most item that contains the point, or null when the point is on empty canvas.
    CanvasItem? HitTest(CanvasPoint worldPoint);

    // Purpose:       Puts a new item on the canvas, on top of all the others.
    // Precondition:  receives item - the new item (not null, and no item with the same id is on the canvas yet).
    // Postcondition: returns nothing - the item is last in Items and ItemsChanged was raised.
    //                Throws ArgumentException when an item with this id is already there.
    void AddItem(CanvasItem item);

    // Purpose:       Moves one item to a new place.
    // Precondition:  receives id - which item; newPosition - its new top-left corner, as a world point.
    // Postcondition: returns true (and raises ItemsChanged) when the item was found and moved;
    //                returns false (and changes nothing) when there is no item with this id.
    bool MoveItem(EntityId id, CanvasPoint newPosition);

    // Purpose:       Changes the text under one item.
    // Precondition:  receives id - which item; newLabel - the new text (not empty).
    // Postcondition: returns true (and raises ItemsChanged) when the item was found and renamed;
    //                returns false (and changes nothing) when there is no item with this id.
    bool RenameItem(EntityId id, string newLabel);

    // Purpose:       Takes one item off the canvas.
    // Precondition:  receives id - which item.
    // Postcondition: returns true (and raises ItemsChanged) when the item was found and removed;
    //                returns false (and changes nothing) when there is no item with this id.
    bool RemoveItem(EntityId id);
}
