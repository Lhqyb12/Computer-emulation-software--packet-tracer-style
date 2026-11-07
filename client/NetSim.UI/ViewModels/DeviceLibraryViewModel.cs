using System;                               // EventArgs, ArgumentNullException, StringComparison
using System.Collections.Generic;           // IReadOnlyList, IEnumerable
using System.Collections.ObjectModel;       // ObservableCollection - a list that tells the screen when items change
using System.Linq;                          // Where, FirstOrDefault
using CommunityToolkit.Mvvm.ComponentModel; // [ObservableProperty] - writes the public property for a private field
using CommunityToolkit.Mvvm.Input;          // [RelayCommand] - turns a method into a Command a button can call
using NetSim.Application.Canvas;            // IDevicePlacementState - the device waiting to be placed
using NetSim.UI.Common;                     // ViewModelBase - the base class of every screen
using NetSim.UI.Devices;                    // DeviceCatalog, DeviceLibraryEntry, DeviceCategory

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The Device Library panel: lets the user browse and search the device cards and pick one to place
// on the canvas. Picking a card does NOT create a device - it only starts "place a device" mode
// (IDevicePlacementState). The device itself will be created later, when the user clicks the canvas.
// So this class has no network logic at all - only searching, filtering, and passing the pick on
public partial class DeviceLibraryViewModel : ViewModelBase
{
    private readonly IDevicePlacementState _placementState;      // the device waiting to be placed
    private readonly IReadOnlyList<DeviceLibraryEntry> _allDevices;   // every card, before filtering

    // true while we copy the selection FROM the placement state - so the change we make does not
    // bounce back and start placement again (see OnSelectedEntryChanged and OnPlacementStateChanged)
    private bool _isSyncingSelectionFromPlacementState;

    // The text in the search box. [ObservableProperty] = a public SearchText property that tells the screen when it changes
    [ObservableProperty]
    private string _searchText = string.Empty;

    // The filter button that is selected ("All", "End Devices", ...)
    [ObservableProperty]
    private CategoryFilterOption _selectedCategoryOption;

    // The card that is selected, or null if none is
    [ObservableProperty]
    private DeviceLibraryEntry? _selectedEntry;

    // true = the panel is folded into a thin strip
    [ObservableProperty]
    private bool _isCollapsed;

    // Purpose: The constructor the DI container uses - takes every card from DeviceCatalog.
    // Input:   placementState - passed in by the DI container.
    // Output:  none (a constructor).
    // ": this(...)" = run the second constructor below, with DeviceCatalog.AllDevices as the cards
    public DeviceLibraryViewModel(IDevicePlacementState placementState)
        : this(placementState, DeviceCatalog.AllDevices)
    {
    }

    // Purpose: Builds the panel with a given list of cards (a test can pass a small fixed list).
    // Input:   placementState - the device waiting to be placed; allDevices - the cards to show.
    // Output:  none (a constructor).
    public DeviceLibraryViewModel(IDevicePlacementState placementState, IReadOnlyList<DeviceLibraryEntry> allDevices)
    {
        // Stop right away with a clear message if something was not passed in
        ArgumentNullException.ThrowIfNull(placementState);
        ArgumentNullException.ThrowIfNull(allDevices);

        _placementState = placementState;   // keep the placement state
        _allDevices = allDevices;           // keep all the cards

        Categories = BuildCategories();             // the filter buttons
        _selectedCategoryOption = Categories[0];    // start with "All" selected

        // When the waiting device changes somewhere else, update the selected card
        _placementState.Changed += OnPlacementStateChanged;

        // Fill the visible list for the first time
        Refilter();
    }

    // The filter buttons: "All" plus one for every group - even groups with no devices yet
    public IReadOnlyList<CategoryFilterOption> Categories { get; }

    // The cards shown right now, after search and filter. "[]" = start with an empty list
    public ObservableCollection<DeviceLibraryEntry> FilteredDevices { get; } = [];

    // Is there at least one card to show? false = show the "nothing found" message instead
    public bool HasResults => FilteredDevices.Count > 0;

    // Is the user searching? (is there any text in the search box, other than spaces)
    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    // The message when no card is shown - a different one for "your search found nothing"
    // and for "this group has no devices yet". "a ? b : c" = if a then b, otherwise c
    public string EmptyStateMessage => IsSearching
        ? "No devices found. Try another search."
        : "No devices available in this category yet.";

    // Purpose: The collapse / expand button - folds or opens the panel.
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void ToggleCollapse() => IsCollapsed = !IsCollapsed;

    // Purpose: Runs by itself every time SearchText changes (CommunityToolkit calls it) - filters again.
    // Input:   value - the new search text.
    // Output:  nothing.
    partial void OnSearchTextChanged(string value) => Refilter();

    // Purpose: Runs by itself every time another filter button is selected - filters again.
    // Input:   value - the new filter button.
    // Output:  nothing.
    partial void OnSelectedCategoryOptionChanged(CategoryFilterOption value) => Refilter();

    // Purpose: Runs by itself every time the selected card changes - passes the pick on to the placement state.
    // Input:   value - the new selected card, or null.
    // Output:  nothing.
    partial void OnSelectedEntryChanged(DeviceLibraryEntry? value)
    {
        // The change came FROM the placement state - don't send it back (that would be a loop)
        if (_isSyncingSelectionFromPlacementState)
        {
            return;   // stop here - nothing more to do
        }

        // No card is selected - nothing is waiting to be placed
        if (value is null)
        {
            _placementState.Cancel();
        }
        // A card is selected - its device kind is now waiting to be placed
        else
        {
            _placementState.Begin(value.DeviceType);
        }
    }

    // Purpose: Runs when the placement state changes from somewhere else (later: Escape on the canvas,
    //          or a device was placed) - selects the matching card, or none.
    // Input:   sender - who raised the event; e - extra information (none here).
    // Output:  nothing.
    private void OnPlacementStateChanged(object? sender, EventArgs e)
    {
        _isSyncingSelectionFromPlacementState = true;   // "this change comes from the placement state"

        // "is { } deviceType" = if there is a waiting kind, call it deviceType.
        // Then select the first card of that kind, or no card at all
        SelectedEntry = _placementState.PendingDeviceType is { } deviceType
            ? _allDevices.FirstOrDefault(d => d.DeviceType == deviceType)
            : null;

        _isSyncingSelectionFromPlacementState = false;   // back to normal
    }

    // Purpose: Fills the visible list again, by the selected filter button and the search text.
    // Input:   nothing.
    // Output:  nothing.
    private void Refilter()
    {
        // Which cards pass the filter and the search
        var results = Filter(_allDevices, SelectedCategoryOption.Category, SearchText);

        // Empty the visible list and put the results in it, one by one
        FilteredDevices.Clear();
        foreach (var entry in results)
        {
            FilteredDevices.Add(entry);
        }

        // These three are computed from the list, so tell the screen by hand that they may have changed
        OnPropertyChanged(nameof(HasResults));          // tell the screen that HasResults may have changed
        OnPropertyChanged(nameof(IsSearching));         // tell the screen that IsSearching may have changed
        OnPropertyChanged(nameof(EmptyStateMessage));   // tell the screen that EmptyStateMessage may have changed
    }

    // Purpose: Picks the cards that match a group and a search text.
    // Input:   devices - all the cards; category - the group (null = all groups); searchText - the text (may be empty).
    // Output:  the matching cards.
    // "internal" = usable only inside this project; "static" = doesn't use any field of the object
    internal static IEnumerable<DeviceLibraryEntry> Filter(IReadOnlyList<DeviceLibraryEntry> devices, DeviceCategory? category, string? searchText)
    {
        // Start from all the cards
        IEnumerable<DeviceLibraryEntry> query = devices;

        // A group is selected - keep only its cards. "Where" = keep only items that pass the test
        if (category is { } selectedCategory)
        {
            query = query.Where(d => d.Category == selectedCategory);
        }

        // There is search text - keep only cards whose name contains it
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var needle = searchText.Trim();   // the text without spaces at the start and the end

            // OrdinalIgnoreCase = capital and small letters count as the same ("pc" finds "PC")
            query = query.Where(d => d.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        // Give back what is left
        return query;
    }

    // Purpose: Builds the filter buttons.
    // Input:   nothing.
    // Output:  the list of buttons - a label and a group for each ("All" has no group: null).
    private static IReadOnlyList<CategoryFilterOption> BuildCategories() =>
    [
        new("All", null),                                     // no filter
        new("End Devices", DeviceCategory.EndDevices),        // PC, laptop, server
        new("Network Devices", DeviceCategory.NetworkDevices),// router, switch
        new("Wireless", DeviceCategory.Wireless),             // none yet
        new("Security", DeviceCategory.Security),             // none yet
        new("Other", DeviceCategory.Other),                   // none yet
    ];
}

// One filter button: the text on it, and the group it filters by (null for "All")
public sealed record CategoryFilterOption(string Label, DeviceCategory? Category);
