using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSim.Application.Canvas;
using NetSim.UI.Common;
using NetSim.UI.Devices;

namespace NetSim.UI.ViewModels;

/// <summary>
/// The Device Library panel: lets the user browse/search <see cref="DeviceCatalog.AllDevices"/>
/// and pick one to place on the canvas. Picking a device does not create it immediately - it only
/// arms <see cref="IDevicePlacementState"/> (see docs/architecture/network-canvas.md and the Phase
/// 11 brief, "Device Selection") - the actual <c>NetworkDevice</c> creation happens in
/// <see cref="NetworkCanvasViewModel.ConfirmPlacement"/> once the user clicks the canvas, through
/// the existing <c>IDeviceService</c>/<c>NetworkDeviceFactory</c> chain. This ViewModel therefore
/// holds no domain/creation logic of its own - only catalog browsing/filtering and the placement
/// hand-off.
/// </summary>
public partial class DeviceLibraryViewModel : ViewModelBase
{
    private readonly IDevicePlacementState _placementState;
    private readonly IReadOnlyList<DeviceLibraryEntry> _allDevices;
    private bool _isSyncingSelectionFromPlacementState;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private CategoryFilterOption _selectedCategoryOption;

    [ObservableProperty]
    private DeviceLibraryEntry? _selectedEntry;

    [ObservableProperty]
    private bool _isCollapsed;

    public DeviceLibraryViewModel(IDevicePlacementState placementState)
        : this(placementState, DeviceCatalog.AllDevices)
    {
    }

    // Public (not internal - this codebase has no InternalsVisibleTo wiring, see
    // NetworkDeviceMapper for the same precedent) seam so tests can exercise filtering against a
    // fixed, known set of entries instead of depending on whatever DeviceCatalog.AllDevices
    // currently contains.
    public DeviceLibraryViewModel(IDevicePlacementState placementState, IReadOnlyList<DeviceLibraryEntry> allDevices)
    {
        ArgumentNullException.ThrowIfNull(placementState);
        ArgumentNullException.ThrowIfNull(allDevices);

        _placementState = placementState;
        _allDevices = allDevices;

        Categories = BuildCategories();
        _selectedCategoryOption = Categories[0];

        _placementState.Changed += OnPlacementStateChanged;

        Refilter();
    }

    /// <summary>Fixed set of category tabs - "All" plus every <see cref="DeviceCategory"/>, even ones with no supported devices yet.</summary>
    public IReadOnlyList<CategoryFilterOption> Categories { get; }

    public ObservableCollection<DeviceLibraryEntry> FilteredDevices { get; } = [];

    public bool HasResults => FilteredDevices.Count > 0;

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    /// <summary>Distinguishes "no results for this search" from "this category has nothing yet" - see the Phase 11 brief, "Empty Search Results"/"Empty Category".</summary>
    public string EmptyStateMessage => IsSearching
        ? "No devices found. Try another search."
        : "No devices available in this category yet.";

    [RelayCommand]
    private void ToggleCollapse() => IsCollapsed = !IsCollapsed;

    partial void OnSearchTextChanged(string value) => Refilter();

    partial void OnSelectedCategoryOptionChanged(CategoryFilterOption value) => Refilter();

    partial void OnSelectedEntryChanged(DeviceLibraryEntry? value)
    {
        if (_isSyncingSelectionFromPlacementState)
        {
            return;
        }

        if (value is null)
        {
            _placementState.Cancel();
        }
        else
        {
            _placementState.Begin(value.DeviceType);
        }
    }

    // Keeps the library's selection card in sync with placement state changing from elsewhere -
    // Escape on the canvas cancelling placement, or NetworkCanvasViewModel.ConfirmPlacement ending
    // it after a successful single-shot placement (see docs/architecture/device-model.md).
    private void OnPlacementStateChanged(object? sender, EventArgs e)
    {
        _isSyncingSelectionFromPlacementState = true;
        SelectedEntry = _placementState.PendingDeviceType is { } deviceType
            ? _allDevices.FirstOrDefault(d => d.DeviceType == deviceType)
            : null;
        _isSyncingSelectionFromPlacementState = false;
    }

    private void Refilter()
    {
        var results = Filter(_allDevices, SelectedCategoryOption.Category, SearchText);

        FilteredDevices.Clear();
        foreach (var entry in results)
        {
            FilteredDevices.Add(entry);
        }

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(IsSearching));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    internal static IEnumerable<DeviceLibraryEntry> Filter(IReadOnlyList<DeviceLibraryEntry> devices, DeviceCategory? category, string? searchText)
    {
        IEnumerable<DeviceLibraryEntry> query = devices;

        if (category is { } selectedCategory)
        {
            query = query.Where(d => d.Category == selectedCategory);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var needle = searchText.Trim();
            query = query.Where(d => d.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    private static IReadOnlyList<CategoryFilterOption> BuildCategories() =>
    [
        new("All", null),
        new("End Devices", DeviceCategory.EndDevices),
        new("Network Devices", DeviceCategory.NetworkDevices),
        new("Wireless", DeviceCategory.Wireless),
        new("Security", DeviceCategory.Security),
        new("Other", DeviceCategory.Other),
    ];
}

/// <summary>One entry in the Device Library's category filter tabs. <see cref="Category"/> is null for the "All" option.</summary>
public sealed record CategoryFilterOption(string Label, DeviceCategory? Category);
