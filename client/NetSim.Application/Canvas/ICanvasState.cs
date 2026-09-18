using System;

namespace NetSim.Application.Canvas;

/// <summary>
/// Workspace state for the Network Canvas of the currently open project: viewport (pan + zoom),
/// grid visibility/spacing, and the active tool. Singleton, mirroring the existing
/// <c>IApplicationState</c>/<c>ISelectionState</c> pattern (see
/// docs/architecture/network-canvas.md) - the canvas ViewModel is free to be recreated on every
/// navigation to the workspace because the state that matters lives here, not on the ViewModel.
/// Resets to defaults whenever the current project changes (see
/// docs/architecture/network-canvas.md, "Project integration"); Phase 8 does not yet persist
/// per-project canvas state, so "belongs to the current project" means "starts fresh for it".
/// </summary>
public interface ICanvasState
{
    CanvasViewport Viewport { get; }

    event EventHandler? ViewportChanged;

    void SetViewport(CanvasViewport viewport);

    /// <summary>Moves the viewport origin by a delta expressed in world units.</summary>
    void Pan(CanvasPoint worldDelta);

    /// <summary>Sets zoom directly (clamped to <see cref="CanvasViewport.MinZoom"/>/<see cref="CanvasViewport.MaxZoom"/>), keeping the current origin.</summary>
    void SetZoom(double zoom);

    /// <summary>Resets the viewport (only) to <see cref="CanvasViewport.Default"/>.</summary>
    void ResetViewport();

    bool IsGridVisible { get; }

    /// <summary>Grid line spacing, in world units. Centralized here so zoom-dependent spacing/snap-to-grid have one source of truth.</summary>
    double GridSpacing { get; }

    /// <summary>Whether dragged canvas items snap to the grid - see <see cref="CanvasGridSnap"/>. Independent of <see cref="IsGridVisible"/>: snapping works even while the grid is hidden.</summary>
    bool IsSnapToGridEnabled { get; }

    event EventHandler? GridSettingsChanged;

    void SetGridVisible(bool isVisible);

    void SetSnapToGridEnabled(bool isEnabled);

    CanvasTool CurrentTool { get; }

    event EventHandler? CurrentToolChanged;

    void SetCurrentTool(CanvasTool tool);

    /// <summary>The size of the virtual world the canvas represents.</summary>
    CanvasWorldBounds WorldBounds { get; }
}
