using System;                      // brings in EventHandler, EventArgs, ArgumentNullException
using NetSim.Application.State;    // brings in IApplicationState - it tells us when another network is opened

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// The class that does the work of ICanvasState.
// Change from the reference project: there it went back to its defaults when the PROJECT changed;
// here it follows the NETWORK (like CanvasItemsState in step 2), so "Save As" keeps the view too.
// "sealed" = no other class can inherit from it; ": ICanvasState" = it does everything the interface lists
public sealed class CanvasState : ICanvasState
{
    // The distance between grid lines, in world units. 40 = a neat step for the 96 x 72 devices.
    // "public const" = a fixed number everyone may read; it can never change
    public const double DefaultGridSpacing = 40;

    // Purpose:       Builds the canvas state with its defaults, and resets it whenever another network is opened.
    // Precondition:  receives applicationState - the app's "what is open" state (passed in by the DI container; not null).
    // Postcondition: returns nothing (a constructor) - default camera, grid shown, snap on, Select tool;
    //                it listens to CurrentNetworkChanged.
    public CanvasState(IApplicationState applicationState)
    {
        // If nothing was passed in (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(applicationState);

        // "+=" = start listening: every time a different network is opened, ResetToDefaults runs (a fresh view).
        // "(_, _)" = the event's two values (who raised it, details) - not needed here
        applicationState.CurrentNetworkChanged += (_, _) => ResetToDefaults();
    }

    // The values, each with its starting value after "=". "private set" = only this class changes them
    public CanvasViewport Viewport { get; private set; } = CanvasViewport.Default;   // the camera: world (0,0) at the top-left, 100%
    public bool IsGridVisible { get; private set; } = true;                           // the grid starts shown
    public double GridSpacing { get; } = DefaultGridSpacing;                          // 40 units between lines; only "get" = never changes
    public bool IsSnapToGridEnabled { get; private set; } = true;                     // devices start snapping to the grid
    public CanvasTool CurrentTool { get; private set; } = CanvasTool.Select;          // the Select tool starts picked

    // The events. "?" = may have no listeners
    public event EventHandler? ViewportChanged;        // raised when the camera changed
    public event EventHandler? GridSettingsChanged;    // raised when "grid shown" or "snap" changed
    public event EventHandler? CurrentToolChanged;     // raised when another tool was picked

    // Purpose:       Replaces the camera.
    // Precondition:  receives viewport - the new camera (not null).
    // Postcondition: returns nothing - Viewport equals the new camera; ViewportChanged was raised only if it is different.
    public void SetViewport(CanvasViewport viewport)
    {
        // If no camera was passed in (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(viewport);

        // The same camera as now? A record compares its values (Origin and Zoom), so "==" answers "nothing changed"
        if (Viewport == viewport)
        {
            return;   // stop here - no change, so no event (the canvas does not redraw for nothing)
        }

        Viewport = viewport;                                // keep the new camera
        ViewportChanged?.Invoke(this, EventArgs.Empty);     // tell the listeners ("?." = only if someone listens)
    }

    // Purpose:       Scrolls the camera by a distance in world units.
    // Precondition:  receives worldDelta - how far to move (X and Y, world units).
    // Postcondition: returns nothing - Origin moved by worldDelta (through SetViewport, so the event is raised if it moved).
    // Builds a new origin = the old origin + the distance, then a new camera with it (WithOrigin), and hands it to SetViewport
    public void Pan(CanvasPoint worldDelta) =>
        SetViewport(Viewport.WithOrigin(new CanvasPoint(Viewport.Origin.X + worldDelta.X, Viewport.Origin.Y + worldDelta.Y)));

    // Purpose:       Sets the zoom (around the top-left corner).
    // Precondition:  receives zoom - the wanted zoom (kept between 25% and 400%).
    // Postcondition: returns nothing - Viewport.Zoom is the new zoom (through SetViewport).
    public void SetZoom(double zoom) => SetViewport(Viewport.WithZoom(zoom));   // a new camera: the same origin, the new zoom

    // Purpose:       Goes back to the starting camera.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - Viewport equals CanvasViewport.Default.
    public void ResetViewport() => SetViewport(CanvasViewport.Default);

    // Purpose:       Shows or hides the grid.
    // Precondition:  receives isVisible - true = show, false = hide.
    // Postcondition: returns nothing - IsGridVisible equals isVisible; GridSettingsChanged was raised if it changed.
    public void SetGridVisible(bool isVisible)
    {
        // Already like this - nothing changes, so no event
        if (IsGridVisible == isVisible)
        {
            return;   // stop here
        }

        IsGridVisible = isVisible;                              // keep the new value
        GridSettingsChanged?.Invoke(this, EventArgs.Empty);     // tell the listeners
    }

    // Purpose:       Turns "snap to grid" on or off.
    // Precondition:  receives isEnabled - true = on, false = off.
    // Postcondition: returns nothing - IsSnapToGridEnabled equals isEnabled; GridSettingsChanged was raised if it changed.
    public void SetSnapToGridEnabled(bool isEnabled)
    {
        // Already like this - nothing changes, so no event
        if (IsSnapToGridEnabled == isEnabled)
        {
            return;   // stop here
        }

        IsSnapToGridEnabled = isEnabled;                        // keep the new value
        GridSettingsChanged?.Invoke(this, EventArgs.Empty);     // tell the listeners
    }

    // Purpose:       Picks a tool.
    // Precondition:  receives tool - the tool to pick (Select / Pan / Connect).
    // Postcondition: returns nothing - CurrentTool equals tool; CurrentToolChanged was raised if it changed.
    public void SetCurrentTool(CanvasTool tool)
    {
        // Already this tool - nothing changes, so no event
        if (CurrentTool == tool)
        {
            return;   // stop here
        }

        CurrentTool = tool;                                     // keep the new tool
        CurrentToolChanged?.Invoke(this, EventArgs.Empty);      // tell the listeners
    }

    // Purpose:       Puts everything back to its starting value (when another network is opened).
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - default camera, grid shown, snap on, Select tool (each event raised only if it changed).
    private void ResetToDefaults()
    {
        SetViewport(CanvasViewport.Default);   // the starting camera
        SetGridVisible(true);                  // grid shown
        SetSnapToGridEnabled(true);            // snap on
        SetCurrentTool(CanvasTool.Select);     // the Select tool
    }
}
