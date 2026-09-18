using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using NetSim.Application.Canvas;
using NetSim.Core.Devices;
using NetSim.UI.Devices;
using NetSim.UI.ViewModels;

namespace NetSim.UI.Controls;

/// <summary>
/// Renders the Network Canvas - background, grid, canvas items, selection/hover highlighting and
/// the marquee-selection overlay - directly via <see cref="Render"/> instead of composing
/// per-item Avalonia controls, so cost stays flat regardless of how large the world
/// (<see cref="CanvasWorldBounds"/>) or a future topology gets - see
/// docs/architecture/canvas-interaction.md, "Rendering strategy".
///
/// This is also the "User Input" edge of the interaction architecture: it translates raw Avalonia
/// pointer/keyboard events into calls on <see cref="NetworkCanvasViewModel.Interaction"/>
/// (<see cref="CanvasInteractionController"/>, Application layer) and reflects the result back as
/// cursor changes and a redraw. It holds no interaction *logic* of its own - every decision
/// (what a click/drag/wheel/key means) is made by the controller; this class only decodes
/// Avalonia-specific event args into the controller's plain (point, button, modifiers) calls.
/// </summary>
public sealed class NetworkCanvasControl : Control
{
    public static readonly StyledProperty<NetworkCanvasViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, NetworkCanvasViewModel?>(nameof(ViewModel));

    // Bound from XAML via DynamicResource (see NetworkWorkspaceView.axaml) rather than looked up
    // by key inside Render() - that lets Avalonia's own resource/theme-variant machinery handle
    // Dark/Light switching and invalidation the same way every styled control in this app already
    // gets it, instead of this control re-implementing theme-change bookkeeping by hand.
    public static readonly StyledProperty<IBrush?> CanvasBackgroundProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(CanvasBackground));

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> ItemFillBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ItemFillBrush));

    public static readonly StyledProperty<IBrush?> ItemBorderBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ItemBorderBrush));

    public static readonly StyledProperty<IBrush?> ItemTextBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ItemTextBrush));

    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(HoverBrush));

    public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(SelectionBrush));

    public static readonly StyledProperty<IBrush?> MarqueeFillBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(MarqueeFillBrush));

    public static readonly StyledProperty<IBrush?> ConnectionBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ConnectionBrush));

    public static readonly StyledProperty<IBrush?> ConnectionSelectedBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ConnectionSelectedBrush));

    public static readonly StyledProperty<IBrush?> AnchorBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(AnchorBrush));

    public static readonly StyledProperty<IBrush?> AnchorHighlightBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(AnchorHighlightBrush));

    public static readonly StyledProperty<IBrush?> AnchorDisabledBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(AnchorDisabledBrush));

    public static readonly StyledProperty<IBrush?> InvalidBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(InvalidBrush));

    private static readonly IBrush FallbackCanvasBackground = new SolidColorBrush(Color.Parse("#12151A"));
    private static readonly IBrush FallbackGridBrush = new SolidColorBrush(Color.Parse("#262B33"));
    private static readonly IBrush FallbackItemFill = new SolidColorBrush(Color.Parse("#262B34"));
    private static readonly IBrush FallbackItemBorder = new SolidColorBrush(Color.Parse("#3A414D"));
    private static readonly IBrush FallbackItemText = new SolidColorBrush(Color.Parse("#E7EAEE"));
    private static readonly IBrush FallbackHover = new SolidColorBrush(Color.Parse("#4FA8E8"));
    private static readonly IBrush FallbackSelection = new SolidColorBrush(Color.Parse("#4FA8E8"));
    private static readonly IBrush FallbackMarqueeFill = new SolidColorBrush(Color.Parse("#1F3B52"));
    private static readonly IBrush FallbackConnection = new SolidColorBrush(Color.Parse("#8A93A3"));
    private static readonly IBrush FallbackAnchor = new SolidColorBrush(Color.Parse("#6BB9EE"));
    private static readonly IBrush FallbackAnchorHighlight = new SolidColorBrush(Color.Parse("#3DD68C"));
    private static readonly IBrush FallbackAnchorDisabled = new SolidColorBrush(Color.Parse("#5A6270"));
    private static readonly IBrush FallbackInvalid = new SolidColorBrush(Color.Parse("#F0665E"));
    private static readonly IBrush FallbackTooltipBackground = new SolidColorBrush(Color.Parse("#1D2129"));

    private static readonly Cursor DefaultCursor = Cursor.Default;
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly Cursor MoveCursor = new(StandardCursorType.SizeAll);
    private static readonly Cursor CrossCursor = new(StandardCursorType.Cross);
    private static readonly IDashStyle MarqueeDashStyle = new DashStyle([4, 2], 0);

    // Icon.* resources (Theme/Icons.axaml) are theme-invariant, so a lazily-cached lookup per
    // DeviceType is safe (no Dark/Light re-resolution needed) - same reasoning as DeviceCatalog's
    // own icon resolution, which this reuses the same DeviceIcons.ResourceKeyFor mapping as.
    private static readonly Dictionary<DeviceType, Geometry?> DeviceIconCache = new();

    private bool _hasCenteredView;
    private bool _isPointerInside;
    private CanvasInteractionMode _lastCursorMode = CanvasInteractionMode.Idle;

    static NetworkCanvasControl()
    {
        AffectsRender<NetworkCanvasControl>(
            CanvasBackgroundProperty, GridBrushProperty, ItemFillBrushProperty, ItemBorderBrushProperty,
            ItemTextBrushProperty, HoverBrushProperty, SelectionBrushProperty, MarqueeFillBrushProperty,
            ConnectionBrushProperty, ConnectionSelectedBrushProperty, AnchorBrushProperty,
            AnchorHighlightBrushProperty, AnchorDisabledBrushProperty, InvalidBrushProperty);
    }

    public NetworkCanvasControl()
    {
        Focusable = true;
        ClipToBounds = true;
        SizeChanged += OnSizeChanged;
    }

    public NetworkCanvasViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public IBrush? CanvasBackground
    {
        get => GetValue(CanvasBackgroundProperty);
        set => SetValue(CanvasBackgroundProperty, value);
    }

    public IBrush? GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public IBrush? ItemFillBrush
    {
        get => GetValue(ItemFillBrushProperty);
        set => SetValue(ItemFillBrushProperty, value);
    }

    public IBrush? ItemBorderBrush
    {
        get => GetValue(ItemBorderBrushProperty);
        set => SetValue(ItemBorderBrushProperty, value);
    }

    public IBrush? ItemTextBrush
    {
        get => GetValue(ItemTextBrushProperty);
        set => SetValue(ItemTextBrushProperty, value);
    }

    public IBrush? HoverBrush
    {
        get => GetValue(HoverBrushProperty);
        set => SetValue(HoverBrushProperty, value);
    }

    public IBrush? SelectionBrush
    {
        get => GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    public IBrush? MarqueeFillBrush
    {
        get => GetValue(MarqueeFillBrushProperty);
        set => SetValue(MarqueeFillBrushProperty, value);
    }

    /// <summary>Stroke for a connection line in its normal state.</summary>
    public IBrush? ConnectionBrush
    {
        get => GetValue(ConnectionBrushProperty);
        set => SetValue(ConnectionBrushProperty, value);
    }

    /// <summary>Stroke for a selected connection line.</summary>
    public IBrush? ConnectionSelectedBrush
    {
        get => GetValue(ConnectionSelectedBrushProperty);
        set => SetValue(ConnectionSelectedBrushProperty, value);
    }

    /// <summary>Fill for an interface connection anchor point (Connect tool).</summary>
    public IBrush? AnchorBrush
    {
        get => GetValue(AnchorBrushProperty);
        set => SetValue(AnchorBrushProperty, value);
    }

    /// <summary>Fill for an anchor that is a valid target for the pending connection (Connect tool).</summary>
    public IBrush? AnchorHighlightBrush
    {
        get => GetValue(AnchorHighlightBrushProperty);
        set => SetValue(AnchorHighlightBrushProperty, value);
    }

    /// <summary>Fill for an interface anchor whose interface is administratively disabled (Connect tool).</summary>
    public IBrush? AnchorDisabledBrush
    {
        get => GetValue(AnchorDisabledBrushProperty);
        set => SetValue(AnchorDisabledBrushProperty, value);
    }

    /// <summary>Stroke used to flag an invalid connection target / rejected connection.</summary>
    public IBrush? InvalidBrush
    {
        get => GetValue(InvalidBrushProperty);
        set => SetValue(InvalidBrushProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ViewModelProperty)
        {
            return;
        }

        if (change.OldValue is NetworkCanvasViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            oldViewModel.CanvasVisualsChanged -= OnCanvasVisualsChanged;
        }

        if (change.NewValue is NetworkCanvasViewModel newViewModel)
        {
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
            newViewModel.CanvasVisualsChanged += OnCanvasVisualsChanged;
            _hasCenteredView = false;
            TryCenterView();
        }

        InvalidateVisual();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NetworkCanvasViewModel.Viewport) or nameof(NetworkCanvasViewModel.IsGridVisible))
        {
            InvalidateVisual();
        }
    }

    private void OnCanvasVisualsChanged(object? sender, EventArgs e)
    {
        UpdateCursor();
        InvalidateVisual();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ViewModel?.SetViewportSize(Bounds.Width, Bounds.Height);
        TryCenterView();
    }

    private void TryCenterView()
    {
        if (_hasCenteredView || ViewModel is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        _hasCenteredView = true;
        ViewModel.SetViewportSize(Bounds.Width, Bounds.Height);
        ViewModel.CenterView(Bounds.Width, Bounds.Height);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        var button = ToCanvasButton(point.Properties);
        if (button is null)
        {
            return;
        }

        // While a device is armed for placement, a left click confirms it instead of driving the
        // normal select/marquee/drag gesture - see NetworkCanvasViewModel.ConfirmPlacement. Other
        // buttons (e.g. middle-mouse pan) still flow through normally, so the user can pan into
        // position before placing.
        if (vm.IsPlacingDevice && button == CanvasPointerButton.Left)
        {
            vm.ConfirmPlacement(vm.Viewport.ScreenToWorld(ToCanvasPoint(point.Position)));
            e.Handled = true;
            return;
        }

        // Connect tool: a left click picks an interface anchor (first, then second). Other
        // buttons still flow through so the user can middle-drag to pan mid-connection.
        if (vm.CurrentTool == Application.Canvas.CanvasTool.Connect && button == CanvasPointerButton.Left)
        {
            vm.ConnectionTool.OnClick(
                vm.Viewport.ScreenToWorld(ToCanvasPoint(point.Position)), vm.ConnectionHitRadiusWorld);
            e.Handled = true;
            return;
        }

        e.Pointer.Capture(this);
        vm.Interaction.OnPointerPressed(ToCanvasPoint(point.Position), button.Value, IsControlHeld(e.KeyModifiers));
        e.Handled = true;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isPointerInside = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        var position = ToCanvasPoint(e.GetPosition(this));
        vm.Interaction.OnPointerMoved(position, IsControlHeld(e.KeyModifiers));

        if (vm.CurrentTool == Application.Canvas.CanvasTool.Connect)
        {
            vm.ConnectionTool.OnPointerMoved(vm.Viewport.ScreenToWorld(position), vm.ConnectionHitRadiusWorld);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        var button = ToCanvasButton(e.InitialPressMouseButton);
        if (button is not null)
        {
            vm.Interaction.OnPointerReleased(ToCanvasPoint(e.GetPosition(this)), button.Value);
        }

        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isPointerInside = false;
        ViewModel?.Interaction.OnPointerExited();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        vm.Interaction.OnWheel(ToCanvasPoint(e.GetPosition(this)), e.Delta.Y);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Space:
                vm.Interaction.SetSpaceHeld(true);
                e.Handled = true;
                break;

            case Key.Escape:
                if (vm.IsPlacingDevice)
                {
                    vm.CancelPlacement();
                }
                else if (vm.CurrentTool == Application.Canvas.CanvasTool.Connect && vm.ConnectionTool.HasPendingEndpoint)
                {
                    // First Escape abandons the half-made cable...
                    vm.ConnectionTool.Cancel();
                }
                else if (vm.CurrentTool == Application.Canvas.CanvasTool.Connect)
                {
                    // ...a second Escape leaves the Connect tool entirely.
                    vm.SelectToolCommand.Execute(null);
                }
                else
                {
                    vm.Interaction.OnEscape();
                }

                e.Handled = true;
                break;

            case Key.A when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                vm.Interaction.OnSelectAll();
                e.Handled = true;
                break;

            case Key.Delete:
            case Key.Back:
                // Deletes selected connections only (device deletion is a later phase) - see
                // NetworkCanvasViewModel.DeleteSelection.
                vm.DeleteSelectionCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (e.Key == Key.Space)
        {
            ViewModel?.Interaction.SetSpaceHeld(false);
            e.Handled = true;
        }
    }

    private void UpdateCursor()
    {
        var vm = ViewModel;
        if (vm is null)
        {
            Cursor = DefaultCursor;
            return;
        }

        var mode = vm.Interaction.Mode;
        if (mode == _lastCursorMode && mode is CanvasInteractionMode.Panning or CanvasInteractionMode.DraggingItems or CanvasInteractionMode.SelectingRect)
        {
            return;
        }

        _lastCursorMode = mode;

        Cursor = mode switch
        {
            CanvasInteractionMode.Panning => HandCursor,
            CanvasInteractionMode.DraggingItems => MoveCursor,
            CanvasInteractionMode.SelectingRect => CrossCursor,
            _ when vm.IsPlacingDevice => CrossCursor,
            _ when vm.CurrentTool == Application.Canvas.CanvasTool.Connect => CrossCursor,
            _ when vm.Interaction.IsSpaceHeld || vm.CurrentTool == Application.Canvas.CanvasTool.Pan => HandCursor,
            _ when vm.Interaction.HoveredItemId is not null => HandCursor,
            _ => DefaultCursor,
        };
    }

    private static CanvasPoint ToCanvasPoint(Point point) => new(point.X, point.Y);

    private static bool IsControlHeld(KeyModifiers modifiers) => modifiers.HasFlag(KeyModifiers.Control);

    private static CanvasPointerButton? ToCanvasButton(PointerPointProperties properties)
    {
        if (properties.IsLeftButtonPressed)
        {
            return CanvasPointerButton.Left;
        }

        if (properties.IsMiddleButtonPressed)
        {
            return CanvasPointerButton.Middle;
        }

        if (properties.IsRightButtonPressed)
        {
            return CanvasPointerButton.Right;
        }

        return null;
    }

    private static CanvasPointerButton? ToCanvasButton(MouseButton button) => button switch
    {
        MouseButton.Left => CanvasPointerButton.Left,
        MouseButton.Middle => CanvasPointerButton.Middle,
        MouseButton.Right => CanvasPointerButton.Right,
        _ => null,
    };

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);

        context.FillRectangle(CanvasBackground ?? FallbackCanvasBackground, bounds);

        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        if (vm.IsGridVisible)
        {
            DrawGrid(context, bounds, vm);
        }

        // Layer order (see docs/architecture/connections.md): grid -> connections -> devices ->
        // connection anchors/preview -> selection marquee -> placement ghost. Connections sit
        // behind device bodies so the topology stays readable.
        DrawConnections(context, vm);
        DrawItems(context, vm);
        DrawConnectionOverlay(context, vm);
        DrawSelectionRectangle(context, vm);
        DrawPlacementPreview(context, vm);
    }

    private void DrawConnections(DrawingContext context, NetworkCanvasViewModel vm)
    {
        if (vm.Connections.Count == 0)
        {
            return;
        }

        var viewport = vm.Viewport;
        var normalBrush = ConnectionBrush ?? FallbackConnection;
        var selectedBrush = ConnectionSelectedBrush ?? SelectionBrush ?? FallbackSelection;
        var hoverBrush = HoverBrush ?? FallbackHover;
        var hoveredId = vm.Interaction.HoveredConnectionId;

        foreach (var connection in vm.Connections)
        {
            var a = viewport.WorldToScreen(connection.A);
            var b = viewport.WorldToScreen(connection.B);

            var brush = connection.IsSelected ? selectedBrush
                : hoveredId == connection.Id ? hoverBrush
                : normalBrush;
            var thickness = connection.IsSelected ? 3.0 : 2.0;

            var pen = new Pen(brush, thickness) { LineCap = PenLineCap.Round };
            context.DrawLine(pen, new Point(a.X, a.Y), new Point(b.X, b.Y));

            // Small collars where the cable meets each interface so the attachment point reads clearly.
            context.DrawEllipse(brush, null, new Point(a.X, a.Y), 3, 3);
            context.DrawEllipse(brush, null, new Point(b.X, b.Y), 3, 3);
        }
    }

    // Connect-tool feedback drawn on top of devices: every interface anchor, the pending pick,
    // compatible-target highlighting, and the rubber-band preview line to the pointer.
    private void DrawConnectionOverlay(DrawingContext context, NetworkCanvasViewModel vm)
    {
        if (vm.CurrentTool != Application.Canvas.CanvasTool.Connect)
        {
            return;
        }

        var viewport = vm.Viewport;
        var tool = vm.ConnectionTool;
        var endpoints = tool.InterfaceEndpoints();
        var pending = tool.PendingEndpoint;

        var anchorBrush = AnchorBrush ?? FallbackAnchor;
        var highlightBrush = AnchorHighlightBrush ?? FallbackAnchorHighlight;
        var disabledBrush = AnchorDisabledBrush ?? FallbackAnchorDisabled;
        var invalidBrush = InvalidBrush ?? FallbackInvalid;
        var selectionBrush = SelectionBrush ?? FallbackSelection;

        foreach (var endpoint in endpoints)
        {
            var screen = viewport.WorldToScreen(endpoint.Anchor);
            var center = new Point(screen.X, screen.Y);
            var isPending = pending is not null && endpoint.SameInterfaceAs(pending);
            var isHovered = tool.HoveredEndpoint is not null && endpoint.SameInterfaceAs(tool.HoveredEndpoint);

            IBrush fill;
            if (isPending)
            {
                fill = selectionBrush;
            }
            else if (pending is not null)
            {
                // Compatibility (media + availability + admin state) is decided by the controller,
                // never re-derived here - see ConnectionToolController.IsCompatibleWithPending.
                fill = tool.IsCompatibleWithPending(endpoint) ? highlightBrush : invalidBrush;
            }
            else if (!endpoint.IsEnabled)
            {
                fill = disabledBrush;
            }
            else
            {
                fill = endpoint.IsConnected ? invalidBrush : anchorBrush;
            }

            var radius = isPending || isHovered ? 5.0 : 3.5;
            context.DrawEllipse(fill, null, center, radius, radius);

            if (isHovered && !isPending)
            {
                context.DrawEllipse(null, new Pen(fill, 1.5), center, radius + 3, radius + 3);
            }
        }

        if (pending is not null)
        {
            var from = viewport.WorldToScreen(pending.Anchor);
            var toWorld = tool.HoveredEndpoint?.Anchor ?? tool.PointerWorldPosition;
            var to = viewport.WorldToScreen(toWorld);

            var previewPen = new Pen(selectionBrush, 1.5) { DashStyle = MarqueeDashStyle, LineCap = PenLineCap.Round };
            context.DrawLine(previewPen, new Point(from.X, from.Y), new Point(to.X, to.Y));
        }

        DrawInterfaceTooltip(context, vm, tool.HoveredEndpoint);
    }

    // A concise, subtle info card next to the interface anchor under the pointer while the Connect
    // tool is active - name, type, speed, status, connection (see the Phase 14 brief, "Interface
    // Hover"). Text/geometry only, no per-anchor Avalonia controls.
    private void DrawInterfaceTooltip(DrawingContext context, NetworkCanvasViewModel vm, InterfaceEndpoint? hovered)
    {
        if (hovered is null)
        {
            return;
        }

        var textBrush = ItemTextBrush ?? FallbackItemText;
        var background = ItemFillBrush ?? FallbackTooltipBackground;
        var borderBrush = ItemBorderBrush ?? FallbackItemBorder;

        var formatted = new FormattedText(
            hovered.ToTooltip(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, textBrush);

        var anchorScreen = vm.Viewport.WorldToScreen(hovered.Anchor);
        const double pad = 6;
        var origin = new Point(anchorScreen.X + 12, anchorScreen.Y + 12);
        var boxRect = new Rect(origin.X, origin.Y, formatted.Width + (pad * 2), formatted.Height + (pad * 2));

        // Keep the card inside the control bounds.
        var dx = boxRect.Right > Bounds.Width ? Bounds.Width - boxRect.Right - 4 : 0;
        var dy = boxRect.Bottom > Bounds.Height ? Bounds.Height - boxRect.Bottom - 4 : 0;
        boxRect = boxRect.Translate(new Vector(dx, dy));

        context.DrawRectangle(background, new Pen(borderBrush, 1), boxRect, 4, 4);
        context.DrawText(formatted, new Point(boxRect.X + pad, boxRect.Y + pad));
    }

    private void DrawGrid(DrawingContext context, Rect bounds, NetworkCanvasViewModel vm)
    {
        var viewport = vm.Viewport;
        var spacingScreen = vm.GridSpacing * viewport.Zoom;

        // A spacing collapsed by zoom to a couple of pixels would paint a solid smear instead of
        // a grid, so skip drawing rather than degrade to noise.
        if (spacingScreen < 4)
        {
            return;
        }

        var pen = new Pen(GridBrush ?? FallbackGridBrush, 1);

        var topLeftWorld = viewport.ScreenToWorld(new CanvasPoint(0, 0));
        var bottomRightWorld = viewport.ScreenToWorld(new CanvasPoint(bounds.Width, bounds.Height));

        var firstX = Math.Floor(topLeftWorld.X / vm.GridSpacing) * vm.GridSpacing;
        for (var worldX = firstX; worldX <= bottomRightWorld.X; worldX += vm.GridSpacing)
        {
            var screenX = viewport.WorldToScreen(new CanvasPoint(worldX, 0)).X;
            context.DrawLine(pen, new Point(screenX, 0), new Point(screenX, bounds.Height));
        }

        var firstY = Math.Floor(topLeftWorld.Y / vm.GridSpacing) * vm.GridSpacing;
        for (var worldY = firstY; worldY <= bottomRightWorld.Y; worldY += vm.GridSpacing)
        {
            var screenY = viewport.WorldToScreen(new CanvasPoint(0, worldY)).Y;
            context.DrawLine(pen, new Point(0, screenY), new Point(bounds.Width, screenY));
        }
    }

    private void DrawItems(DrawingContext context, NetworkCanvasViewModel vm)
    {
        var viewport = vm.Viewport;
        var fill = ItemFillBrush ?? FallbackItemFill;
        var textBrush = ItemTextBrush ?? FallbackItemText;
        var hoverBrush = HoverBrush ?? FallbackHover;
        var selectionBrush = SelectionBrush ?? FallbackSelection;
        var borderBrush = ItemBorderBrush ?? FallbackItemBorder;

        foreach (var item in vm.Items)
        {
            var topLeft = viewport.WorldToScreen(item.Position);
            var size = new Size(item.Size.Width * viewport.Zoom, item.Size.Height * viewport.Zoom);
            var rect = new Rect(new Point(topLeft.X, topLeft.Y), size);

            if (!rect.Intersects(new Rect(Bounds.Size)))
            {
                continue;
            }

            var isSelected = vm.IsSelected(item.Id);
            var isHovered = vm.Interaction.HoveredItemId == item.Id;

            var strokeBrush = isSelected ? selectionBrush : isHovered ? hoverBrush : borderBrush;
            var strokeThickness = isSelected ? 2 : 1;

            if (item.DeviceType is { } deviceType)
            {
                // A real, placed device (Phase 11) - a solid border/fill reads as "real content",
                // distinct from the dashed placeholder style below and from the dashed, translucent
                // placement-preview ghost (see DrawPlacementPreview).
                var devicePen = new Pen(strokeBrush, strokeThickness);
                context.DrawRectangle(fill, devicePen, rect, 8, 8);

                var icon = GetDeviceIcon(deviceType);
                if (icon is not null && rect.Height >= 32)
                {
                    var iconArea = new Rect(rect.X, rect.Y + 4, rect.Width, rect.Height * 0.6);
                    DrawIconCentered(context, icon, iconArea, textBrush);
                }

                if (rect.Width >= 40 && rect.Height >= 24)
                {
                    var formattedText = new FormattedText(
                        item.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 12, textBrush);
                    var textOrigin = new Point(rect.Center.X - formattedText.Width / 2, rect.Bottom - formattedText.Height - 4);
                    context.DrawText(formattedText, textOrigin);
                }

                continue;
            }

            // Plain/generic item (interaction-layer scaffolding, no linked device) - kept dashed so
            // it never reads as a real device - see docs/architecture/canvas-interaction.md.
            var pen = new Pen(strokeBrush, strokeThickness) { DashStyle = DashStyle.Dash };
            context.DrawRectangle(fill, pen, rect, 4, 4);

            if (rect.Width >= 40 && rect.Height >= 24)
            {
                var formattedText = new FormattedText(
                    item.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 12, textBrush);
                var textOrigin = new Point(rect.Center.X - formattedText.Width / 2, rect.Center.Y - formattedText.Height / 2);
                context.DrawText(formattedText, textOrigin);
            }
        }
    }

    // The device armed for placement (Phase 11), following the pointer until the user clicks to
    // confirm or Escapes to cancel - dashed + translucent so it never reads as an already-placed
    // device (see the Phase 11 brief, "Placement Preview").
    private void DrawPlacementPreview(DrawingContext context, NetworkCanvasViewModel vm)
    {
        if (!_isPointerInside || vm.PendingPlacementDeviceType is not { } deviceType)
        {
            return;
        }

        var viewport = vm.Viewport;
        var size = NetworkCanvasViewModel.DeviceItemSize;
        var worldCenter = vm.Interaction.PointerWorldPosition;
        var topLeftWorld = new CanvasPoint(worldCenter.X - size.Width / 2, worldCenter.Y - size.Height / 2);
        var topLeft = viewport.WorldToScreen(topLeftWorld);
        var screenSize = new Size(size.Width * viewport.Zoom, size.Height * viewport.Zoom);
        var rect = new Rect(new Point(topLeft.X, topLeft.Y), screenSize);

        var accentBrush = SelectionBrush ?? FallbackSelection;
        var pen = new Pen(accentBrush, 1.5) { DashStyle = MarqueeDashStyle };
        context.DrawRectangle(MarqueeFillBrush ?? FallbackMarqueeFill, pen, rect, 8, 8);

        var icon = GetDeviceIcon(deviceType);
        if (icon is not null)
        {
            DrawIconCentered(context, icon, rect, accentBrush);
        }
    }

    private static Geometry? GetDeviceIcon(DeviceType deviceType)
    {
        if (DeviceIconCache.TryGetValue(deviceType, out var cached))
        {
            return cached;
        }

        var resourceKey = DeviceIcons.ResourceKeyFor(deviceType);
        var geometry = Avalonia.Application.Current?.TryFindResource(resourceKey, out var resource) == true
            ? resource as Geometry
            : null;

        DeviceIconCache[deviceType] = geometry;
        return geometry;
    }

    // Icon.* geometries (Theme/Icons.axaml) are authored on a fixed 20x20 grid - see that file's
    // header comment - so scaling to a target box is a simple uniform scale + centering, no
    // per-icon bounds math needed.
    private static void DrawIconCentered(DrawingContext context, Geometry icon, Rect area, IBrush brush)
    {
        const double iconGridSize = 20.0;
        var targetSize = Math.Min(area.Width, area.Height) * 0.55;
        if (targetSize <= 0)
        {
            return;
        }

        var scale = targetSize / iconGridSize;
        var offsetX = area.Center.X - (iconGridSize * scale) / 2;
        var offsetY = area.Center.Y - (iconGridSize * scale) / 2;

        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            context.DrawGeometry(brush, null, icon);
        }
    }

    private void DrawSelectionRectangle(DrawingContext context, NetworkCanvasViewModel vm)
    {
        var rectangle = vm.Interaction.SelectionRectangle;
        if (rectangle is null)
        {
            return;
        }

        var viewport = vm.Viewport;
        var topLeft = viewport.WorldToScreen(rectangle.Min);
        var bottomRight = viewport.WorldToScreen(rectangle.Max);
        var screenRect = new Rect(new Point(topLeft.X, topLeft.Y), new Point(bottomRight.X, bottomRight.Y));

        var pen = new Pen(SelectionBrush ?? FallbackSelection, 1) { DashStyle = MarqueeDashStyle };
        context.DrawRectangle(MarqueeFillBrush ?? FallbackMarqueeFill, pen, screenRect);
    }
}
