using System;                        // Math, EventArgs
using System.Collections.Generic;    // Dictionary<TKey, TValue>
using System.Globalization;          // CultureInfo - for FormattedText
using Avalonia;                      // AvaloniaProperty, StyledProperty, Rect, Point, Size, Matrix
using Avalonia.Controls;             // Control, TopLevel
using Avalonia.Input;                // pointer / key events, Key, KeyModifiers, MouseButton, Cursor
using Avalonia.Interactivity;        // RoutingStrategies - which way an event travels through the screen
using Avalonia.Media;                // DrawingContext, IBrush, Pen, Geometry, FormattedText, Typeface, DashStyle
using NetSim.Application.Canvas;     // CanvasItem, CanvasPoint, CanvasPointerButton, CanvasTool, CanvasInteractionMode
using NetSim.Core.Devices;           // DeviceType
using NetSim.UI.Devices;             // DeviceIcons - which icon draws each device kind
using NetSim.UI.ViewModels;          // NetworkCanvasViewModel

namespace NetSim.UI.Controls;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The Network Canvas on the screen. It DRAWS everything itself in Render() and turns the mouse and the
// keyboard into calls on NetworkCanvasViewModel and its two controllers. It holds no rules of its own.
//   Step 3: placing a device (click-then-click, or drag from the Device Library), the ghost, Escape.
//   Step 4: select (click, Ctrl+click, rectangle), drag to move, highlight, Delete, Ctrl+A.
//   Step 5: the grid, wheel zoom around the mouse, scrolling (middle button / Space+drag / Pan tool).
//   Step 6: the cables, and the Connect tool (highlight + a dashed "rubber band" line to the mouse).
// How the camera works here: everything that lives in the world (devices, cables, the selection
// rectangle, the ghost) is drawn in WORLD units, inside one "transform" that moves and grows the picture
// by the camera (Viewport). Only the grid is drawn in screen units.
// ": Control" = a basic Avalonia screen element that we draw by hand
public sealed class NetworkCanvasControl : Control
{
    // ----- Properties that can be set from XAML (see NetworkWorkspaceView.axaml) -----

    // The canvas view model: ViewModel="{Binding Canvas}"
    public static readonly StyledProperty<NetworkCanvasViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, NetworkCanvasViewModel?>(nameof(ViewModel));

    // The colors, from the theme with {DynamicResource ...}, so dark / light reaches the canvas by itself
    public static readonly StyledProperty<IBrush?> CanvasBackgroundProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(CanvasBackground));   // the floor
    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(GridBrush));          // the grid lines (step 5)
    public static readonly StyledProperty<IBrush?> ItemFillBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ItemFillBrush));      // inside a device box
    public static readonly StyledProperty<IBrush?> ItemBorderBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ItemBorderBrush));    // the line around a device box
    public static readonly StyledProperty<IBrush?> ItemTextBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ItemTextBrush));      // the icon and the name
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(AccentBrush));        // selection, ghost, rubber band
    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(HoverBrush));         // the thing under the mouse (step 4)
    public static readonly StyledProperty<IBrush?> ConnectionBrushProperty =
        AvaloniaProperty.Register<NetworkCanvasControl, IBrush?>(nameof(ConnectionBrush));    // a cable (step 6)

    // Colors to use when the theme did not give one
    private static readonly IBrush FallbackBackground = new SolidColorBrush(Color.Parse("#12151A"));
    private static readonly IBrush FallbackGrid = new SolidColorBrush(Color.Parse("#262B33"));
    private static readonly IBrush FallbackItemFill = new SolidColorBrush(Color.Parse("#20242C"));
    private static readonly IBrush FallbackItemBorder = new SolidColorBrush(Color.Parse("#3A414D"));
    private static readonly IBrush FallbackItemText = new SolidColorBrush(Color.Parse("#E7EAEE"));
    private static readonly IBrush FallbackAccent = new SolidColorBrush(Color.Parse("#4FA8E8"));
    private static readonly IBrush FallbackHover = new SolidColorBrush(Color.Parse("#6BB9EE"));
    private static readonly IBrush FallbackConnection = new SolidColorBrush(Color.Parse("#8A93A3"));
    private static readonly IBrush SeeThroughAccent = new SolidColorBrush(Color.Parse("#334FA8E8"));   // "33" = about 20% visible

    private static readonly DashStyle Dashed = new([4, 3], 0);   // 4 units of line, 3 units of gap

    private const double IconGridSize = 20.0;     // the icons are drawn on a 20 x 20 grid (Theme/Icons.axaml)
    private const double LabelFontSize = 12;      // the device name's size

    // The mouse cursors (made once). Hand = "you can grab", SizeAll = "you are moving it", Cross = "pick a point"
    private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly Cursor MoveCursor = new(StandardCursorType.SizeAll);
    private static readonly Cursor CrossCursor = new(StandardCursorType.Cross);

    // Icons already looked up, so each kind is searched in the theme only once
    private readonly Dictionary<DeviceType, Geometry?> _iconCache = [];

    // ----- What the control remembers between events -----
    private NetworkCanvasViewModel? _subscribedViewModel;   // the view model whose RedrawRequested we listen to
    private TopLevel? _topLevel;                            // the window (null when not on screen)
    private Point _pointerPosition;                         // the last mouse position, in SCREEN units, from the canvas's top-left
    private bool _isPointerInside;                          // is the mouse over the canvas

    // Purpose:       Tells Avalonia which properties change the drawing, and that the canvas can take the keyboard.
    // Precondition:  none (a static constructor runs once, before the first control is built).
    // Postcondition: changing a color redraws the canvas by itself; the canvas can get the keyboard focus.
    static NetworkCanvasControl()
    {
        // "AffectsRender" = when one of these changes, call Render() again
        AffectsRender<NetworkCanvasControl>(
            CanvasBackgroundProperty, GridBrushProperty, ItemFillBrushProperty, ItemBorderBrushProperty,
            ItemTextBrushProperty, AccentBrushProperty, HoverBrushProperty, ConnectionBrushProperty);

        // The canvas can receive the keyboard focus (Delete, Ctrl+A, Space, Escape)
        FocusableProperty.OverrideDefaultValue<NetworkCanvasControl>(true);
    }

    // The C# side of each StyledProperty: "get" reads it, "set" writes it
    public NetworkCanvasViewModel? ViewModel { get => GetValue(ViewModelProperty); set => SetValue(ViewModelProperty, value); }
    public IBrush? CanvasBackground { get => GetValue(CanvasBackgroundProperty); set => SetValue(CanvasBackgroundProperty, value); }
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush? ItemFillBrush { get => GetValue(ItemFillBrushProperty); set => SetValue(ItemFillBrushProperty, value); }
    public IBrush? ItemBorderBrush { get => GetValue(ItemBorderBrushProperty); set => SetValue(ItemBorderBrushProperty, value); }
    public IBrush? ItemTextBrush { get => GetValue(ItemTextBrushProperty); set => SetValue(ItemTextBrushProperty, value); }
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public IBrush? HoverBrush { get => GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }
    public IBrush? ConnectionBrush { get => GetValue(ConnectionBrushProperty); set => SetValue(ConnectionBrushProperty, value); }

    // ----- Joining the view model, and the canvas size -----

    // Purpose:       Runs when an Avalonia property of this control changes: re-joins a new view model, or
    //                reports a new size.
    // Precondition:  change describes which property changed (Avalonia passes it in).
    // Postcondition: we listen to the current view model only; the view model knows the canvas size.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        // Let Avalonia do its own work first
        base.OnPropertyChanged(change);

        // The canvas got a new size (the window was resized) - tell the view model ("Fit to View" needs it)
        if (change.Property == BoundsProperty)
        {
            ViewModel?.SetViewportSize(Bounds.Width, Bounds.Height);
            return;   // stop here
        }

        // Only the ViewModel property matters from here on
        if (change.Property != ViewModelProperty)
        {
            return;   // stop here
        }

        // Stop listening to the old view model, if there was one
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.RedrawRequested -= OnRedrawRequested;
        }

        // Listen to the new one, and tell it our size
        _subscribedViewModel = ViewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.RedrawRequested += OnRedrawRequested;
            _subscribedViewModel.SetViewportSize(Bounds.Width, Bounds.Height);
        }

        InvalidateVisual();   // draw with the new view model
    }

    // Purpose:       The view model asks for a redraw.
    // Precondition:  none (called by RedrawRequested).
    // Postcondition: the cursor fits what is happening, and Avalonia will call Render() again soon.
    private void OnRedrawRequested(object? sender, EventArgs e)
    {
        UpdateCursor();       // e.g. a hand while panning
        InvalidateVisual();   // "the picture is out of date"
    }

    // ----- Listening to the whole window (step 3: dragging from the Device Library, and Escape) -----

    // Purpose:       Runs when the canvas appears on the screen - starts listening to the WHOLE window.
    // Precondition:  e describes where the control was attached (Avalonia passes it in).
    // Postcondition: OnWindowPointerMoved / OnWindowPointerReleased / OnWindowKeyDown run for the whole window.
    // Why: a pressed Device Library card "captures" the mouse, so moves and the release go to the CARD -
    // they still travel up (bubble) through the window, which is where the canvas can see them.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);              // Avalonia's own work first
        _topLevel = TopLevel.GetTopLevel(this);      // the window this control is inside

        // Not inside a window (should not happen) - nothing to listen to
        if (_topLevel is null)
        {
            return;   // stop here
        }

        // Bubble + handledEventsToo = hear moves / releases on their way up, even if another control handled them
        _topLevel.AddHandler(PointerMovedEvent, OnWindowPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        _topLevel.AddHandler(PointerReleasedEvent, OnWindowPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);

        // Tunnel = hear keys on their way down, before any other control (Escape cancels placing from anywhere)
        _topLevel.AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    // Purpose:       Runs when the canvas leaves the screen - stops listening to the window.
    // Precondition:  e describes where the control was detached from (Avalonia passes it in).
    // Postcondition: the window no longer calls this control.
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);   // Avalonia's own work first

        // Only if we were listening
        if (_topLevel is not null)
        {
            _topLevel.RemoveHandler(PointerMovedEvent, OnWindowPointerMoved);         // stop: mouse moves
            _topLevel.RemoveHandler(PointerReleasedEvent, OnWindowPointerReleased);   // stop: mouse releases
            _topLevel.RemoveHandler(KeyDownEvent, OnWindowKeyDown);                   // stop: key presses
            _topLevel = null;                                                         // no window any more
        }
    }

    // Purpose:       Every mouse move in the window - keeps the mouse position for the ghost.
    // Precondition:  e is the mouse event.
    // Postcondition: _pointerPosition / _isPointerInside are up to date; redrawn while a ghost is shown.
    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        _pointerPosition = e.GetPosition(this);                               // from the canvas's top-left
        _isPointerInside = new Rect(Bounds.Size).Contains(_pointerPosition);  // inside the canvas?

        // The ghost follows the mouse only while a device is waiting to be placed
        if (ViewModel?.IsPlacingDevice == true)
        {
            InvalidateVisual();
        }
    }

    // Purpose:       Every mouse release in the window - finishes a drag from the Device Library.
    // Precondition:  e is the mouse event.
    // Postcondition: the left button let go over the canvas while a device was waiting -> the device was placed there.
    private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Only the left button, and only while a device is waiting
        if (e.InitialPressMouseButton != MouseButton.Left || ViewModel is not { IsPlacingDevice: true } viewModel)
        {
            return;   // stop here
        }

        // Where the button was let go (screen units, from the canvas's top-left)
        var position = e.GetPosition(this);

        // Let go outside the canvas (e.g. a plain click on a card) - not a drop
        if (!new Rect(Bounds.Size).Contains(position))
        {
            return;   // stop here - the device keeps waiting
        }

        // A drop: place the device at the WORLD point under the mouse
        viewModel.ConfirmPlacement(ToWorld(viewModel, position));
    }

    // Purpose:       Every key press in the window - Escape cancels "place a device" from anywhere.
    // Precondition:  e is the key event.
    // Postcondition: Escape while a device was waiting -> nothing is waiting, the key is used up.
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Only Escape, and only while placing
        if (e.Key != Key.Escape || ViewModel is not { IsPlacingDevice: true } viewModel)
        {
            return;   // stop here
        }

        viewModel.CancelPlacement();   // stop waiting
        e.Handled = true;              // no other control reacts to this Escape
    }

    // ----- Mouse on the canvas itself -----

    // Purpose:       A mouse button pressed ON the canvas: place, connect, or start a select / drag / pan.
    // Precondition:  e is the mouse event.
    // Postcondition: the canvas has the keyboard focus, and the press went to the right place:
    //                placing -> ConfirmPlacement; Connect tool -> ConnectionTool.OnClick; otherwise -> Interaction.
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);   // Avalonia's own work first
        Focus();                    // keys (Delete, Space...) now come to the canvas

        // No view model - nothing to do
        if (ViewModel is not { } viewModel)
        {
            return;   // stop here
        }

        var point = e.GetCurrentPoint(this);                  // where, and which buttons are down
        var button = ToCanvasButton(point.Properties);        // our own button kind (null = another button)
        if (button is null)
        {
            return;   // stop here - a button we don't use
        }

        var screen = ToCanvasPoint(point.Position);           // the press, in screen units
        var world = viewModel.Viewport.ScreenToWorld(screen); // the same place in the world

        // Step 3: a device is waiting - a left click places it
        if (viewModel.IsPlacingDevice && button == CanvasPointerButton.Left)
        {
            viewModel.ConfirmPlacement(world);
            e.Handled = true;   // the click is used up
            return;             // stop here
        }

        // Step 6: the Connect tool - a left click picks a device (unless Space is held: then it scrolls)
        if (viewModel.IsConnectToolActive && button == CanvasPointerButton.Left && !viewModel.Interaction.IsSpaceHeld)
        {
            viewModel.ConnectionTool.OnClick(world);
            e.Handled = true;
            return;
        }

        // Steps 4-5: select / drag / rectangle / pan. "Capture" = keep getting this mouse's moves and its
        // release even if it leaves the canvas, so a drag never gets "stuck"
        e.Pointer.Capture(this);
        viewModel.Interaction.OnPointerPressed(screen, button.Value, IsCtrlHeld(e.KeyModifiers));
        e.Handled = true;
    }

    // Purpose:       The mouse moved over the canvas (or while captured by it).
    // Precondition:  e is the mouse event.
    // Postcondition: the select / drag / pan logic got the move; the Connect tool got it too when it is on.
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);   // Avalonia's own work first

        // No view model - nothing to do
        if (ViewModel is not { } viewModel)
        {
            return;   // stop here
        }

        var screen = ToCanvasPoint(e.GetPosition(this));   // the mouse, in screen units
        viewModel.Interaction.OnPointerMoved(screen);      // hover, drag, rectangle, pan, coordinates

        // The Connect tool's highlight and rubber band follow the mouse too
        if (viewModel.IsConnectToolActive)
        {
            viewModel.ConnectionTool.OnPointerMoved(viewModel.Viewport.ScreenToWorld(screen));
        }
    }

    // Purpose:       A mouse button let go over the canvas (or while captured by it).
    // Precondition:  e is the mouse event.
    // Postcondition: the drag / rectangle / pan ended, and the mouse is no longer captured.
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);   // Avalonia's own work first

        // Which button was let go (null = one we don't use)
        if (ViewModel is { } viewModel && ToCanvasButton(e.InitialPressMouseButton) is { } button)
        {
            viewModel.Interaction.OnPointerReleased(ToCanvasPoint(e.GetPosition(this)), button);
        }

        e.Pointer.Capture(null);   // let go of the mouse
    }

    // Purpose:       The mouse left the canvas - no hover, no ghost.
    // Precondition:  e is the mouse event.
    // Postcondition: nothing is hovered; the canvas is redrawn.
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);                    // Avalonia's own work first
        _isPointerInside = false;                   // the mouse is not over the canvas
        ViewModel?.Interaction.OnPointerExited();   // forget what was hovered
        InvalidateVisual();                         // draw again, without the ghost
    }

    // Purpose:       The mouse wheel over the canvas - zoom around the mouse (step 5).
    // Precondition:  e is the wheel event (Delta.Y > 0 = wheel up).
    // Postcondition: the camera zoomed; the wheel is used up (the page does not scroll).
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);   // Avalonia's own work first

        if (ViewModel is { } viewModel)
        {
            viewModel.Interaction.OnWheel(ToCanvasPoint(e.GetPosition(this)), e.Delta.Y);   // zoom
            e.Handled = true;                                                                // used up
        }
    }

    // ----- Keys on the canvas (it must have the focus - it takes it on every click) -----

    // Purpose:       A key pressed while the canvas has the focus.
    // Precondition:  e is the key event.
    // Postcondition: Space -> "Space held" (drag scrolls); Escape -> cancel the cable / leave the Connect tool /
    //                cancel the drag or un-select; Ctrl+A -> select all; Delete / Backspace -> delete the selection.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);   // Avalonia's own work first

        // No view model - nothing to do
        if (ViewModel is not { } viewModel)
        {
            return;   // stop here
        }

        // "switch" = pick the case that matches the key
        switch (e.Key)
        {
            case Key.Space:
                viewModel.Interaction.SetSpaceHeld(true);   // hold Space + drag = scroll
                e.Handled = true;
                break;

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
                    viewModel.Interaction.OnEscape();                // undo a drag / rectangle, or un-select
                }

                e.Handled = true;
                break;

            // "when" = this case only if the extra condition is true too
            case Key.A when IsCtrlHeld(e.KeyModifiers):
                viewModel.SelectAllCommand.Execute(null);   // Ctrl + A
                e.Handled = true;
                break;

            case Key.Delete:
            case Key.Back:
                // Delete only when something is selected (CanExecute says so)
                if (viewModel.DeleteSelectionCommand.CanExecute(null))
                {
                    viewModel.DeleteSelectionCommand.Execute(null);
                }

                e.Handled = true;
                break;
        }
    }

    // Purpose:       A key let go while the canvas has the focus.
    // Precondition:  e is the key event.
    // Postcondition: letting go of Space ends "Space held".
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);   // Avalonia's own work first

        if (e.Key == Key.Space)
        {
            ViewModel?.Interaction.SetSpaceHeld(false);   // Space is up again
            e.Handled = true;
        }
    }

    // ----- Drawing -----

    // Purpose:       Draws the whole canvas, in layers: floor, grid, (world:) cables, rubber band, devices,
    //                selection rectangle, ghost.
    // Precondition:  context is where to draw (Avalonia passes it in when the picture is out of date).
    // Postcondition: the canvas on the screen shows the current state.
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);   // the whole canvas, from (0, 0)

        // The floor
        context.FillRectangle(CanvasBackground ?? FallbackBackground, bounds);

        // No view model yet - only the floor
        if (ViewModel is not { } viewModel)
        {
            return;   // stop here
        }

        // The grid is drawn in screen units (so its lines stay 1 unit thin at every zoom)
        if (viewModel.IsGridVisible)
        {
            DrawGrid(context, bounds, viewModel);
        }

        // Everything else lives in the world. The transform: first move by -Origin, then grow by Zoom -
        // exactly CanvasViewport.WorldToScreen, but done once for the whole picture
        var viewport = viewModel.Viewport;
        var worldToScreen = Matrix.CreateTranslation(-viewport.Origin.X, -viewport.Origin.Y) * Matrix.CreateScale(viewport.Zoom, viewport.Zoom);

        // "using" = until the end of this block, everything is drawn through the transform
        using (context.PushTransform(worldToScreen))
        {
            DrawConnections(context, viewModel);      // cables first, so devices are drawn on top of them
            DrawRubberBand(context, viewModel);       // the cable being made (Connect tool)
            foreach (var item in viewModel.Items)     // every device, the last one on top
            {
                DrawItem(context, viewModel, item);
            }
            DrawSelectionRectangle(context, viewModel);   // the rectangle being drawn
            DrawPlacementGhost(context, viewModel);       // the device being placed, on top of everything
        }
    }

    // Purpose:       Draws the grid lines that are inside the canvas.
    // Precondition:  context, the canvas area and the view model.
    // Postcondition: vertical and horizontal lines every GridSpacing world units are drawn (skipped when
    //                zoomed out so far that the lines would be under 4 units apart - that would be a smear).
    private void DrawGrid(DrawingContext context, Rect bounds, NetworkCanvasViewModel viewModel)
    {
        var viewport = viewModel.Viewport;                       // the camera
        var spacing = viewModel.GridSpacing;                     // world units between lines

        // Too dense to be useful - don't draw it
        if (spacing * viewport.Zoom < 4)
        {
            return;   // stop here
        }

        var pen = new Pen(GridBrush ?? FallbackGrid, 1);         // thin grid lines

        // The world area that is visible: the world under the top-left and under the bottom-right corners
        var topLeft = viewport.ScreenToWorld(new CanvasPoint(0, 0));
        var bottomRight = viewport.ScreenToWorld(new CanvasPoint(bounds.Width, bounds.Height));

        // Vertical lines: from the first grid line left of the visible area, every "spacing", to the right edge
        for (var worldX = Math.Floor(topLeft.X / spacing) * spacing; worldX <= bottomRight.X; worldX += spacing)
        {
            var screenX = viewport.WorldToScreen(new CanvasPoint(worldX, 0)).X;              // where it is on the screen
            context.DrawLine(pen, new Point(screenX, 0), new Point(screenX, bounds.Height));  // top to bottom
        }

        // Horizontal lines: the same, top to bottom
        for (var worldY = Math.Floor(topLeft.Y / spacing) * spacing; worldY <= bottomRight.Y; worldY += spacing)
        {
            var screenY = viewport.WorldToScreen(new CanvasPoint(0, worldY)).Y;
            context.DrawLine(pen, new Point(0, screenY), new Point(bounds.Width, screenY));   // left to right
        }
    }

    // Purpose:       Draws every cable as a line from device middle to device middle (step 6).
    // Precondition:  inside the world transform.
    // Postcondition: each cable is drawn - thicker and in the accent color when selected, lighter when hovered.
    private void DrawConnections(DrawingContext context, NetworkCanvasViewModel viewModel)
    {
        var hoveredId = viewModel.Interaction.HoveredConnectionId;   // the cable under the mouse (or null)

        foreach (var cable in viewModel.Connections)
        {
            var isSelected = viewModel.IsSelected(cable.Id);   // is this cable selected

            // The color: selected -> accent, hovered -> hover, otherwise the normal cable color
            var brush = isSelected ? AccentBrush ?? FallbackAccent
                : hoveredId == cable.Id ? HoverBrush ?? FallbackHover
                : ConnectionBrush ?? FallbackConnection;

            // A round-ended pen, 3 units when selected and 2 otherwise
            var pen = new Pen(brush, isSelected ? 3 : 2) { LineCap = PenLineCap.Round };

            context.DrawLine(pen, ToPoint(cable.A), ToPoint(cable.B));   // the line
        }
    }

    // Purpose:       Draws the "rubber band": a dashed line from the first picked device to the mouse (step 6).
    // Precondition:  inside the world transform.
    // Postcondition: drawn only while the Connect tool has a first device picked.
    private void DrawRubberBand(DrawingContext context, NetworkCanvasViewModel viewModel)
    {
        var tool = viewModel.ConnectionTool;

        // Only with a first device picked, and only while it is still on the canvas
        if (!viewModel.IsConnectToolActive || tool.PendingDeviceId is not { } pendingId)
        {
            return;   // stop here
        }

        // The picked device's item (its middle is where the band starts)
        foreach (var item in viewModel.Items)
        {
            if (item.Id == pendingId)
            {
                var pen = new Pen(AccentBrush ?? FallbackAccent, 2) { DashStyle = Dashed };    // dashed accent
                context.DrawLine(pen, ToPoint(item.Center), ToPoint(tool.PointerWorldPosition)); // middle -> mouse
                return;   // done
            }
        }
    }

    // Purpose:       Draws one device: a rounded box (border by its state), its icon and its name.
    // Precondition:  inside the world transform; item is a device on the canvas.
    // Postcondition: the device is drawn - accent border when selected or picked by the Connect tool,
    //                hover border when the mouse is over it, the normal border otherwise.
    private void DrawItem(DrawingContext context, NetworkCanvasViewModel viewModel, CanvasItem item)
    {
        var rect = new Rect(item.Position.X, item.Position.Y, item.Size.Width, item.Size.Height);   // the box, in world units

        // Its state
        var isSelected = viewModel.IsSelected(item.Id);                                       // selected (step 4)
        var isPicked = viewModel.IsConnectToolActive && viewModel.ConnectionTool.PendingDeviceId == item.Id;   // first cable end (step 6)
        var isHovered = viewModel.IsConnectToolActive
            ? viewModel.ConnectionTool.HoveredDeviceId == item.Id                             // Connect tool: its own hover
            : viewModel.Interaction.HoveredItemId == item.Id;                                 // otherwise: the normal hover

        // The border: strongest state wins
        var borderPen = isSelected || isPicked ? new Pen(AccentBrush ?? FallbackAccent, 2.5)
            : isHovered ? new Pen(HoverBrush ?? FallbackHover, 1.5)
            : new Pen(ItemBorderBrush ?? FallbackItemBorder, 1);

        // The box, with corners rounded by 8
        context.DrawRectangle(ItemFillBrush ?? FallbackItemFill, borderPen, rect, 8, 8);

        var textBrush = ItemTextBrush ?? FallbackItemText;   // the icon and name color

        // The icon, in the upper 60% of the box
        if (GetDeviceIcon(item.DeviceType) is { } icon)
        {
            DrawIconCentered(context, icon, new Rect(rect.X, rect.Y + 4, rect.Width, rect.Height * 0.6), textBrush);
        }

        // The name, centered, 4 units above the bottom of the box
        var label = new FormattedText(item.Label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, textBrush);
        context.DrawText(label, new Point(rect.Center.X - label.Width / 2, rect.Bottom - label.Height - 4));
    }

    // Purpose:       Draws the selection rectangle while the user drags on empty canvas (step 4).
    // Precondition:  inside the world transform.
    // Postcondition: a see-through box with a dashed border is drawn, or nothing when no rectangle is being drawn.
    private void DrawSelectionRectangle(DrawingContext context, NetworkCanvasViewModel viewModel)
    {
        // No rectangle right now
        if (viewModel.Interaction.SelectionRectangle is not { } rectangle)
        {
            return;   // stop here
        }

        var rect = new Rect(ToPoint(rectangle.Min), ToPoint(rectangle.Max));                          // corner to corner
        context.DrawRectangle(SeeThroughAccent, new Pen(AccentBrush ?? FallbackAccent, 1) { DashStyle = Dashed }, rect);   // fill + dashed line
    }

    // Purpose:       Draws the ghost of the device waiting to be placed, centered under the mouse (step 3).
    // Precondition:  inside the world transform.
    // Postcondition: drawn only while a device is waiting and the mouse is over the canvas.
    private void DrawPlacementGhost(DrawingContext context, NetworkCanvasViewModel viewModel)
    {
        // Nothing waiting, or the mouse is not over the canvas - no ghost
        if (!_isPointerInside || viewModel.PendingPlacementDeviceType is not { } deviceType)
        {
            return;   // stop here
        }

        // The mouse in the world, and a device-sized box centered on it
        var center = viewModel.Viewport.ScreenToWorld(ToCanvasPoint(_pointerPosition));
        var size = CanvasItem.DefaultDeviceSize;
        var rect = new Rect(center.X - size.Width / 2, center.Y - size.Height / 2, size.Width, size.Height);

        var accent = AccentBrush ?? FallbackAccent;
        context.DrawRectangle(SeeThroughAccent, new Pen(accent, 1.5) { DashStyle = Dashed }, rect, 8, 8);   // see-through dashed box

        if (GetDeviceIcon(deviceType) is { } icon)
        {
            DrawIconCentered(context, icon, rect, accent);   // the icon in the middle
        }
    }

    // ----- Small helpers -----

    // Purpose:       Picks the mouse cursor that fits what is happening.
    // Precondition:  none.
    // Postcondition: Cursor is: a moving cross while panning / dragging, a hand when a drag would scroll,
    //                a cross for placing or connecting, otherwise the arrow.
    private void UpdateCursor()
    {
        if (ViewModel is not { } viewModel)
        {
            Cursor = ArrowCursor;   // no view model - the normal arrow
            return;
        }

        var mode = viewModel.Interaction.Mode;   // what the mouse is doing
        Cursor = mode is CanvasInteractionMode.Panning or CanvasInteractionMode.DraggingItems ? MoveCursor   // moving something
            : viewModel.IsPanToolActive || viewModel.Interaction.IsSpaceHeld ? HandCursor                    // a drag would scroll
            : viewModel.IsPlacingDevice || viewModel.IsConnectToolActive ? CrossCursor                       // picking a point / a device
            : ArrowCursor;                                                                                   // normal
    }

    // Purpose:       Finds a device kind's icon in the theme (Theme/Icons.axaml).
    // Precondition:  the control is on the screen.
    // Postcondition: returns the icon shape, or null. Each kind is looked up only once.
    private Geometry? GetDeviceIcon(DeviceType deviceType)
    {
        // Already looked up - use the remembered answer
        if (_iconCache.TryGetValue(deviceType, out var cached))
        {
            return cached;
        }

        // e.g. "Icon.Router" -> search this control, its parents, then the app
        var icon = this.TryFindResource(DeviceIcons.ResourceKeyFor(deviceType), out var resource) ? resource as Geometry : null;

        _iconCache[deviceType] = icon;   // remember (even null)
        return icon;
    }

    // Purpose:       Draws an icon in the middle of an area, scaled to 55% of the area's smaller side.
    // Precondition:  icon is drawn on a 20 x 20 grid.
    // Postcondition: the icon is drawn centered in the area.
    private static void DrawIconCentered(DrawingContext context, Geometry icon, Rect area, IBrush brush)
    {
        var targetSize = Math.Min(area.Width, area.Height) * 0.55;   // how big the icon should be
        if (targetSize <= 0)
        {
            return;   // no room at all
        }

        var scale = targetSize / IconGridSize;                             // how much to grow the 20 x 20 icon
        var offsetX = area.Center.X - (IconGridSize * scale) / 2;         // its left edge, so it is centered
        var offsetY = area.Center.Y - (IconGridSize * scale) / 2;         // its top edge

        // First grow, then move into place, then fill the shape (no outline)
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            context.DrawGeometry(brush, null, icon);
        }
    }

    // Purpose:       Turns a screen position (from the canvas's top-left) into the world point under it.
    // Precondition:  viewModel is not null.
    // Postcondition: returns the world point, through the camera.
    private static CanvasPoint ToWorld(NetworkCanvasViewModel viewModel, Point screen) =>
        viewModel.Viewport.ScreenToWorld(ToCanvasPoint(screen));

    // Purpose:       Avalonia's Point -> our CanvasPoint (the Application layer does not know Avalonia).
    // Precondition:  none.
    // Postcondition: the same X and Y.
    private static CanvasPoint ToCanvasPoint(Point point) => new(point.X, point.Y);

    // Purpose:       Our CanvasPoint -> Avalonia's Point (for drawing).
    // Precondition:  none.
    // Postcondition: the same X and Y.
    private static Point ToPoint(CanvasPoint point) => new(point.X, point.Y);

    // Purpose:       Answers whether Ctrl is held (Ctrl + click adds to the selection).
    // Precondition:  none.
    // Postcondition: true when the Control key is part of the modifiers.
    private static bool IsCtrlHeld(KeyModifiers modifiers) => modifiers.HasFlag(KeyModifiers.Control);

    // Purpose:       Which button is pressed, as our own CanvasPointerButton.
    // Precondition:  properties come from the press event.
    // Postcondition: Left / Middle / Right, or null for another button.
    private static CanvasPointerButton? ToCanvasButton(PointerPointProperties properties) =>
        properties.IsLeftButtonPressed ? CanvasPointerButton.Left
        : properties.IsMiddleButtonPressed ? CanvasPointerButton.Middle
        : properties.IsRightButtonPressed ? CanvasPointerButton.Right
        : null;

    // Purpose:       Which button was let go, as our own CanvasPointerButton.
    // Precondition:  none.
    // Postcondition: Left / Middle / Right, or null for another button.
    private static CanvasPointerButton? ToCanvasButton(MouseButton button) => button switch
    {
        MouseButton.Left => CanvasPointerButton.Left,       // left
        MouseButton.Middle => CanvasPointerButton.Middle,   // the wheel button
        MouseButton.Right => CanvasPointerButton.Right,     // right
        _ => null,                                          // anything else
    };
}
