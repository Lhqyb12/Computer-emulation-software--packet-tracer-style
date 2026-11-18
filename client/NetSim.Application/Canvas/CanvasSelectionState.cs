using System;                      // brings in EventHandler, EventArgs, ArgumentNullException
using System.Collections.Generic;  // brings in HashSet<T> (a set with no repeats), IReadOnlySet<T>, IEnumerable<T>
using NetSim.Application.State;    // brings in IApplicationState - it tells us when another network is opened
using NetSim.Core.Common;          // brings in EntityId - the unique id of a device or a cable

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// The class that does the work of ICanvasSelectionState.
// Change from the reference project: it empties itself when the NETWORK changes (not the project) -
// the same rule as CanvasItemsState and CanvasState.
// "sealed" = no other class can inherit from it; ": ICanvasSelectionState" = it does everything the interface lists
public sealed class CanvasSelectionState : ICanvasSelectionState
{
    // The selected ids. A HashSet = a collection with no repeats that answers "is X in it?" very fast.
    // It is REPLACED (not changed in place) on every change, so a set handed out earlier never changes under its reader.
    // "[]" = start with an empty set
    private HashSet<EntityId> _selectedIds = [];

    // Purpose:       Builds an empty selection that empties itself whenever another network is opened.
    // Precondition:  receives applicationState - the app's "what is open" state (passed in by the DI container; not null).
    // Postcondition: returns nothing (a constructor) - nothing is selected; the state listens to CurrentNetworkChanged.
    public CanvasSelectionState(IApplicationState applicationState)
    {
        // If nothing was passed in (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(applicationState);

        // "+=" = start listening: another network = the old ids mean nothing any more - forget them (Clear)
        applicationState.CurrentNetworkChanged += (_, _) => Clear();
    }

    // The selected ids, read-only. "=>" = computed on every read: hand out the current set
    public IReadOnlySet<EntityId> SelectedIds => _selectedIds;

    // The event raised every time the selection changes
    public event EventHandler? SelectionChanged;

    // Purpose:       Answers whether one id is selected.
    // Precondition:  receives id - the device's or the cable's id.
    // Postcondition: returns true when it is in the set, otherwise false. Nothing is changed.
    public bool IsSelected(EntityId id) => _selectedIds.Contains(id);   // Contains = a fast look-up in the set

    // Purpose:       Makes exactly these ids the selection.
    // Precondition:  receives ids - the ids that should be selected (not null).
    // Postcondition: returns nothing - SelectedIds holds exactly these ids; SelectionChanged was raised only if it differs.
    public void Replace(IEnumerable<EntityId> ids)
    {
        // If nothing was passed in (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(ids);

        // Build the new set from the ids (an id given twice is kept once)
        var next = new HashSet<EntityId>(ids);

        // SetEquals = "the same ids, in any order?" - if so nothing changed, so no event
        if (next.SetEquals(_selectedIds))
        {
            return;   // stop here
        }

        _selectedIds = next;                                // the new set is the selection now
        SelectionChanged?.Invoke(this, EventArgs.Empty);    // tell the listeners ("?." = only if someone listens)
    }

    // Purpose:       Flips one id: selects it, or un-selects it if it was selected (Ctrl + click).
    // Precondition:  receives id - the device's or the cable's id.
    // Postcondition: returns nothing - the id's selected state flipped and SelectionChanged was raised.
    public void Toggle(EntityId id)
    {
        // A COPY of the current set, to change (the old set is not touched)
        var next = new HashSet<EntityId>(_selectedIds);

        // Remove(id) takes the id out and answers true; if the id was NOT there it answers false - "!" = NOT,
        // so in that case we add it instead
        if (!next.Remove(id))
        {
            next.Add(id);
        }

        _selectedIds = next;                                // the changed copy is the selection now
        SelectionChanged?.Invoke(this, EventArgs.Empty);    // tell the listeners
    }

    // Purpose:       Un-selects everything.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - nothing is selected; SelectionChanged was raised only if something was selected.
    public void Clear()
    {
        // Already empty - nothing changes, so no event
        if (_selectedIds.Count == 0)
        {
            return;   // stop here
        }

        _selectedIds = [];                                  // a new, empty set
        SelectionChanged?.Invoke(this, EventArgs.Empty);    // tell the listeners
    }
}
