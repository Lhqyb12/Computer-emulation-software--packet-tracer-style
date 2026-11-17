using System;                      // EventHandler, EventArgs, ArgumentNullException
using System.Collections.Generic;  // HashSet<T>, IReadOnlySet<T>, IEnumerable<T>
using NetSim.Application.State;    // IApplicationState - tells us when another network is opened
using NetSim.Core.Common;          // EntityId

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The class that does the work of ICanvasSelectionState.
// Change from the reference project: it empties itself when the NETWORK changes (not the project) -
// the same rule as CanvasItemsState and CanvasState.
// "sealed" = no other class can inherit from it
public sealed class CanvasSelectionState : ICanvasSelectionState
{
    // The selected ids. A HashSet = a list with no repeats that answers "is X in it?" very fast.
    // It is REPLACED (not changed) on every change, so a set handed out earlier never changes under its reader
    private HashSet<EntityId> _selectedIds = [];

    // Purpose:       Builds an empty selection that empties itself whenever another network is opened.
    // Precondition:  applicationState is not null (the DI container passes it in).
    // Postcondition: nothing is selected; the state listens to CurrentNetworkChanged.
    public CanvasSelectionState(IApplicationState applicationState)
    {
        // Stop right away with a clear message if nothing was passed in
        ArgumentNullException.ThrowIfNull(applicationState);

        // Another network = the old ids mean nothing any more - forget them
        applicationState.CurrentNetworkChanged += (_, _) => Clear();
    }

    // The selected ids, read-only
    public IReadOnlySet<EntityId> SelectedIds => _selectedIds;

    // Raised every time the selection changes
    public event EventHandler? SelectionChanged;

    // Purpose:       Answers whether one id is selected.
    // Precondition:  none.
    // Postcondition: returns true / false. Nothing is changed.
    public bool IsSelected(EntityId id) => _selectedIds.Contains(id);   // a fast look-up in the set

    // Purpose:       Makes exactly these ids the selection.
    // Precondition:  ids is not null.
    // Postcondition: SelectedIds holds exactly these ids; SelectionChanged was raised only if it differs.
    public void Replace(IEnumerable<EntityId> ids)
    {
        // null is not allowed - stop with a clear message
        ArgumentNullException.ThrowIfNull(ids);

        // The new set (a repeated id is kept once)
        var next = new HashSet<EntityId>(ids);

        // The same ids as now (in any order) - nothing changed, no event
        if (next.SetEquals(_selectedIds))
        {
            return;   // stop here
        }

        _selectedIds = next;                                // the new selection
        SelectionChanged?.Invoke(this, EventArgs.Empty);    // tell the listeners ("?." = only if someone listens)
    }

    // Purpose:       Flips one id: selects it, or un-selects it if it was selected (Ctrl + click).
    // Precondition:  none.
    // Postcondition: the id's selected state flipped and SelectionChanged was raised.
    public void Toggle(EntityId id)
    {
        // A copy of the current set, to change
        var next = new HashSet<EntityId>(_selectedIds);

        // Remove returns false when the id was not there - then add it instead
        if (!next.Remove(id))
        {
            next.Add(id);
        }

        _selectedIds = next;                                // the new selection
        SelectionChanged?.Invoke(this, EventArgs.Empty);    // tell the listeners
    }

    // Purpose:       Un-selects everything.
    // Precondition:  none.
    // Postcondition: nothing is selected; SelectionChanged was raised only if something was selected.
    public void Clear()
    {
        // Already empty - nothing changed, no event
        if (_selectedIds.Count == 0)
        {
            return;   // stop here
        }

        _selectedIds = [];                                  // an empty set
        SelectionChanged?.Invoke(this, EventArgs.Empty);    // tell the listeners
    }
}
