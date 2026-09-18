using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSim.Application.Canvas;
using NetSim.Application.Services;
using NetSim.Application.State;
using NetSim.Core.Common;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.UI.Common;

namespace NetSim.UI.ViewModels;

/// <summary>
/// Canvas-only state and commands for the Network Canvas: viewport (pan + zoom), grid
/// visibility/snap and the active tool, plus canvas items/selection for interaction. Everything
/// here delegates to the singleton <see cref="ICanvasState"/>/<see cref="ICanvasItemsState"/>/
/// <see cref="ICanvasSelectionState"/> (see docs/architecture/canvas-interaction.md) rather than
/// owning state itself, so the state survives this ViewModel being recreated on every navigation
/// to the workspace. Mouse/keyboard interaction logic itself lives in <see cref="Interaction"/>
/// (<see cref="CanvasInteractionController"/>, Application layer) - this ViewModel only mirrors
/// state into bindable properties and exposes toolbar commands; it holds no drawing or pointer-
/// handling logic - see NetSim.UI.Controls.NetworkCanvasControl for that.
/// </summary>
public partial class NetworkCanvasViewModel : ViewModelBase
{
    private const double ZoomStep = 1.25;

    /// <summary>Footprint (world units) given to every device placed from the Device Library - see <see cref="ConfirmPlacement"/>.</summary>
    public static readonly CanvasSize DeviceItemSize = new(96, 72);

    private readonly ICanvasState _canvasState;
    private readonly IApplicationState _applicationState;
    private readonly ICanvasItemsState _itemsState;
    private readonly ICanvasSelectionState _selectionState;
    private readonly IDevicePlacementState _placementState;
    private readonly IDeviceService _deviceService;
    private readonly IConnectionService _connectionService;

    private IReadOnlyList<CanvasConnectionView> _connections = [];

    [ObservableProperty]
    private CanvasViewport _viewport;

    [ObservableProperty]
    private bool _isGridVisible;

    [ObservableProperty]
    private bool _isSnapToGridEnabled;

    [ObservableProperty]
    private CanvasTool _currentTool;

    [ObservableProperty]
    private DeviceType? _pendingPlacementDeviceType;

    private double _lastKnownViewportWidth;
    private double _lastKnownViewportHeight;

    public NetworkCanvasViewModel(
        ICanvasState canvasState,
        IApplicationState applicationState,
        ICanvasItemsState itemsState,
        ICanvasSelectionState selectionState,
        IDevicePlacementState placementState,
        IDeviceService deviceService,
        IConnectionService connectionService)
    {
        _canvasState = canvasState;
        _applicationState = applicationState;
        _itemsState = itemsState;
        _selectionState = selectionState;
        _placementState = placementState;
        _deviceService = deviceService;
        _connectionService = connectionService;

        _viewport = _canvasState.Viewport;
        _isGridVisible = _canvasState.IsGridVisible;
        _isSnapToGridEnabled = _canvasState.IsSnapToGridEnabled;
        _currentTool = _canvasState.CurrentTool;
        _pendingPlacementDeviceType = _placementState.PendingDeviceType;

        Interaction = new CanvasInteractionController(
            _canvasState, _itemsState, _selectionState, new ConnectionHitTester(this));
        ConnectionTool = new ConnectionToolController(_applicationState, _itemsState, _connectionService);
        ConnectionTool.Changed += (_, _) =>
        {
            // The Connect tool's prompt / rejection text is surfaced through the shared
            // ErrorMessage slot; a null message (successful pick) clears it again.
            ErrorMessage = ConnectionTool.StatusMessage;
            RebuildConnections();
            RaiseCanvasVisualsChanged();
        };

        RebuildConnections();

        _canvasState.ViewportChanged += (_, _) => Viewport = _canvasState.Viewport;
        _canvasState.GridSettingsChanged += (_, _) =>
        {
            IsGridVisible = _canvasState.IsGridVisible;
            IsSnapToGridEnabled = _canvasState.IsSnapToGridEnabled;
        };
        _canvasState.CurrentToolChanged += (_, _) => CurrentTool = _canvasState.CurrentTool;

        // An interface being enabled/disabled from the Device Properties panel changes what the
        // Connect tool's anchors should look like (available vs. dimmed) without any canvas
        // interaction - refresh the visuals on that too (Phase 14).
        _deviceService.InterfacesChanged += (_, _) => RaiseCanvasVisualsChanged();
        _applicationState.CurrentNetworkChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsWorkspaceEmpty));
            RebuildConnections();
            RaiseCanvasVisualsChanged();
        };
        _placementState.Changed += (_, _) => PendingPlacementDeviceType = _placementState.PendingDeviceType;

        _itemsState.ItemsChanged += (_, _) => RaiseCanvasVisualsChanged();
        _selectionState.SelectionChanged += (_, _) => RaiseCanvasVisualsChanged();
        Interaction.Changed += (_, _) => RaiseCanvasVisualsChanged();
    }

    /// <summary>Zero-logic Avalonia-free interaction engine - see <see cref="CanvasInteractionController"/>.</summary>
    public CanvasInteractionController Interaction { get; }

    /// <summary>Avalonia-free state machine for the Connect tool (pick interface, preview, pick second) - see <see cref="ConnectionToolController"/>.</summary>
    public ConnectionToolController ConnectionTool { get; }

    /// <summary>Cables currently on the canvas, each with its two endpoint anchors resolved against live device positions.</summary>
    public IReadOnlyList<CanvasConnectionView> Connections => _connections;

    /// <summary>Pointer tolerance for picking interface anchors, in world units (~10px regardless of zoom).</summary>
    public double ConnectionHitRadiusWorld => 10.0 / Viewport.Zoom;

    /// <summary>Raised whenever anything the canvas draws beyond the grid (items, selection, hover, marquee, pan/drag) may have changed, so NetworkCanvasControl knows to redraw. Deliberately a plain event, not INotifyPropertyChanged, since it fires on every pointer move.</summary>
    public event System.EventHandler? CanvasVisualsChanged;

    public IReadOnlyList<CanvasItem> Items => _itemsState.Items;

    /// <summary>Grid line spacing, in world units - see <see cref="ICanvasState.GridSpacing"/>.</summary>
    public double GridSpacing => _canvasState.GridSpacing;

    public string ZoomPercentText => $"{Viewport.Zoom * 100:0}%";

    public string PointerCoordinatesText =>
        $"X: {Interaction.PointerWorldPosition.X:0}   Y: {Interaction.PointerWorldPosition.Y:0}";

    public bool IsSelectToolActive => CurrentTool == CanvasTool.Select;

    public bool IsPanToolActive => CurrentTool == CanvasTool.Pan;

    public bool IsConnectToolActive => CurrentTool == CanvasTool.Connect;

    /// <summary>True while a device from the Device Library is armed, waiting for the user to click the canvas - see <see cref="ConfirmPlacement"/>.</summary>
    public bool IsPlacingDevice => PendingPlacementDeviceType is not null;

    public bool IsSelected(EntityId id) => _selectionState.IsSelected(id);

    /// <summary>
    /// True when there is nothing to show yet, so the View can display the "Your network is
    /// empty" state: no real network devices *and* no canvas items - showing "empty" text over
    /// placed devices would be actively misleading.
    /// </summary>
    public bool IsWorkspaceEmpty =>
        (_applicationState.CurrentNetwork is null || _applicationState.CurrentNetwork.Devices.Count == 0)
        && _itemsState.Items.Count == 0;

    partial void OnViewportChanged(CanvasViewport value) => OnPropertyChanged(nameof(ZoomPercentText));

    partial void OnCurrentToolChanged(CanvasTool value)
    {
        OnPropertyChanged(nameof(IsSelectToolActive));
        OnPropertyChanged(nameof(IsPanToolActive));
        OnPropertyChanged(nameof(IsConnectToolActive));

        // Leaving the Connect tool must not strand a half-started cable or its status text.
        if (value != CanvasTool.Connect)
        {
            ConnectionTool.Cancel();
        }
    }

    partial void OnPendingPlacementDeviceTypeChanged(DeviceType? value) => OnPropertyChanged(nameof(IsPlacingDevice));

    /// <summary>
    /// Called by NetworkCanvasControl when the user left-clicks the canvas while
    /// <see cref="IsPlacingDevice"/> is true: creates the pending device through the existing
    /// <see cref="IDeviceService"/>/<c>NetworkDeviceFactory</c> chain (so it is added to the real
    /// <c>NetworkTopology</c>, not a UI-only object), places a matching <see cref="CanvasItem"/>
    /// centered on <paramref name="worldPoint"/> (snapped to the grid if enabled - reusing
    /// <see cref="CanvasGridSnap"/>, never a second snapping implementation), selects it, and ends
    /// placement (single click places exactly one device - see the Phase 11 brief, "Device
    /// Selection").
    /// </summary>
    public void ConfirmPlacement(CanvasPoint worldPoint)
    {
        if (_placementState.PendingDeviceType is not { } deviceType)
        {
            return;
        }

        var result = _deviceService.AddDevice(deviceType);
        if (!result.IsSuccess || result.Value is null)
        {
            ErrorMessage = result.ErrorMessage ?? "Could not place device.";
            _placementState.Cancel();
            return;
        }

        var device = result.Value;
        var center = _canvasState.IsSnapToGridEnabled ? CanvasGridSnap.Snap(worldPoint, _canvasState.GridSpacing) : worldPoint;
        var topLeft = new CanvasPoint(center.X - DeviceItemSize.Width / 2, center.Y - DeviceItemSize.Height / 2);

        _itemsState.AddItem(new CanvasItem(device.Id, device.Name, topLeft, DeviceItemSize, device.DeviceType));
        _selectionState.Replace([device.Id]);
        _placementState.Cancel();
    }

    /// <summary>Leaves placement mode without creating a device - called on Escape (see NetworkCanvasControl).</summary>
    public void CancelPlacement() => _placementState.Cancel();

    /// <summary>
    /// Centers the world origin within a viewport of the given screen size, at the current zoom.
    /// Called once by the view on its first layout pass (see NetworkCanvasControl) - the
    /// ViewModel has no notion of screen size on its own, so it cannot do this by itself at
    /// construction time.
    /// </summary>
    public void CenterView(double viewportWidth, double viewportHeight)
    {
        var zoom = _canvasState.Viewport.Zoom;
        var origin = new CanvasPoint(-viewportWidth / (2 * zoom), -viewportHeight / (2 * zoom));
        _canvasState.SetViewport(new CanvasViewport(origin, zoom));
    }

    /// <summary>
    /// Called by NetworkCanvasControl on every layout pass (not just the first) so
    /// <see cref="FitToView"/> has an up-to-date on-screen size to fit content into - the
    /// ViewModel has no notion of screen size on its own.
    /// </summary>
    public void SetViewportSize(double viewportWidth, double viewportHeight)
    {
        _lastKnownViewportWidth = viewportWidth;
        _lastKnownViewportHeight = viewportHeight;
    }

    [RelayCommand]
    private void ZoomIn() => _canvasState.SetZoom(_canvasState.Viewport.Zoom * ZoomStep);

    [RelayCommand]
    private void ZoomOut() => _canvasState.SetZoom(_canvasState.Viewport.Zoom / ZoomStep);

    [RelayCommand]
    private void ResetView() => _canvasState.ResetViewport();

    // Fits the combined bounds of every CanvasItem (Phase 9's stand-in for "every network
    // object" - see docs/architecture/canvas-interaction.md). Falls back to a plain reset when
    // there is nothing to fit (no items, or the control hasn't reported a size yet), rather than
    // pretending to calculate a fit that has nothing to fit around.
    [RelayCommand]
    private void FitToView()
    {
        var items = _itemsState.Items;
        if (items.Count == 0 || _lastKnownViewportWidth <= 0 || _lastKnownViewportHeight <= 0)
        {
            _canvasState.ResetViewport();
            return;
        }

        var min = new CanvasPoint(items.Min(i => i.Position.X), items.Min(i => i.Position.Y));
        var max = new CanvasPoint(items.Max(i => i.Position.X + i.Size.Width), items.Max(i => i.Position.Y + i.Size.Height));

        _canvasState.SetViewport(CanvasViewport.Fit(min, max, _lastKnownViewportWidth, _lastKnownViewportHeight));
    }

    [RelayCommand]
    private void ToggleGrid() => _canvasState.SetGridVisible(!_canvasState.IsGridVisible);

    [RelayCommand]
    private void ToggleSnapToGrid() => _canvasState.SetSnapToGridEnabled(!_canvasState.IsSnapToGridEnabled);

    [RelayCommand]
    private void SelectTool() => _canvasState.SetCurrentTool(CanvasTool.Select);

    [RelayCommand]
    private void PanTool() => _canvasState.SetCurrentTool(CanvasTool.Pan);

    [RelayCommand]
    private void ConnectTool() => _canvasState.SetCurrentTool(CanvasTool.Connect);

    [RelayCommand]
    private void SelectAll() => Interaction.OnSelectAll();

    /// <summary>
    /// Deletes every currently-selected connection through <see cref="IConnectionService"/>
    /// (which detaches both interfaces and marks the project dirty). Selected *devices* are left
    /// untouched - device deletion is a later phase - so this is safe to bind to the Delete key.
    /// </summary>
    [RelayCommand]
    private void DeleteSelection()
    {
        if (_applicationState.CurrentNetwork is not { } network)
        {
            return;
        }

        var selectedConnections = _selectionState.SelectedIds
            .Select(id => network.Connections.FirstOrDefault(c => c.Id == id))
            .Where(c => c is not null)
            .Cast<Connection>()
            .ToList();

        if (selectedConnections.Count == 0)
        {
            return;
        }

        foreach (var connection in selectedConnections)
        {
            _connectionService.Disconnect(connection);
        }

        var removedIds = selectedConnections.Select(c => c.Id).ToHashSet();
        _selectionState.Replace(_selectionState.SelectedIds.Where(id => !removedIds.Contains(id)));

        RebuildConnections();
        RaiseCanvasVisualsChanged();
    }

    private void RaiseCanvasVisualsChanged()
    {
        // Connection anchors are derived from device positions, so a drag/move/zoom must refresh
        // them before the redraw - there is no stored line to move.
        RebuildConnections();

        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(Connections));
        OnPropertyChanged(nameof(PointerCoordinatesText));
        // Items (not just the network reference) factor into IsWorkspaceEmpty - see its doc
        // comment - so a placed/removed item can flip it just as much as CurrentNetworkChanged
        // can. This was dormant before Phase 11: items only ever changed at construction/reset
        // time, before anything had bound to IsWorkspaceEmpty yet - real device placement is the
        // first case where items change while the workspace is already on screen.
        OnPropertyChanged(nameof(IsWorkspaceEmpty));
        CanvasVisualsChanged?.Invoke(this, System.EventArgs.Empty);
    }

    /// <summary>
    /// Rebuilds <see cref="Connections"/> from the current network's <c>Connections</c>, resolving
    /// each endpoint's world-space anchor from the matching <see cref="CanvasItem"/> position and
    /// the interface's ordinal on its device (see <see cref="InterfaceAnchors"/>). A connection
    /// whose device has no canvas item yet (nothing places it) is simply skipped.
    /// </summary>
    private void RebuildConnections()
    {
        var network = _applicationState.CurrentNetwork;
        if (network is null || network.Connections.Count == 0)
        {
            _connections = [];
            return;
        }

        var views = new List<CanvasConnectionView>();
        foreach (var connection in network.Connections)
        {
            if (TryResolveAnchor(connection.EndpointA, out var a) && TryResolveAnchor(connection.EndpointB, out var b))
            {
                views.Add(new CanvasConnectionView(
                    connection.Id, a, b, connection.ConnectionType, connection.State, _selectionState.IsSelected(connection.Id)));
            }
        }

        _connections = views;
    }

    private bool TryResolveAnchor(NetworkInterface networkInterface, out CanvasPoint anchor)
    {
        anchor = default;

        var device = networkInterface.Device;
        var item = _itemsState.Items.FirstOrDefault(i => i.Id == device.Id);
        if (item is null)
        {
            return false;
        }

        var interfaces = device.Interfaces.ToList();
        var index = interfaces.FindIndex(i => ReferenceEquals(i, networkInterface));
        if (index < 0)
        {
            return false;
        }

        anchor = InterfaceAnchors.ForInterface(item.Position, item.Size, index, interfaces.Count);
        return true;
    }

    /// <summary>
    /// Adapts this ViewModel's live <see cref="Connections"/> geometry into the point-to-segment
    /// test <see cref="CanvasInteractionController"/> needs to let a click select a cable - keeping
    /// connection geometry out of the interaction controller itself.
    /// </summary>
    private sealed class ConnectionHitTester(NetworkCanvasViewModel owner) : IConnectionHitTester
    {
        public EntityId? HitTestConnection(CanvasPoint worldPoint, double radius)
        {
            EntityId? best = null;
            var bestDistance = radius;

            foreach (var connection in owner._connections)
            {
                var distance = DistanceToSegment(worldPoint, connection.A, connection.B);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = connection.Id;
                }
            }

            return best;
        }

        private static double DistanceToSegment(CanvasPoint p, CanvasPoint a, CanvasPoint b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var lengthSquared = (dx * dx) + (dy * dy);

            if (lengthSquared <= double.Epsilon)
            {
                return System.Math.Sqrt(((p.X - a.X) * (p.X - a.X)) + ((p.Y - a.Y) * (p.Y - a.Y)));
            }

            var t = System.Math.Clamp((((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / lengthSquared, 0.0, 1.0);
            var projX = a.X + (t * dx);
            var projY = a.Y + (t * dy);
            return System.Math.Sqrt(((p.X - projX) * (p.X - projX)) + ((p.Y - projY) * (p.Y - projY)));
        }
    }
}
