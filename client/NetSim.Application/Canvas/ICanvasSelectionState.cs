using System;                      // brings in EventHandler - the type of an event
using System.Collections.Generic;  // brings in IReadOnlySet<T> (a read-only set) and IEnumerable<T> (any list we can go over)
using NetSim.Core.Common;          // brings in EntityId - the unique id of a device or a cable

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// Remembers what is selected on the canvas (step 4): devices AND cables, by their ids.
// Both kinds share one set because both have an EntityId - so "Delete" (and later the properties window)
// can work on whatever is selected. It empties itself when another network is opened.
// "interface" = a list of WHAT the class can do, without HOW (CanvasSelectionState does the work)
public interface ICanvasSelectionState
{
    // The ids of everything selected. A "set" = no id appears twice. Read-only
    IReadOnlySet<EntityId> SelectedIds { get; }

    // Raised every time the selection changes
    event EventHandler? SelectionChanged;

    // Purpose:       Answers whether one device / cable is selected.
    // Precondition:  receives id - the device's or the cable's id.
    // Postcondition: returns true when it is selected, otherwise false. Nothing is changed.
    bool IsSelected(EntityId id);

    // Purpose:       Makes exactly these ids the selection (everything else is un-selected).
    // Precondition:  receives ids - the ids that should be selected (not null; may be empty).
    // Postcondition: returns nothing - SelectedIds holds exactly these ids; SelectionChanged was raised only if it differs.
    void Replace(IEnumerable<EntityId> ids);

    // Purpose:       Selects one id if it was not selected, or un-selects it if it was (Ctrl + click).
    // Precondition:  receives id - the device's or the cable's id.
    // Postcondition: returns nothing - the id's selected state flipped and SelectionChanged was raised.
    void Toggle(EntityId id);

    // Purpose:       Un-selects everything.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - SelectedIds is empty; SelectionChanged was raised only if something was selected.
    void Clear();
}
