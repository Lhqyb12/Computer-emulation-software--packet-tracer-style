using System;                      // EventHandler, EventArgs, ArgumentNullException
using NetSim.Application.State;    // IApplicationState - tells us when another network is opened

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The class that does the work of ICanvasState.
// Change from the reference project: there it went back to its defaults when the PROJECT changed;
// here it follows the NETWORK (like CanvasItemsState in step 2), so "Save As" keeps the view too.
// "sealed" = no other class can inherit from it
public sealed class CanvasState : ICanvasState
{
    // The distance between grid lines, in world units. 40 = a neat step for the 96 x 72 devices
    public const double DefaultGridSpacing = 40;

    // Purpose:       Builds the canvas state with its defaults, and resets it whenever another network is opened.
    // Precondition:  applicationState is not null (the DI container passes it in).
    // Postcondition: default camera, grid shown, snap on, Select tool; listens to CurrentNetworkChanged.
    public CanvasState(IApplicationState applicationState)
    {
        // Stop right away with a clear message if nothing was passed in
        ArgumentNullException.ThrowIfNull(applicationState);

        // A different network = a fresh view. "(_, _)" = we don't need the event's details
        applicationState.CurrentNetworkChanged += (_, _) => ResetToDefaults();
    }

    // The values, each with its starting value. "private set" = only this class changes them
    public CanvasViewport Viewport { get; private set; } = CanvasViewport.Default;   // world (0,0) at the top-left, 100%
    public bool IsGridVisible { get; private set; } = true;                           // the grid is shown
    public double GridSpacing { get; } = DefaultGridSpacing;                          // 40 units between lines
    public bool IsSnapToGridEnabled { get; private set; } = true;                     // devices snap to it
    public CanvasTool CurrentTool { get; private set; } = CanvasTool.Select;          // the Select tool

    // The events
    public event EventHandler? ViewportChanged;        // the camera changed
    public event EventHandler? GridSettingsChanged;    // grid shown / snap changed
    public event EventHandler? CurrentToolChanged;     // another tool was picked

    // Purpose:       Replaces the camera.
    // Precondition:  viewport is not null.
    // Postcondition: Viewport equals the given camera; ViewportChanged was raised only if it is different.
    public void SetViewport(CanvasViewport viewport)
    {
        // null is not allowed - stop with a clear message
        ArgumentNullException.ThrowIfNull(viewport);

        // The same camera (a record compares Origin and Zoom) - nothing changed, no event
        if (Viewport == viewport)
        {
            return;   // stop here - nothing more to do
        }

        Viewport = viewport;                                // the new camera
        ViewportChanged?.Invoke(this, EventArgs.Empty);     // tell the listeners ("?." = only if someone listens)
    }

    // Purpose:       Scrolls the camera by a distance in world units.
    // Precondition:  none.
    // Postcondition: Origin moved by worldDelta (through SetViewport, so the event is raised if it moved).
    public void Pan(CanvasPoint worldDelta) =>
        SetViewport(Viewport.WithOrigin(new CanvasPoint(Viewport.Origin.X + worldDelta.X, Viewport.Origin.Y + worldDelta.Y)));   // old origin + the distance

    // Purpose:       Sets the zoom, kept between 25% and 400%.
    // Precondition:  none.
    // Postcondition: Viewport.Zoom is the given zoom (kept in range).
    public void SetZoom(double zoom) => SetViewport(Viewport.WithZoom(zoom));   // the same origin, a new zoom

    // Purpose:       Goes back to the starting camera.
    // Precondition:  none.
    // Postcondition: Viewport equals CanvasViewport.Default.
    public void ResetViewport() => SetViewport(CanvasViewport.Default);

    // Purpose:       Shows or hides the grid.
    // Precondition:  none.
    // Postcondition: IsGridVisible equals isVisible; GridSettingsChanged was raised if it changed.
    public void SetGridVisible(bool isVisible)
    {
        // Already like this - nothing changed, no event
        if (IsGridVisible == isVisible)
        {
            return;   // stop here
        }

        IsGridVisible = isVisible;                              // the new value
        GridSettingsChanged?.Invoke(this, EventArgs.Empty);     // tell the listeners
    }

    // Purpose:       Turns "snap to grid" on or off.
    // Precondition:  none.
    // Postcondition: IsSnapToGridEnabled equals isEnabled; GridSettingsChanged was raised if it changed.
    public void SetSnapToGridEnabled(bool isEnabled)
    {
        // Already like this - nothing changed, no event
        if (IsSnapToGridEnabled == isEnabled)
        {
            return;   // stop here
        }

        IsSnapToGridEnabled = isEnabled;                        // the new value
        GridSettingsChanged?.Invoke(this, EventArgs.Empty);     // tell the listeners
    }

    // Purpose:       Picks a tool.
    // Precondition:  none.
    // Postcondition: CurrentTool equals tool; CurrentToolChanged was raised if it changed.
    public void SetCurrentTool(CanvasTool tool)
    {
        // Already this tool - nothing changed, no event
        if (CurrentTool == tool)
        {
            return;   // stop here
        }

        CurrentTool = tool;                                     // the new tool
        CurrentToolChanged?.Invoke(this, EventArgs.Empty);      // tell the listeners
    }

    // Purpose:       Puts everything back to its starting value (when another network is opened).
    // Precondition:  none.
    // Postcondition: default camera, grid shown, snap on, Select tool (each event raised only if it changed).
    private void ResetToDefaults()
    {
        SetViewport(CanvasViewport.Default);   // the starting camera
        SetGridVisible(true);                  // grid shown
        SetSnapToGridEnabled(true);            // snap on
        SetCurrentTool(CanvasTool.Select);     // the Select tool
    }
}
