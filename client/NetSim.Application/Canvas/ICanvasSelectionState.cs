using System;                      // EventHandler
using System.Collections.Generic;  // IReadOnlySet<T>, IEnumerable<T>
using NetSim.Core.Common;          // EntityId

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Remembers what is selected on the canvas (step 4): devices AND cables, by their ids.
// Both kinds share one list because both have an EntityId - so "Delete" (and later the properties
// window) can work on whatever is selected. It empties itself when another network is opened.
// An interface = a list of what the class can do, without how
public interface ICanvasSelectionState
{
    // The ids of everything selected. A "set" = no id appears twice. Read-only
    IReadOnlySet<EntityId> SelectedIds { get; }

    // Raised every time the selection changes
    event EventHandler? SelectionChanged;

    // Purpose:       Answers whether one device / cable is selected.
    // Precondition:  none.
    // Postcondition: returns true / false. Nothing is changed.
    bool IsSelected(EntityId id);

    // Purpose:       Makes exactly these ids the selection (everything else is un-selected).
    // Precondition:  ids is not null.
    // Postcondition: SelectedIds holds exactly these ids; SelectionChanged was raised only if it differs.
    void Replace(IEnumerable<EntityId> ids);

    // Purpose:       Selects one id if it was not selected, or un-selects it if it was (Ctrl + click).
    // Precondition:  none.
    // Postcondition: the id's selected state flipped and SelectionChanged was raised.
    void Toggle(EntityId id);

    // Purpose:       Un-selects everything.
    // Precondition:  none.
    // Postcondition: SelectedIds is empty; SelectionChanged was raised only if something was selected.
    void Clear();
}
