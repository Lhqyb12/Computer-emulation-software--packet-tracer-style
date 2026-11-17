using System;   // EventHandler

namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Remembers how the canvas is being LOOKED AT (step 5) - not what is on it:
//   the camera (Viewport: scroll + zoom), the grid (shown? snap to it?), and the tool picked in the toolbar.
// It only remembers; the screen changes it through the toolbar and the mouse. It goes back to its
// defaults every time another network is opened, so each project starts with a fresh view.
// An interface = a list of what the class can do, without how
public interface ICanvasState
{
    // ----- The camera -----

    // The current camera (which part of the world is seen, and the zoom)
    CanvasViewport Viewport { get; }

    // Raised every time the camera changes (scroll or zoom)
    event EventHandler? ViewportChanged;

    // Purpose:       Replaces the camera.
    // Precondition:  viewport is not null.
    // Postcondition: Viewport equals the given camera; ViewportChanged was raised only if it is different.
    void SetViewport(CanvasViewport viewport);

    // Purpose:       Scrolls the camera by a distance in world units.
    // Precondition:  none.
    // Postcondition: the camera's Origin moved by worldDelta; ViewportChanged was raised if it moved.
    void Pan(CanvasPoint worldDelta);

    // Purpose:       Sets the zoom (around the top-left corner), kept between 25% and 400%.
    // Precondition:  none.
    // Postcondition: Viewport.Zoom is the given zoom (kept in range); ViewportChanged was raised if it changed.
    void SetZoom(double zoom);

    // Purpose:       Goes back to the starting camera: world (0, 0) at the top-left corner, 100%.
    // Precondition:  none.
    // Postcondition: Viewport equals CanvasViewport.Default.
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
    // Precondition:  none.
    // Postcondition: IsGridVisible equals the given value; GridSettingsChanged was raised if it changed.
    void SetGridVisible(bool isVisible);

    // Purpose:       Turns "snap to grid" on or off.
    // Precondition:  none.
    // Postcondition: IsSnapToGridEnabled equals the given value; GridSettingsChanged was raised if it changed.
    void SetSnapToGridEnabled(bool isEnabled);

    // ----- The tool -----

    // The tool picked in the toolbar (Select / Pan / Connect)
    CanvasTool CurrentTool { get; }

    // Raised when CurrentTool changes
    event EventHandler? CurrentToolChanged;

    // Purpose:       Picks a tool.
    // Precondition:  none.
    // Postcondition: CurrentTool equals the given tool; CurrentToolChanged was raised if it changed.
    void SetCurrentTool(CanvasTool tool);
}
