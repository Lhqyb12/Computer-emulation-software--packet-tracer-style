using System;   // brings in EventHandler - the type of an event

namespace NetSim.Application.Canvas;   // the "address" of this file: the Canvas part of the Application layer

// Remembers how the canvas is being LOOKED AT (step 5) - not what is on it:
//   the camera (Viewport: scroll + zoom), the grid (shown? snap to it?), and the tool picked in the toolbar.
// It only remembers; the screen changes it through the toolbar and the mouse. It goes back to its defaults
// every time another network is opened, so each project starts with a fresh view.
// "interface" = a list of WHAT the class can do, without HOW (CanvasState does the work)
public interface ICanvasState
{
    // ----- The camera -----

    // The current camera (which part of the world is seen, and the zoom)
    CanvasViewport Viewport { get; }

    // Raised every time the camera changes (scroll or zoom)
    event EventHandler? ViewportChanged;

    // Purpose:       Replaces the camera.
    // Precondition:  receives viewport - the new camera (not null).
    // Postcondition: returns nothing - Viewport equals the new camera; ViewportChanged was raised only if it is different.
    void SetViewport(CanvasViewport viewport);

    // Purpose:       Scrolls the camera by a distance.
    // Precondition:  receives worldDelta - how far to move, in world units (X and Y).
    // Postcondition: returns nothing - the camera's Origin moved by worldDelta; ViewportChanged was raised if it moved.
    void Pan(CanvasPoint worldDelta);

    // Purpose:       Sets the zoom (around the top-left corner).
    // Precondition:  receives zoom - the wanted zoom (kept between 25% and 400%).
    // Postcondition: returns nothing - Viewport.Zoom is the new zoom; ViewportChanged was raised if it changed.
    void SetZoom(double zoom);

    // Purpose:       Goes back to the starting camera: world (0, 0) at the top-left corner, 100%.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing - Viewport equals CanvasViewport.Default.
    void ResetViewport();

    // ----- The grid -----

    // Is the grid drawn on the canvas
    bool IsGridVisible { get; }

    // The distance between two grid lines, in world units
    double GridSpacing { get; }

    // Do placed / dragged devices jump to the nearest grid crossing
    bool IsSnapToGridEnabled { get; }

    // Raised when IsGridVisible or IsSnapToGridEnabled changes
    event EventHandler? GridSettingsChanged;

    // Purpose:       Shows or hides the grid.
    // Precondition:  receives isVisible - true = show it, false = hide it.
    // Postcondition: returns nothing - IsGridVisible equals isVisible; GridSettingsChanged was raised if it changed.
    void SetGridVisible(bool isVisible);

    // Purpose:       Turns "snap to grid" on or off.
    // Precondition:  receives isEnabled - true = on, false = off.
    // Postcondition: returns nothing - IsSnapToGridEnabled equals isEnabled; GridSettingsChanged was raised if it changed.
    void SetSnapToGridEnabled(bool isEnabled);

    // ----- The tool -----

    // The tool picked in the toolbar (Select / Pan / Connect)
    CanvasTool CurrentTool { get; }

    // Raised when CurrentTool changes
    event EventHandler? CurrentToolChanged;

    // Purpose:       Picks a tool.
    // Precondition:  receives tool - the tool to pick.
    // Postcondition: returns nothing - CurrentTool equals tool; CurrentToolChanged was raised if it changed.
    void SetCurrentTool(CanvasTool tool);
}
