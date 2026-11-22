using System;                        // brings in Math (Min, Floor) and EventArgs
using System.Collections.Generic;    // brings in Dictionary<TKey, TValue> - the icons already looked up
using System.Globalization;          // brings in CultureInfo - the language rules FormattedText needs to draw text
using Avalonia;                      // brings in AvaloniaProperty, StyledProperty, Rect (a box), Point, Matrix (move / grow)
using Avalonia.Controls;             // brings in Control (a screen element we draw ourselves) and TopLevel (the window)
using Avalonia.Input;                // brings in the mouse / key events, Key, KeyModifiers, MouseButton, Cursor
using Avalonia.Interactivity;        // brings in RoutingStrategies - which way an event travels through the screen (Tunnel / Bubble)
using Avalonia.Media;                // brings in DrawingContext (what we draw on), IBrush (a color), Pen, Geometry, FormattedText, Typeface, DashStyle
using NetSim.Application.Canvas;     // brings in CanvasItem, CanvasPoint, CanvasPointerButton, CanvasTool, CanvasInteractionMode
using NetSim.Core.Devices;           // brings in DeviceType - the kind of a device (for its icon)
using NetSim.Core.Networking;        // brings in ConnectionType - each cable kind is drawn in its own style (step 7)
using NetSim.UI.Devices;             // brings in DeviceIcons - which icon draws each device kind
using NetSim.UI.ViewModels;          // brings in NetworkCanvasViewModel - everything this control draws comes from it

namespace NetSim.UI.Controls;   // the "address" of this file: the Controls part of the UI layer

// The Network Canvas on the screen. It DRAWS everything itself in Render() and turns the mouse and the keyboard into
// calls on NetworkCanvasViewModel and its two controllers. It holds no rules of its own.
//   Step 3: placing a device (click-then-click, or drag from the Device Library), the ghost, Escape.
//   Step 4: select (click, Ctrl+click, rectangle), drag to move, highlight, Delete, Ctrl+A.
//   Step 5: the grid, wheel zoom around the mouse, scrolling (middle button / Space+drag / Pan tool).
//   Step 6: the cables, and the Connect tool (highlight + a dashed "rubber band" line to the mouse).
//   Step 7: each cable kind in its own style; Escape also closes the port list.
// How the camera works here: everything that lives in the world (devices, cables, the selection rectangle, the ghost)
// is drawn in WORLD units, inside one "transform" that moves and grows the picture by the camera (Viewport). Only the
// grid is drawn in screen units.
// "sealed" = nothing inherits from it; ": Control" = a basic Avalonia screen element that we draw by hand
public sealed class NetworkCanvasControl : Control
{
    // ----- Properties that can be set from XAML (see NetworkWorkspaceView.axaml) -----
    // A "StyledProperty" is a property Avalonia knows about: it can be set from XAML, bound with {Binding} or
    // {DynamicResource}, and Avalonia tells us when it changes. "Register<owner, type>(name)" creates one.
    // "public static readonly" = one shared registration for all canvases, never replaced

    // The canvas view model. In XAML: ViewModel="{Binding Canvas}"
    public static readonly StyledProperty<NetworkCanvasViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, NetworkCanvasViewModel?>(nameof(ViewModel));

    // The colors. XAML fills them from the theme with {DynamicResource ...}, so a dark / light switch reaches the canvas by itself
    public static readonly StyledProperty<IBrush?> CanvasBackgroundProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(CanvasBackground));   // the canvas floor
    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(GridBrush));          // the grid lines (step 5)
    public static readonly StyledProperty<IBrush?> ItemFillBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ItemFillBrush));      // inside a device box
    public static readonly StyledProperty<IBrush?> ItemBorderBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ItemBorderBrush));    // the line around a device box
    public static readonly StyledProperty<IBrush?> ItemTextBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ItemTextBrush));      // a device's icon and name
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(AccentBrush));        // selection, ghost, rubber band (blue)
    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(HoverBrush));         // the thing under the mouse (step 4)
    public static readonly StyledProperty<IBrush?> ConnectionBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ConnectionBrush));    // a copper cable (step 6)

    // Step 8: the status lights at the ends of a cable. XAML fills them from the theme (Brush.Success / Brush.Error)
    public static readonly StyledProperty<IBrush?> LinkUpBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(LinkUpBrush));        // a working port's light (green)
    public static readonly StyledProperty<IBrush?> LinkDownBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(LinkDownBrush));      // a port that is down (red)


    // Colors to use when the theme did not give one, so the canvas never draws with "no color".
    // Color.Parse("#RRGGBB") turns a color code into a color; SolidColorBrush = a "paint" of one color
    private static readonly IBrush FallbackBackground = new SolidColorBrush(Color.Parse("#12151A"));   // dark floor
    private static readonly IBrush FallbackGrid = new SolidColorBrush(Color.Parse("#262B33"));         // dark grey grid lines
    private static readonly IBrush FallbackItemFill = new SolidColorBrush(Color.Parse("#20242C"));     // dark device box
    private static readonly IBrush FallbackItemBorder = new SolidColorBrush(Color.Parse("#3A414D"));   // grey box line
    private static readonly IBrush FallbackItemText = new SolidColorBrush(Color.Parse("#E7EAEE"));     // light text
    private static readonly IBrush FallbackAccent = new SolidColorBrush(Color.Parse("#4FA8E8"));       // blue
    private static readonly IBrush FallbackHover = new SolidColorBrush(Color.Parse("#6BB9EE"));        // light blue
    private static readonly IBrush FallbackConnection = new SolidColorBrush(Color.Parse("#8A93A3"));   // grey cable
    private static readonly IBrush FallbackLinkUp = new SolidColorBrush(Color.Parse("#3DD68C"));       // step 8: green - a working port
    private static readonly IBrush FallbackLinkDown = new SolidColorBrush(Color.Parse("#F0665E"));     // step 8: red - a port that is down
    private static readonly IBrush SeeThroughAccent = new SolidColorBrush(Color.Parse("#334FA8E8"));   // see-through blue: "#AARRGGBB", AA = 33 = about 20% visible

    // A dashed line: "new([4, 3], 0)" = 4 units of line, then 3 units of gap, starting at 0
    private static readonly DashStyle Dashed = new([4, 3], 0);

    // Step 7: the cable kinds' looks (like Packet Tracer). Crossover = long dashes; fiber = orange; serial = red
    private static readonly DashStyle CrossoverDash = new([6, 4], 0);                            // 6 units of line, 4 of gap
    private static readonly IBrush FiberBrush = new SolidColorBrush(Color.Parse("#F29B38"));     // orange
    private static readonly IBrush SerialBrush = new SolidColorBrush(Color.Parse("#E5534B"));    // red

    private const double IconGridSize = 20.0;     // the icons in Theme/Icons.axaml are drawn on a 20 x 20 grid - used to scale them
    private const double LabelFontSize = 12;      // the size of a device's name under its icon
    private const double LinkLightRadius = 4;     // step 8: the radius of a status light, in world units (a dot 8 units across)


    // The mouse cursors, made once. Arrow = normal; Hand = "you can grab and scroll"; SizeAll = "you are moving
    // something"; Cross = "pick a point / a device"
    private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly Cursor MoveCursor = new(StandardCursorType.SizeAll);
    private static readonly Cursor CrossCursor = new(StandardCursorType.Cross);

    // Icons already looked up (device kind -> its shape, or null), so each kind is searched in the theme only once
    private readonly Dictionary<DeviceType, Geometry?> _iconCache = [];

    // ----- What the control remembers between events -----
    private NetworkCanvasViewModel? _subscribedViewModel;   // the view model whose RedrawRequested we listen to right now
    private TopLevel? _topLevel;                            // the window this control is in (null when it is not on the screen)
    private Point _pointerPosition;                         // the last mouse position, in SCREEN units, from the canvas's top-left
    private bool _isPointerInside;                          // is the mouse over the canvas right now

    // Purpose:       Tells Avalonia which properties change the drawing, and that the canvas can take the keyboard.
    // Precondition:  receives nothing. (A "static constructor" runs by itself once, before the first canvas is built.)
    // Postcondition: returns nothing - changing a color redraws the canvas by itself; the canvas can get the keyboard focus.
    static NetworkCanvasControl()
    {
        // AffectsRender = "when one of these properties changes, call Render() again"
        AffectsRender<NetworkCanvasControl>(
            CanvasBackgroundProperty, GridBrushProperty, ItemFillBrushProperty, ItemBorderBrushProperty,
            ItemTextBrushProperty, AccentBrushProperty, HoverBrushProperty, ConnectionBrushProperty,
            LinkUpBrushProperty, LinkDownBrushProperty);   // step 8: the two light colors too


        // Let the canvas receive the keyboard focus (needed for Delete, Ctrl+A, Space and Escape)
        FocusableProperty.OverrideDefaultValue<NetworkCanvasControl>(true);
    }

    // The C# side of each StyledProperty: "get" reads the value Avalonia keeps (GetValue), "set" writes it (SetValue)
    public NetworkCanvasViewModel? ViewModel { get => GetValue(ViewModelProperty); set => SetValue(ViewModelProperty, value); }
    public IBrush? CanvasBackground { get => GetValue(CanvasBackgroundProperty); set => SetValue(CanvasBackgroundProperty, value); }
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush? ItemFillBrush { get => GetValue(ItemFillBrushProperty); set => SetValue(ItemFillBrushProperty, value); }
    public IBrush? ItemBorderBrush { get => GetValue(ItemBorderBrushProperty); set => SetValue(ItemBorderBrushProperty, value); }
    public IBrush? ItemTextBrush { get => GetValue(ItemTextBrushProperty); set => SetValue(ItemTextBrushProperty, value); }
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public IBrush? HoverBrush { get => GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }
    public IBrush? ConnectionBrush { get => GetValue(ConnectionBrushProperty); set => SetValue(ConnectionBrushProperty, value); }
    public IBrush? LinkUpBrush { get => GetValue(LinkUpBrushProperty); set => SetValue(LinkUpBrushProperty, value); }       // step 8: green
    public IBrush? LinkDownBrush { get => GetValue(LinkDownBrushProperty); set => SetValue(LinkDownBrushProperty, value); } // step 8: red


    // ----- Joining the view model, and the canvas size -----

    // Purpose:       Runs when an Avalonia property of this control changes: reports a new size, or joins a new view model.
    // Precondition:  receives change - which property changed (Avalonia passes it in).
    // Postcondition: returns nothing - we listen to the current view model only, and it knows the canvas size.
    // "protected override" = we replace a method that Control already has, and only Avalonia calls it
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        // Let Avalonia do its own work first ("base" = the original method of Control)
        base.OnPropertyChanged(change);

        // The canvas got a new size (e.g. the window was resized) - tell the view model ("Fit to View" needs it)
        if (change.Property == BoundsProperty)
        {
            ViewModel?.SetViewportSize(Bounds.Width, Bounds.Height);   // "?." = only if there is a view model
            return;   // stop here
        }

        // From here on only the ViewModel property matters
        if (change.Property != ViewModelProperty)
        {
            return;   // stop here - some other property changed
        }

        // Stop listening to the old view model, if there was one ("-=" = stop listening)
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.RedrawRequested -= OnRedrawRequested;
        }

        // Remember the new one, listen to it ("+="), and tell it our size
        _subscribedViewModel = ViewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.RedrawRequested += OnRedrawRequested;
            _subscribedViewModel.SetViewportSize(Bounds.Width, Bounds.Height);
        }

        InvalidateVisual();   // "the picture is out of date" - draw again with the new view model
    }

    // Purpose:       The view model asks for a redraw.
    // Precondition:  receives sender, e - the event's values (not used); called by RedrawRequested.
    // Postcondition: returns nothing - the cursor fits what is happening, and Avalonia will call Render() again soon.
    private void OnRedrawRequested(object? sender, EventArgs e)
    {
        UpdateCursor();       // e.g. a hand while panning
        InvalidateVisual();   // "the picture is out of date"
    }

    // ----- Listening to the whole window (step 3: dragging from the Device Library; Escape) -----

    // Purpose:       Runs when the canvas appears on the screen - starts listening to the WHOLE window.
    // Precondition:  receives e - where the control was attached (Avalonia passes it in).
    // Postcondition: returns nothing - OnWindowPointerMoved / OnWindowPointerReleased / OnWindowKeyDown now run for the whole window.
    // Why: a pressed Device Library card "captures" the mouse, so the moves and the release go to the CARD - they still
    // travel up ("bubble") through the window, which is the one place where the canvas can see them
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);              // Avalonia's own work first
        _topLevel = TopLevel.GetTopLevel(this);      // the window this control is inside

        // Not inside a window (should not happen) - nothing to listen to
        if (_topLevel is null)
        {
            return;   // stop here
        }

        // AddHandler(event, method, route, handledEventsToo):
        //   Bubble = hear the event on its way UP from the element that got it (e.g. the card);
        //   handledEventsToo: true = hear it even if that element already marked it "handled"
        _topLevel.AddHandler(PointerMovedEvent, OnWindowPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        _topLevel.AddHandler(PointerReleasedEvent, OnWindowPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);

        // Tunnel = hear every key on its way DOWN, before any other control - so Escape works from anywhere
        _topLevel.AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    // Purpose:       Runs when the canvas leaves the screen - stops listening to the window.
    // Precondition:  receives e - where the control was detached from (Avalonia passes it in).
    // Postcondition: returns nothing - the window no longer calls this control (so it can be freed from memory).
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);   // Avalonia's own work first

        // Only if we were listening
        if (_topLevel is not null)
        {
            _topLevel.RemoveHandler(PointerMovedEvent, OnWindowPointerMoved);         // stop hearing mouse moves
            _topLevel.RemoveHandler(PointerReleasedEvent, OnWindowPointerReleased);   // stop hearing mouse releases
            _topLevel.RemoveHandler(KeyDownEvent, OnWindowKeyDown);                   // stop hearing key presses
            _topLevel = null;                                                         // no window any more
        }
    }

    // Purpose:       Every mouse move in the window - keeps the mouse position for the ghost.
    // Precondition:  receives sender - who raised it; e - the mouse event.
    // Postcondition: returns nothing - _pointerPosition / _isPointerInside are up to date; redrawn while a ghost is shown.
    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        _pointerPosition = e.GetPosition(this);                               // the mouse, measured from THIS canvas's top-left corner
        _isPointerInside = new Rect(Bounds.Size).Contains(_pointerPosition);  // is it inside the canvas's box (from (0,0), the canvas size)?

        // The ghost follows the mouse only while a device is waiting to be placed.
        // "?." = if there is no view model the result is null; "== true" turns that null into false
        if (ViewModel?.IsPlacingDevice == true)
        {
            InvalidateVisual();   // draw again, with the ghost at the new place
        }
    }

    // Purpose:       Every mouse release in the window - finishes a drag from the Device Library.
    // Precondition:  receives sender - who raised it; e - the mouse event.
    // Postcondition: returns nothing - if the left button was let go over the canvas while a device was waiting, the
    //                device was placed there; otherwise nothing changed.
    private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Only the left button, and only while a device is waiting.
        // "ViewModel is not { IsPlacingDevice: true } viewModel" = if there is no view model, or it is not placing -> stop;
        // otherwise keep it in "viewModel"
        if (e.InitialPressMouseButton != MouseButton.Left || ViewModel is not { IsPlacingDevice: true } viewModel)
        {
            return;   // stop here
        }

        // Where the button was let go (screen units, from the canvas's top-left)
        var position = e.GetPosition(this);

        // Let go OUTSIDE the canvas (e.g. a plain click on a card) - not a drop; the device keeps waiting
        if (!new Rect(Bounds.Size).Contains(position))
        {
            return;   // stop here
        }

        // A drop: place the device at the WORLD point under the mouse (ToWorld, below, goes through the camera)
        viewModel.ConfirmPlacement(ToWorld(viewModel, position));
    }

    // Purpose:       Every key press in the window - Escape cancels "place a device", or closes the port list, from
    //                anywhere (the port list's buttons take the keyboard focus away from the canvas).
    // Precondition:  receives sender - who raised it; e - the key event.
    // Postcondition: returns nothing - Escape while placing -> nothing is waiting; Escape while the port list is open
    //                (step 7) -> the half-made cable is given up. In both cases the key is used up.
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Only Escape, and only with a view model
        if (e.Key != Key.Escape || ViewModel is not { } viewModel)
        {
            return;   // stop here
        }

        if (viewModel.IsPlacingDevice)
        {
            viewModel.CancelPlacement();           // stop waiting to place
        }
        else if (viewModel.IsPortPickerOpen)
        {
            viewModel.ConnectionTool.Cancel();     // close the port list and give up the cable
        }
        else
        {
            return;   // stop here - any other Escape is handled by the canvas's own OnKeyDown
        }

        e.Handled = true;   // "used up" - no other control reacts to this Escape
    }

    // ----- The mouse on the canvas itself -----

    // Purpose:       A mouse button was pressed ON the canvas: place, connect, or start a select / drag / pan.
    // Precondition:  receives e - the mouse event (Avalonia passes it in).
    // Postcondition: returns nothing - the canvas has the keyboard focus, and the press went to the right place:
    //                placing -> ConfirmPlacement; Connect tool -> ConnectionTool.OnClick; otherwise -> Interaction.
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);   // Avalonia's own work first
        Focus();                    // take the keyboard focus, so keys (Delete, Space...) now come to the canvas

        // No view model - nothing to do
        if (ViewModel is not { } viewModel)
        {
            return;   // stop here
        }

        var point = e.GetCurrentPoint(this);                  // where the press was, and which buttons are down
        var button = ToCanvasButton(point.Properties);        // turned into our own button kind (null = a button we don't use)
        if (button is null)
        {
            return;   // stop here
        }

        var screen = ToCanvasPoint(point.Position);           // the press, in screen units, as our CanvasPoint
        var world = viewModel.Viewport.ScreenToWorld(screen); // the same place in the world, through the camera

        // Step 3: a device is waiting - a left click places it there
        if (viewModel.IsPlacingDevice && button == CanvasPointerButton.Left)
        {
            viewModel.ConfirmPlacement(world);
            e.Handled = true;   // the click is used up
            return;             // stop here
        }

        // Step 6: the Connect tool - a left click goes to the tool (unless Space is held: then the press scrolls instead)
        if (viewModel.IsConnectToolActive && button == CanvasPointerButton.Left && !viewModel.Interaction.IsSpaceHeld)
        {
            viewModel.ConnectionTool.OnClick(world);
            e.Handled = true;   // used up
            return;             // stop here
        }
        // Step 9: a DOUBLE-click with the left button opens the device's properties panel.
        // e.ClickCount = how many quick clicks in a row this press is - Avalonia counts them for us:
        // 1 = a normal click, 2 = the SECOND click of a double-click. (The first click of the pair already went through
        // the normal path below and selected the device - so a double-click both selects the device and opens its panel)
        if (e.ClickCount == 2 && button == CanvasPointerButton.Left)
        {
            // Ask the view model to open the properties of the device under the double-click (part 9.4).
            // "world" = the press as a WORLD point (worked out a few lines above, through the camera)
            viewModel.RequestDeviceProperties(world);

            e.Handled = true;   // used up - this second click must NOT also start a drag / a selection rectangle
            return;             // stop here
        }


        // Steps 4-5: select / drag / rectangle / pan.
        // "Capture" = keep getting this mouse's moves and its release even if it leaves the canvas, so a drag never gets "stuck"
        e.Pointer.Capture(this);
        // button.Value = the button without the "?" (we know it is not null here); IsCtrlHeld (below) = is Ctrl down
        viewModel.Interaction.OnPointerPressed(screen, button.Value, IsCtrlHeld(e.KeyModifiers));
        e.Handled = true;   // used up
    }

    // Purpose:       The mouse moved over the canvas (or anywhere while the canvas captured it).
    // Precondition:  receives e - the mouse event.
    // Postcondition: returns nothing - the select / drag / pan logic got the move; the Connect tool got it too when it is on.
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);   // Avalonia's own work first

        // No view model - nothing to do
        if (ViewModel is not { } viewModel)
        {
            return;   // stop here
        }

        var screen = ToCanvasPoint(e.GetPosition(this));   // the mouse, in screen units
        viewModel.Interaction.OnPointerMoved(screen);      // hover, drag, rectangle, pan, and the corner coordinates

        // The Connect tool's highlight and rubber band follow the mouse too (it works in world points)
        if (viewModel.IsConnectToolActive)
        {
            viewModel.ConnectionTool.OnPointerMoved(viewModel.Viewport.ScreenToWorld(screen));
        }
    }

    // Purpose:       A mouse button was let go over the canvas (or anywhere while the canvas captured it).
    // Precondition:  receives e - the mouse event.
    // Postcondition: returns nothing - the drag / rectangle / pan ended, and the mouse is no longer captured.
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);   // Avalonia's own work first

        // There is a view model AND the button is one we use ("is { } button" = keep it) - tell the mouse logic
        if (ViewModel is { } viewModel && ToCanvasButton(e.InitialPressMouseButton) is { } button)
        {
            viewModel.Interaction.OnPointerReleased(ToCanvasPoint(e.GetPosition(this)), button);
        }

        e.Pointer.Capture(null);   // let go of the mouse (null = capture nothing)
    }

    // Purpose:       The mouse left the canvas - no hover, no ghost.
    // Precondition:  receives e - the mouse event.
    // Postcondition: returns nothing - nothing is hovered; the canvas is redrawn.
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);                    // Avalonia's own work first
        _isPointerInside = false;                   // the mouse is not over the canvas
        ViewModel?.Interaction.OnPointerExited();   // forget what was hovered ("?." = only if there is a view model)
        InvalidateVisual();                         // draw again, without the ghost
    }

    // Purpose:       The mouse wheel over the canvas - zoom around the mouse (step 5).
    // Precondition:  receives e - the wheel event (e.Delta.Y above 0 = wheel up).
    // Postcondition: returns nothing - the camera zoomed; the wheel is used up (nothing else scrolls).
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);   // Avalonia's own work first

        if (ViewModel is { } viewModel)
        {
            viewModel.Interaction.OnWheel(ToCanvasPoint(e.GetPosition(this)), e.Delta.Y);   // zoom around the mouse
            e.Handled = true;                                                                // used up
        }
    }

    // ----- Keys on the canvas (the canvas must have the focus - it takes it on every click) -----

    // Purpose:       A key was pressed while the canvas has the focus.
    // Precondition:  receives e - the key event.
    // Postcondition: returns nothing - Space -> "Space held" (a drag scrolls); Escape -> give up the cable / leave the
    //                Connect tool / cancel the drag or un-select; Ctrl+A -> select all; Delete / Backspace -> delete the selection.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);   // Avalonia's own work first

        // No view model - nothing to do
        if (ViewModel is not { } viewModel)
        {
            return;   // stop here
        }

        // "switch" = pick the "case" that matches the key
        switch (e.Key)
        {
            case Key.Space:
                viewModel.Interaction.SetSpaceHeld(true);   // holding Space + dragging = scroll
                e.Handled = true;                           // used up
                break;                                      // leave the switch

            case Key.Escape:
                if (viewModel.IsConnectToolActive && viewModel.ConnectionTool.HasPendingDevice)
                {
                    viewModel.ConnectionTool.Cancel();               // first Escape: give up the half-made cable
                }
                else if (viewModel.IsConnectToolActive)
                {
                    viewModel.SelectToolCommand.Execute(null);       // second Escape: back to the Select tool
                }
                else
                {
                    viewModel.Interaction.OnEscape();                // otherwise: undo a drag / rectangle, or un-select
                }

                e.Handled = true;   // used up
                break;

            // "when" = this case counts only if the extra condition is true too: the A key WITH Ctrl held
            case Key.A when IsCtrlHeld(e.KeyModifiers):
                viewModel.SelectAllCommand.Execute(null);   // Ctrl + A = select all
                e.Handled = true;
                break;

            // Two cases stacked = either key does the same thing
            case Key.Delete:
            case Key.Back:
                // Delete only when something is selected - CanExecute asks the command's condition (HasSelection)
                if (viewModel.DeleteSelectionCommand.CanExecute(null))
                {
                    viewModel.DeleteSelectionCommand.Execute(null);
                }

                e.Handled = true;
                break;
        }
    }

    // Purpose:       A key was let go while the canvas has the focus.
    // Precondition:  receives e - the key event.
    // Postcondition: returns nothing - letting go of Space ends "Space held".
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);   // Avalonia's own work first

        if (e.Key == Key.Space)
        {
            ViewModel?.Interaction.SetSpaceHeld(false);   // Space is up again
            e.Handled = true;                             // used up
        }
    }

    // ----- Drawing -----

    // Purpose:       Draws the whole canvas, in layers: floor, grid, then (in world units) cables, rubber band, devices,
    //                selection rectangle, ghost.
    // Precondition:  receives context - where to draw (Avalonia passes it in whenever the picture is out of date).
    // Postcondition: returns nothing - the canvas on the screen shows the current state.
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);   // the whole canvas: a box from (0, 0) with the canvas's size

        // The floor. "??" = the theme's color, or the fallback when there is none
        context.FillRectangle(CanvasBackground ?? FallbackBackground, bounds);

        // No view model yet - only the floor
        if (ViewModel is not { } viewModel)
        {
            return;   // stop here
        }

        // The grid is drawn in SCREEN units (so its lines stay 1 unit thin at every zoom)
        if (viewModel.IsGridVisible)
        {
            DrawGrid(context, bounds, viewModel);
        }

        // Everything else lives in the world. The transform: first move by -Origin (CreateTranslation), then grow by Zoom
        // (CreateScale). "*" joins them in that order. That is exactly CanvasViewport.WorldToScreen, done once for the whole picture
        var viewport = viewModel.Viewport;
        var worldToScreen = Matrix.CreateTranslation(-viewport.Origin.X, -viewport.Origin.Y) * Matrix.CreateScale(viewport.Zoom, viewport.Zoom);

        // "using (PushTransform(...))" = until the end of this block, everything drawn goes through the transform; at the end it is removed by itself
        using (context.PushTransform(worldToScreen))
        {
            DrawConnections(context, viewModel);      // cables FIRST, so the devices are drawn on top of their ends
            DrawRubberBand(context, viewModel);       // the cable being made (Connect tool)
            foreach (var item in viewModel.Items)     // every device, in order - the last one ends up on top
            {
                DrawItem(context, viewModel, item);
            }
            DrawLinkLights(context, viewModel);           // step 8: the status lights - AFTER the devices, so no device ever hides them
            DrawSelectionRectangle(context, viewModel);   // the rectangle being drawn
            DrawPlacementGhost(context, viewModel);       // the device being placed, on top of everything
        }
    }

    // Purpose:       Draws the grid lines that are inside the canvas.
    // Precondition:  receives context - where to draw; bounds - the canvas area; viewModel - for the camera and the spacing.
    // Postcondition: returns nothing - vertical and horizontal lines every GridSpacing world units are drawn (skipped when
    //                zoomed out so far that the lines would be under 4 units apart - that would look like a smear).
    private void DrawGrid(DrawingContext context, Rect bounds, NetworkCanvasViewModel viewModel)
    {
        var viewport = viewModel.Viewport;                       // the camera
        var spacing = viewModel.GridSpacing;                     // world units between two lines

        // On the screen the lines are spacing * zoom apart; under 4 is too dense to be useful - don't draw it
        if (spacing * viewport.Zoom < 4)
        {
            return;   // stop here
        }

        var pen = new Pen(GridBrush ?? FallbackGrid, 1);         // thin grid lines, 1 unit wide

        // The part of the world that is visible: the world point under the top-left corner, and under the bottom-right corner
        var topLeft = viewport.ScreenToWorld(new CanvasPoint(0, 0));
        var bottomRight = viewport.ScreenToWorld(new CanvasPoint(bounds.Width, bounds.Height));

        // Vertical lines. Start at the first grid line at or left of the visible area: Math.Floor(x / spacing) * spacing
        // rounds DOWN to a multiple of the spacing. Then every "spacing" (+=) until past the right edge
        for (var worldX = Math.Floor(topLeft.X / spacing) * spacing; worldX <= bottomRight.X; worldX += spacing)
        {
            var screenX = viewport.WorldToScreen(new CanvasPoint(worldX, 0)).X;              // where that world line is on the screen
            context.DrawLine(pen, new Point(screenX, 0), new Point(screenX, bounds.Height));  // from the top of the canvas to the bottom
        }

        // Horizontal lines: the same, from the top to the bottom
        for (var worldY = Math.Floor(topLeft.Y / spacing) * spacing; worldY <= bottomRight.Y; worldY += spacing)
        {
            var screenY = viewport.WorldToScreen(new CanvasPoint(0, worldY)).Y;              // where that world line is on the screen
            context.DrawLine(pen, new Point(0, screenY), new Point(bounds.Width, screenY));   // from the left of the canvas to the right
        }
    }

    // Purpose:       Draws every cable as a line from device middle to device middle (steps 6-7).
    // Precondition:  receives context - where to draw (inside the world transform); viewModel - for the cables and their state.
    // Postcondition: returns nothing - each cable is drawn in its kind's style (step 7, like Packet Tracer):
    //                straight-through = solid grey, crossover = dashed grey, fiber = orange, serial = red.
    //                Selected -> the accent color and thicker; hovered -> the hover color. The dashes stay, so the kind is still seen.
    private void DrawConnections(DrawingContext context, NetworkCanvasViewModel viewModel)
    {
        var hoveredId = viewModel.Interaction.HoveredConnectionId;   // the cable under the mouse (or null)

        // One line per cable
        foreach (var cable in viewModel.Connections)
        {
            var isSelected = viewModel.IsSelected(cable.Id);   // is this cable selected?

            // The color, by priority ("a ? b : c ? d : e" = if a then b, otherwise if c then d, otherwise e):
            // selected -> accent; hovered -> hover; otherwise the color of its kind (CableBrush, below)
            var brush = isSelected ? AccentBrush ?? FallbackAccent
                : hoveredId == cable.Id ? HoverBrush ?? FallbackHover
                : CableBrush(cable.ConnectionType);

            // The pen: the color, 3 units wide when selected and 2 otherwise. "{ ... }" sets two more settings right after building it
            var pen = new Pen(brush, isSelected ? 3 : 2)
            {
                LineCap = PenLineCap.Round,                                                                  // round line ends
                DashStyle = cable.ConnectionType == ConnectionType.CopperCrossover ? CrossoverDash : null,   // crossover = dashed; anything else = solid (null)
            };

            // The line itself, from the first device's middle (A) to the second's (B)
            context.DrawLine(pen, ToPoint(cable.A), ToPoint(cable.B));
        }
    }

    // Purpose:       Picks the color of a cable kind (step 7).
    // Precondition:  receives cableType - the cable's kind.
    // Postcondition: returns the color: fiber -> orange, serial -> red, copper (both kinds) -> the theme's cable color.
    private IBrush CableBrush(ConnectionType cableType) => cableType switch
    {
        ConnectionType.Fiber => FiberBrush,                                // orange, like Packet Tracer
        ConnectionType.Serial => SerialBrush,                              // red, like Packet Tracer
        _ => ConnectionBrush ?? FallbackConnection,                        // any other (copper) - the normal cable color
    };
    // Purpose:       Step 8 - draws a status light at each end of every cable: green when the port at that end is working,
    //                red when it is down (a wrong cable kind, or a port that is turned off).
    // Precondition:  receives context - where to draw (inside the world transform); viewModel - for the cables.
    // Postcondition: returns nothing - two small round lights are drawn for every cable, one just outside each device.
    private void DrawLinkLights(DrawingContext context, NetworkCanvasViewModel viewModel)
    {
        var upBrush = LinkUpBrush ?? FallbackLinkUp;           // the green: the theme's color, or the fallback when there is none
        var downBrush = LinkDownBrush ?? FallbackLinkDown;     // the red: the theme's color, or the fallback

        // A thin ring around each light, in the color of the canvas floor - it separates the light from the cable line
        // that runs under it, so the dot reads clearly on any cable color
        var ring = new Pen(CanvasBackground ?? FallbackBackground, 1.5);

        // Two lights for every cable
        foreach (var cable in viewModel.Connections)
        {
            // The first end: "a ? b : c" = green when that end's port is working (IsEndAUp), red otherwise.
            // DrawEllipse(fill, ring, middle point, radius across, radius down) - the same radius twice = a circle
            context.DrawEllipse(cable.IsEndAUp ? upBrush : downBrush, ring, ToPoint(cable.LightA), LinkLightRadius, LinkLightRadius);

            // The second end: the same, with its own point and its own port's state
            context.DrawEllipse(cable.IsEndBUp ? upBrush : downBrush, ring, ToPoint(cable.LightB), LinkLightRadius, LinkLightRadius);
        }
    }


    // Purpose:       Draws the "rubber band": a dashed line from the first picked device to the mouse (step 6).
    // Precondition:  receives context - where to draw (inside the world transform); viewModel - for the Connect tool.
    // Postcondition: returns nothing - the line is drawn only while the Connect tool has a first device picked.
    private void DrawRubberBand(DrawingContext context, NetworkCanvasViewModel viewModel)
    {
        var tool = viewModel.ConnectionTool;   // a short name for the Connect tool

        // Only while the Connect tool is on AND a first device is picked ("is not { } pendingId" = none -> stop)
        if (!viewModel.IsConnectToolActive || tool.PendingDeviceId is not { } pendingId)
        {
            return;   // stop here
        }

        // Find the picked device's item (its middle is where the band starts)
        foreach (var item in viewModel.Items)
        {
            if (item.Id == pendingId)
            {
                var pen = new Pen(AccentBrush ?? FallbackAccent, 2) { DashStyle = Dashed };    // a dashed accent pen, 2 units wide
                context.DrawLine(pen, ToPoint(item.Center), ToPoint(tool.PointerWorldPosition)); // from the device's middle to the mouse
                return;   // done - there is only one picked device
            }
        }
    }

    // Purpose:       Draws one device: a rounded box (its border by its state), its icon and its name.
    // Precondition:  receives context - where to draw (inside the world transform); viewModel - for its state;
    //                item - the device on the canvas.
    // Postcondition: returns nothing - the device is drawn: accent border when selected or picked by the Connect tool,
    //                hover border when the mouse is over it, the normal border otherwise.
    private void DrawItem(DrawingContext context, NetworkCanvasViewModel viewModel, CanvasItem item)
    {
        // The box, in world units: its top-left corner, width and height
        var rect = new Rect(item.Position.X, item.Position.Y, item.Size.Width, item.Size.Height);

        // Its state
        var isSelected = viewModel.IsSelected(item.Id);                                                        // selected (step 4)
        var isPicked = viewModel.IsConnectToolActive && viewModel.ConnectionTool.PendingDeviceId == item.Id;   // the first end of a cable (step 6)
        var isHovered = viewModel.IsConnectToolActive
            ? viewModel.ConnectionTool.HoveredDeviceId == item.Id                                              // Connect tool: its own hover
            : viewModel.Interaction.HoveredItemId == item.Id;                                                  // otherwise: the normal hover

        // The border - the strongest state wins: selected / picked (accent, 2.5 wide), then hovered (hover, 1.5), then normal (1)
        var borderPen = isSelected || isPicked ? new Pen(AccentBrush ?? FallbackAccent, 2.5)
            : isHovered ? new Pen(HoverBrush ?? FallbackHover, 1.5)
            : new Pen(ItemBorderBrush ?? FallbackItemBorder, 1);

        // The box: filled, with the border, corners rounded by 8 (the two 8s = the corner's width and height)
        context.DrawRectangle(ItemFillBrush ?? FallbackItemFill, borderPen, rect, 8, 8);

        var textBrush = ItemTextBrush ?? FallbackItemText;   // the color of the icon and the name

        // The icon, in the upper 60% of the box (4 units below its top), so the name fits under it
        if (GetDeviceIcon(item.DeviceType) is { } icon)
        {
            DrawIconCentered(context, icon, new Rect(rect.X, rect.Y + 4, rect.Width, rect.Height * 0.6), textBrush);
        }

        // The name. FormattedText = text ready to draw, with its size measured. Its values: the text, the language rules,
        // left-to-right, the default font, the size, the color
        var label = new FormattedText(item.Label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, textBrush);
        // Draw it centered across (the box's middle - half the text's width), 4 units above the bottom of the box
        context.DrawText(label, new Point(rect.Center.X - label.Width / 2, rect.Bottom - label.Height - 4));
    }

    // Purpose:       Draws the selection rectangle while the user drags on empty canvas (step 4).
    // Precondition:  receives context - where to draw (inside the world transform); viewModel - for the rectangle.
    // Postcondition: returns nothing - a see-through box with a dashed border is drawn, or nothing when no rectangle is being drawn.
    private void DrawSelectionRectangle(DrawingContext context, NetworkCanvasViewModel viewModel)
    {
        // No rectangle right now ("is not { } rectangle" = null -> stop; otherwise keep it)
        if (viewModel.Interaction.SelectionRectangle is not { } rectangle)
        {
            return;   // stop here
        }

        var rect = new Rect(ToPoint(rectangle.Min), ToPoint(rectangle.Max));    // the box from its top-left corner to its bottom-right corner
        // Fill it see-through blue, with a 1-unit dashed accent border
        context.DrawRectangle(SeeThroughAccent, new Pen(AccentBrush ?? FallbackAccent, 1) { DashStyle = Dashed }, rect);
    }

    // Purpose:       Draws the ghost of the device waiting to be placed, centered under the mouse (step 3).
    // Precondition:  receives context - where to draw (inside the world transform); viewModel - for the waiting kind and the camera.
    // Postcondition: returns nothing - the ghost is drawn only while a device is waiting and the mouse is over the canvas.
    private void DrawPlacementGhost(DrawingContext context, NetworkCanvasViewModel viewModel)
    {
        // The mouse is not over the canvas, OR nothing is waiting - no ghost
        if (!_isPointerInside || viewModel.PendingPlacementDeviceType is not { } deviceType)
        {
            return;   // stop here
        }

        // The mouse in the world (through the camera), and a device-sized box centered on it
        var center = viewModel.Viewport.ScreenToWorld(ToCanvasPoint(_pointerPosition));
        var size = CanvasItem.DefaultDeviceSize;                                                               // the same size a placed device gets
        var rect = new Rect(center.X - size.Width / 2, center.Y - size.Height / 2, size.Width, size.Height);   // middle - half size = top-left

        var accent = AccentBrush ?? FallbackAccent;
        // A see-through box with a dashed accent border and rounded corners - so it never looks like a placed device
        context.DrawRectangle(SeeThroughAccent, new Pen(accent, 1.5) { DashStyle = Dashed }, rect, 8, 8);

        // The device's icon in the middle, in the accent color
        if (GetDeviceIcon(deviceType) is { } icon)
        {
            DrawIconCentered(context, icon, rect, accent);
        }
    }

    // ----- Small helpers -----

    // Purpose:       Picks the mouse cursor that fits what is happening.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - Cursor is: "move" while panning / dragging, a hand when a drag would scroll,
    //                a cross for placing or connecting, otherwise the arrow.
    private void UpdateCursor()
    {
        // No view model - the normal arrow
        if (ViewModel is not { } viewModel)
        {
            Cursor = ArrowCursor;
            return;   // stop here
        }

        var mode = viewModel.Interaction.Mode;   // what the mouse is doing
        // By priority: moving something -> MoveCursor; a drag would scroll -> HandCursor;
        // picking a point / a device -> CrossCursor; otherwise -> ArrowCursor
        Cursor = mode is CanvasInteractionMode.Panning or CanvasInteractionMode.DraggingItems ? MoveCursor
            : viewModel.IsPanToolActive || viewModel.Interaction.IsSpaceHeld ? HandCursor
            : viewModel.IsPlacingDevice || viewModel.IsConnectToolActive ? CrossCursor
            : ArrowCursor;
    }

    // Purpose:       Finds a device kind's icon in the theme (Theme/Icons.axaml).
    // Precondition:  receives deviceType - the device kind (the control must be on the screen, to reach the app's resources).
    // Postcondition: returns the icon's shape (Geometry), or null when it was not found. Each kind is looked up only once.
    private Geometry? GetDeviceIcon(DeviceType deviceType)
    {
        // Already looked up before - use the remembered answer ("out var cached" = the value, if found)
        if (_iconCache.TryGetValue(deviceType, out var cached))
        {
            return cached;
        }

        // The resource name (e.g. "Icon.Router", from DeviceIcons) -> search this control, its parents, then the app.
        // TryFindResource answers true / false and hands the resource back through "out var resource";
        // "resource as Geometry" = use it only if it really is a shape, otherwise null
        var icon = this.TryFindResource(DeviceIcons.ResourceKeyFor(deviceType), out var resource) ? resource as Geometry : null;

        _iconCache[deviceType] = icon;   // remember the answer (even null), so the next time is instant
        return icon;                     // hand it back
    }

    // Purpose:       Draws an icon in the middle of an area, scaled to 55% of the area's smaller side.
    // Precondition:  receives context - where to draw; icon - the shape (drawn on a 20 x 20 grid); area - the box to center
    //                it in; brush - its color.
    // Postcondition: returns nothing - the icon is drawn centered in the area.
    private static void DrawIconCentered(DrawingContext context, Geometry icon, Rect area, IBrush brush)
    {
        var targetSize = Math.Min(area.Width, area.Height) * 0.55;   // how big the icon should be: 55% of the smaller side
        if (targetSize <= 0)
        {
            return;   // no room at all - draw nothing
        }

        var scale = targetSize / IconGridSize;                             // how much to grow the 20 x 20 icon (e.g. 22 / 20 = 1.1)
        var offsetX = area.Center.X - (IconGridSize * scale) / 2;         // the icon's left edge, so it is centered across
        var offsetY = area.Center.Y - (IconGridSize * scale) / 2;         // the icon's top edge, so it is centered down

        // Until the end of the block: FIRST grow (CreateScale), THEN move into place (CreateTranslation)
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            context.DrawGeometry(brush, null, icon);   // fill the shape with the color; null = no outline
        }
    }

    // Purpose:       Turns a screen position (from the canvas's top-left) into the world point under it.
    // Precondition:  receives viewModel - for the camera (not null); screen - the position on the screen.
    // Postcondition: returns the world point, through the camera.
    private static CanvasPoint ToWorld(NetworkCanvasViewModel viewModel, Point screen) =>
        viewModel.Viewport.ScreenToWorld(ToCanvasPoint(screen));

    // Purpose:       Turns Avalonia's Point into our CanvasPoint (the Application layer does not know Avalonia).
    // Precondition:  receives point - an Avalonia point.
    // Postcondition: returns a CanvasPoint with the same X and Y.
    private static CanvasPoint ToCanvasPoint(Point point) => new(point.X, point.Y);

    // Purpose:       Turns our CanvasPoint into Avalonia's Point (Avalonia draws with its own Point).
    // Precondition:  receives point - a CanvasPoint.
    // Postcondition: returns an Avalonia Point with the same X and Y.
    private static Point ToPoint(CanvasPoint point) => new(point.X, point.Y);

    // Purpose:       Answers whether Ctrl is held (Ctrl + click adds to the selection).
    // Precondition:  receives modifiers - the keys held during the event (Ctrl, Shift, Alt...).
    // Postcondition: returns true when Ctrl is one of them (HasFlag = "is this bit on?"), otherwise false.
    private static bool IsCtrlHeld(KeyModifiers modifiers) => modifiers.HasFlag(KeyModifiers.Control);

    // Purpose:       Tells which button is pressed, as our own CanvasPointerButton.
    // Precondition:  receives properties - the press event's button details.
    // Postcondition: returns Left / Middle / Right, or null for another button.
    private static CanvasPointerButton? ToCanvasButton(PointerPointProperties properties) =>
        properties.IsLeftButtonPressed ? CanvasPointerButton.Left           // the left button is down
        : properties.IsMiddleButtonPressed ? CanvasPointerButton.Middle     // otherwise the wheel button
        : properties.IsRightButtonPressed ? CanvasPointerButton.Right       // otherwise the right button
        : null;                                                             // none of them

    // Purpose:       Tells which button was let go, as our own CanvasPointerButton.
    // Precondition:  receives button - Avalonia's button.
    // Postcondition: returns Left / Middle / Right, or null for another button.
    private static CanvasPointerButton? ToCanvasButton(MouseButton button) => button switch
    {
        MouseButton.Left => CanvasPointerButton.Left,       // left
        MouseButton.Middle => CanvasPointerButton.Middle,   // the wheel button
        MouseButton.Right => CanvasPointerButton.Right,     // right
        _ => null,                                          // anything else
    };
}
