using System;                        // Math, EventArgs
using System.Collections.Generic;    // Dictionary<TKey, TValue>
using System.Globalization;          // CultureInfo - for FormattedText
using Avalonia;                      // AvaloniaProperty, StyledProperty, Rect, Point, Size, Matrix
using Avalonia.Controls;             // Control, TopLevel
using Avalonia.Input;                // PointerEventArgs, PointerPressedEventArgs, PointerReleasedEventArgs, KeyEventArgs, Key, MouseButton
using Avalonia.Interactivity;        // RoutingStrategies - which way an event travels through the screen
using Avalonia.Media;                // DrawingContext, IBrush, Pen, Geometry, FormattedText, Typeface, DashStyle, Brushes
using NetSim.Application.Canvas;     // CanvasItem, CanvasPoint
using NetSim.Core.Devices;           // DeviceType
using NetSim.UI.Devices;             // DeviceIcons - which icon draws each device kind
using NetSim.UI.ViewModels;          // NetworkCanvasViewModel

namespace NetSim.UI.Controls;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The Network Canvas on the screen: it DRAWS the devices (a rounded box, the icon and the name) and
// the "ghost" of the device waiting to be placed, and it turns the mouse into calls on
// NetworkCanvasViewModel. It holds no rules of its own - "what happens when you place a device" is
// decided by the view model and DeviceService.
// It draws everything itself in Render() (instead of one Avalonia control per device), so it stays
// fast however many devices there are - the same way the reference project did it.
// Taken from the reference project and cut down to what step 3 needs: drawing devices, the ghost,
// click-to-place, drop-to-place and Escape. Grid, zoom, selection, moving and cables come in steps 4-6.
// Two ways to place a device:
//   1. Click a card in the Device Library, then click the canvas.
//   2. Press on a card and DRAG it onto the canvas, then let go (new - not in the reference project).
// ": Control" = a basic Avalonia screen element that we draw by hand
public sealed class NetworkCanvasControl : Control
{
    // ----- Properties that can be set from XAML -----
    // A "StyledProperty" is a property Avalonia knows about: it can be set from XAML, bound with
    // {Binding} / {DynamicResource}, and Avalonia tells us when it changes

    // The canvas view model. Set in NetworkWorkspaceView.axaml: ViewModel="{Binding Canvas}"
    public static readonly StyledProperty<NetworkCanvasViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, NetworkCanvasViewModel?>(nameof(ViewModel));

    // The colors. They come from the theme (Theme/Colors.axaml) with {DynamicResource ...} in XAML, so
    // a dark / light theme switch reaches the canvas by itself, like every other control in the app
    public static readonly StyledProperty<IBrush?> CanvasBackgroundProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(CanvasBackground));   // the canvas floor

    public static readonly StyledProperty<IBrush?> ItemFillBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ItemFillBrush));      // inside a device box

    public static readonly StyledProperty<IBrush?> ItemBorderBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ItemBorderBrush));    // the line around a device box

    public static readonly StyledProperty<IBrush?> ItemTextBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ItemTextBrush));      // the icon and the name

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(AccentBrush));        // the ghost's line and icon

    // Colors to use when the theme did not give one (so the canvas never draws with "nothing")
    private static readonly IBrush FallbackBackground = new SolidColorBrush(Color.Parse("#12151A"));   // dark floor
    private static readonly IBrush FallbackItemFill = new SolidColorBrush(Color.Parse("#20242C"));     // dark box
    private static readonly IBrush FallbackItemBorder = new SolidColorBrush(Color.Parse("#3A414D"));   // grey line
    private static readonly IBrush FallbackItemText = new SolidColorBrush(Color.Parse("#E7EAEE"));     // light text
    private static readonly IBrush FallbackAccent = new SolidColorBrush(Color.Parse("#4FA8E8"));       // blue
    private static readonly IBrush GhostFill = new SolidColorBrush(Color.Parse("#334FA8E8"));          // see-through blue ("33" = about 20% visible)

    // The ghost's dashed line: 4 units of line, 3 units of gap
    private static readonly DashStyle GhostDash = new([4, 3], 0);

    // The icons are drawn on a 20 x 20 grid (see Theme/Icons.axaml) - used to scale them to any size
    private const double IconGridSize = 20.0;

    // The size of the device name under the icon
    private const double LabelFontSize = 12;

    // Icons already looked up, so each kind is searched in the theme only once
    private readonly Dictionary<DeviceType, Geometry?> _iconCache = [];

    // ----- What the control remembers between mouse events -----
    private NetworkCanvasViewModel? _subscribedViewModel;   // the view model whose RedrawRequested we listen to
    private TopLevel? _topLevel;                            // the window this control sits in (null when not on screen)
    private Point _pointerPosition;                         // the last mouse position, relative to the canvas
    private bool _isPointerInside;                          // is the mouse over the canvas right now

    // Purpose:       Tells Avalonia which properties change the drawing.
    // Precondition:  none (a static constructor runs once, before the first control is built).
    // Postcondition: changing any of these properties redraws the canvas by itself.
    static NetworkCanvasControl()
    {
        // "AffectsRender" = when one of these changes, call Render() again
        AffectsRender<NetworkCanvasControl>(
            CanvasBackgroundProperty, ItemFillBrushProperty, ItemBorderBrushProperty,
            ItemTextBrushProperty, AccentBrushProperty);

        // The canvas can receive keyboard focus (needed for the Escape key when it has the focus)
        FocusableProperty.OverrideDefaultValue<NetworkCanvasControl>(true);
    }

    // The C# side of each StyledProperty above: "get" reads it, "set" writes it
    public NetworkCanvasViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);    // read the value Avalonia keeps
        set => SetValue(ViewModelProperty, value);   // write it (Avalonia then raises "changed")
    }

    public IBrush? CanvasBackground
    {
        get => GetValue(CanvasBackgroundProperty);
        set => SetValue(CanvasBackgroundProperty, value);
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

    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    // ----- Joining the view model -----

    // Purpose:       Runs when any Avalonia property of this control changes - re-joins a new view model.
    // Precondition:  change describes which property changed (Avalonia passes it in).
    // Postcondition: when ViewModel changed - we listen to the new view model's RedrawRequested only,
    //                and the canvas is redrawn.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        // Let Avalonia do its own work first (e.g. AffectsRender)
        base.OnPropertyChanged(change);

        // Only the ViewModel property matters here
        if (change.Property != ViewModelProperty)
        {
            return;   // stop here - another property changed
        }

        // Stop listening to the old view model, if there was one
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.RedrawRequested -= OnRedrawRequested;
        }

        // Remember the new one, and listen to it (if it is not null)
        _subscribedViewModel = ViewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.RedrawRequested += OnRedrawRequested;
        }

        // Draw with the new view model
        InvalidateVisual();
    }

    // Purpose:       Runs when the view model asks for a redraw.
    // Precondition:  none (called by RedrawRequested).
    // Postcondition: Avalonia will call Render() again soon.
    private void OnRedrawRequested(object? sender, EventArgs e) => InvalidateVisual();   // "the picture is out of date"

    // ----- Listening to the whole window (for dragging from the Device Library) -----

    // Purpose:       Runs when the canvas appears on the screen - starts listening to the mouse and the
    //                keyboard of the WHOLE window.
    // Precondition:  e describes where the control was attached (Avalonia passes it in).
    // Postcondition: OnWindowPointerMoved / OnWindowPointerReleased / OnWindowKeyDown run for every mouse
    //                move, mouse release and key press in the window.
    // Why the whole window? When the user presses on a card in the Device Library, that card "captures"
    // the mouse: the moves and the release are sent to the CARD, not to the canvas - even when the
    // mouse is over the canvas. Those events still travel up through the window (they "bubble"), so
    // the window is the one place where the canvas can see them.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Let Avalonia do its own work first
        base.OnAttachedToVisualTree(e);

        // The window (the "top level") this control is inside
        _topLevel = TopLevel.GetTopLevel(this);

        // Not inside a window (should not happen) - nothing to listen to
        if (_topLevel is null)
        {
            return;   // stop here - nothing more to do
        }

        // AddHandler(event, method, route, handledEventsToo):
        //   Bubble = hear the event on its way UP from the element that got it (e.g. the card);
        //   handledEventsToo: true = hear it even if that element already marked it "handled"
        _topLevel.AddHandler(PointerMovedEvent, OnWindowPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        _topLevel.AddHandler(PointerReleasedEvent, OnWindowPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);

        // Tunnel = hear the key on its way DOWN, before any other control - so Escape cancels the
        // placement no matter which control has the keyboard focus
        _topLevel.AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    // Purpose:       Runs when the canvas leaves the screen - stops listening to the window.
    // Precondition:  e describes where the control was detached from (Avalonia passes it in).
    // Postcondition: the window no longer calls this control (so it can be freed from memory).
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Let Avalonia do its own work first
        base.OnDetachedFromVisualTree(e);

        // Only if we were listening
        if (_topLevel is not null)
        {
            _topLevel.RemoveHandler(PointerMovedEvent, OnWindowPointerMoved);           // stop: mouse moves
            _topLevel.RemoveHandler(PointerReleasedEvent, OnWindowPointerReleased);     // stop: mouse releases
            _topLevel.RemoveHandler(KeyDownEvent, OnWindowKeyDown);                     // stop: key presses
            _topLevel = null;                                                           // no window any more
        }
    }

    // Purpose:       Runs for every mouse move in the window - moves the ghost with the mouse.
    // Precondition:  e is the mouse event (Avalonia passes it in).
    // Postcondition: _pointerPosition / _isPointerInside are up to date; redrawn when a ghost is shown.
    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        // Where the mouse is, measured from the canvas's own top-left corner
        _pointerPosition = e.GetPosition(this);

        // Is that point inside the canvas? (a rectangle from (0,0) with the canvas's size)
        _isPointerInside = new Rect(Bounds.Size).Contains(_pointerPosition);

        // Only while a device is waiting to be placed does the ghost follow the mouse - redraw.
        // "?." = if ViewModel is null the whole thing is null, and "== true" turns that into false
        if (ViewModel?.IsPlacingDevice == true)
        {
            InvalidateVisual();
        }
    }

    // Purpose:       Runs for every mouse release in the window - finishes a drag from the Device Library.
    // Precondition:  e is the mouse event (Avalonia passes it in).
    // Postcondition: when the left button was let go over the canvas while a device was waiting -
    //                the device was placed there. Otherwise nothing changed.
    // For the "click, then click" way this does nothing: the click on the canvas already placed the
    // device in OnPointerPressed, so when the button is let go nothing is waiting any more.
    private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Only the left mouse button places devices
        if (e.InitialPressMouseButton != MouseButton.Left)
        {
            return;   // stop here - another button
        }

        // The view model, and only while a device is waiting to be placed
        if (ViewModel is not { IsPlacingDevice: true } viewModel)
        {
            return;   // stop here - nothing to place
        }

        // Where the button was let go, measured from the canvas's top-left corner
        var position = e.GetPosition(this);

        // Let go outside the canvas (e.g. on the Device Library itself = a plain click on a card) - not a drop
        if (!new Rect(Bounds.Size).Contains(position))
        {
            return;   // stop here - the device keeps waiting, so the user can still click the canvas
        }

        // A drop on the canvas: place the device there
        viewModel.ConfirmPlacement(ToCanvasPoint(position));
    }

    // Purpose:       Runs for every key press in the window - Escape cancels "place a device".
    // Precondition:  e is the key event (Avalonia passes it in).
    // Postcondition: if Escape was pressed while a device was waiting - nothing is waiting any more,
    //                and the key is marked handled. Otherwise nothing changed.
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Only Escape, and only while a device is waiting
        if (e.Key != Key.Escape || ViewModel is not { IsPlacingDevice: true } viewModel)
        {
            return;   // stop here - not our key
        }

        viewModel.CancelPlacement();   // stop waiting
        e.Handled = true;              // no other control should also react to this Escape
    }

    // ----- Mouse events of the canvas itself -----

    // Purpose:       Runs when a mouse button is pressed ON the canvas - the second click of "click, then click".
    // Precondition:  e is the mouse event (Avalonia passes it in).
    // Postcondition: the canvas has the keyboard focus; if the left button was pressed while a device
    //                was waiting - the device was placed at the mouse position.
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        // Let Avalonia do its own work first
        base.OnPointerPressed(e);

        // Take the keyboard focus (so keys reach the canvas)
        Focus();

        // The press, measured from the canvas's top-left corner, with which buttons are down
        var point = e.GetCurrentPoint(this);

        // Only the left button, and only while a device is waiting to be placed
        if (!point.Properties.IsLeftButtonPressed || ViewModel is not { IsPlacingDevice: true } viewModel)
        {
            return;   // stop here - nothing to place
        }

        // Place the device where the user clicked
        viewModel.ConfirmPlacement(ToCanvasPoint(point.Position));

        // "Handled" = this click is used up - no other control should react to it
        e.Handled = true;
    }

    // Purpose:       Runs when the mouse leaves the canvas - hides the ghost.
    // Precondition:  e is the mouse event (Avalonia passes it in).
    // Postcondition: _isPointerInside is false and the canvas is redrawn.
    protected override void OnPointerExited(PointerEventArgs e)
    {
        // Let Avalonia do its own work first
        base.OnPointerExited(e);

        _isPointerInside = false;   // the mouse is not over the canvas
        InvalidateVisual();         // draw again, without the ghost
    }

    // ----- Drawing -----

    // Purpose:       Draws the whole canvas: the floor, every device, then the ghost on top.
    // Precondition:  context is where to draw (Avalonia passes it in, when the picture is out of date).
    // Postcondition: the canvas on the screen shows the current devices and the ghost (if any).
    public override void Render(DrawingContext context)
    {
        // The whole area of the canvas: from (0,0), with the canvas's size
        var bounds = new Rect(Bounds.Size);

        // The floor. "??" = the theme color, or the fallback when the theme gave none
        context.FillRectangle(CanvasBackground ?? FallbackBackground, bounds);

        // No view model yet - only the floor
        if (ViewModel is not { } viewModel)
        {
            return;   // stop here - nothing more to draw
        }

        // Every device, in order: the last one ends up on top
        foreach (var item in viewModel.Items)
        {
            DrawItem(context, item);
        }

        // The ghost goes last, so it is drawn above everything
        DrawPlacementGhost(context, viewModel);
    }

    // Purpose:       Draws one device: a rounded box, its icon in the upper part and its name below.
    // Precondition:  context is where to draw; item is a device on the canvas.
    // Postcondition: the device is drawn at its position.
    private void DrawItem(DrawingContext context, CanvasItem item)
    {
        // The box: until zoom / scroll arrive in step 5, a world point IS the screen point inside the canvas
        var rect = new Rect(item.Position.X, item.Position.Y, item.Size.Width, item.Size.Height);

        // The colors for this drawing (theme or fallback)
        var textBrush = ItemTextBrush ?? FallbackItemText;                          // icon + name
        var borderPen = new Pen(ItemBorderBrush ?? FallbackItemBorder, 1);          // a 1-unit line around the box

        // The box itself, with corners rounded by 8 units
        context.DrawRectangle(ItemFillBrush ?? FallbackItemFill, borderPen, rect, 8, 8);

        // The icon, in the upper 60% of the box (leaves room for the name below)
        var icon = GetDeviceIcon(item.DeviceType);
        if (icon is not null)
        {
            var iconArea = new Rect(rect.X, rect.Y + 4, rect.Width, rect.Height * 0.6);   // the upper part
            DrawIconCentered(context, icon, iconArea, textBrush);                          // draw it in the middle of that part
        }

        // The name, e.g. "Router0". FormattedText = text ready to be drawn, with its size measured
        var label = new FormattedText(
            item.Label,                      // the text
            CultureInfo.CurrentCulture,      // the language rules for drawing it
            FlowDirection.LeftToRight,       // device names are written left to right
            Typeface.Default,                // the default font
            LabelFontSize,                   // the size
            textBrush);                      // the color

        // Centered horizontally, 4 units above the bottom of the box
        var labelOrigin = new Point(rect.Center.X - label.Width / 2, rect.Bottom - label.Height - 4);
        context.DrawText(label, labelOrigin);
    }

    // Purpose:       Draws the "ghost" of the device waiting to be placed, centered under the mouse.
    // Precondition:  context is where to draw; viewModel is not null.
    // Postcondition: when a device is waiting and the mouse is over the canvas - a see-through dashed
    //                box with the device's icon is drawn under the mouse. Otherwise nothing is drawn.
    private void DrawPlacementGhost(DrawingContext context, NetworkCanvasViewModel viewModel)
    {
        // Nothing waiting, or the mouse is not over the canvas - no ghost
        if (!_isPointerInside || viewModel.PendingPlacementDeviceType is not { } deviceType)
        {
            return;   // stop here - nothing to draw
        }

        // The same size a placed device gets, centered on the mouse - so the user sees exactly where it lands
        var size = CanvasItem.DefaultDeviceSize;
        var rect = new Rect(
            _pointerPosition.X - size.Width / 2,    // left = mouse X minus half the width
            _pointerPosition.Y - size.Height / 2,   // top  = mouse Y minus half the height
            size.Width,                             // width
            size.Height);                           // height

        // A dashed blue line, so it never looks like a device that is already placed
        var accent = AccentBrush ?? FallbackAccent;
        var dashedPen = new Pen(accent, 1.5) { DashStyle = GhostDash };

        // The see-through box
        context.DrawRectangle(GhostFill, dashedPen, rect, 8, 8);

        // The device's icon in the middle, in the blue color
        var icon = GetDeviceIcon(deviceType);
        if (icon is not null)
        {
            DrawIconCentered(context, icon, rect, accent);
        }
    }

    // ----- Small helpers -----

    // Purpose:       Finds the icon drawing of a device kind in the theme (Theme/Icons.axaml).
    // Precondition:  the control is on the screen (so it can reach the app's resources).
    // Postcondition: returns the icon shape, or null when it was not found. Each kind is looked up only once.
    private Geometry? GetDeviceIcon(DeviceType deviceType)
    {
        // Already looked up before - use the remembered answer
        if (_iconCache.TryGetValue(deviceType, out var cached))
        {
            return cached;
        }

        // The resource name, e.g. "Icon.Router" - the same one the Device Library cards use
        var resourceKey = DeviceIcons.ResourceKeyFor(deviceType);

        // Search this control, then its parents, then the app ("TryFindResource").
        // "as Geometry" = use it only if it really is a shape, otherwise null
        var icon = this.TryFindResource(resourceKey, out var resource) ? resource as Geometry : null;

        // Remember the answer (even null), and give it back
        _iconCache[deviceType] = icon;
        return icon;
    }

    // Purpose:       Draws an icon in the middle of an area, scaled to fit.
    // Precondition:  icon is drawn on a 20 x 20 grid; area is where to draw it.
    // Postcondition: the icon is drawn centered in the area, filling about 55% of its smaller side.
    private static void DrawIconCentered(DrawingContext context, Geometry icon, Rect area, IBrush brush)
    {
        // How big the icon should be: 55% of the smaller of width / height
        var targetSize = Math.Min(area.Width, area.Height) * 0.55;

        // No room at all - nothing to draw
        if (targetSize <= 0)
        {
            return;   // stop here
        }

        // How much to grow the 20 x 20 icon, and where its top-left corner goes so it is centered
        var scale = targetSize / IconGridSize;                                // e.g. 22 / 20 = 1.1
        var offsetX = area.Center.X - (IconGridSize * scale) / 2;            // left edge of the icon
        var offsetY = area.Center.Y - (IconGridSize * scale) / 2;            // top edge of the icon

        // PushTransform = "until the end of this 'using' block, draw everything scaled and moved".
        // First grow (CreateScale), then move into place (CreateTranslation)
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            context.DrawGeometry(brush, null, icon);   // fill the shape with the color, no outline (null)
        }
    }

    // Purpose:       Turns an Avalonia point (inside the canvas) into a canvas world point.
    // Precondition:  point is measured from the canvas's top-left corner.
    // Postcondition: returns the same X and Y as a CanvasPoint (until step 5 adds zoom / scroll,
    //                the two are the same).
    private static CanvasPoint ToCanvasPoint(Point point) => new(point.X, point.Y);   // copy X and Y
}
