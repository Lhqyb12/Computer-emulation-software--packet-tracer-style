using System;
using NetSim.Application.State;

namespace NetSim.Application.Canvas;

public sealed class CanvasState : ICanvasState
{
    public const double DefaultGridSpacing = 40;

    public CanvasState(IApplicationState applicationState)
    {
        ArgumentNullException.ThrowIfNull(applicationState);

        // A project is a separate workspace: switching (create/open/close) starts the canvas
        // fresh rather than carrying over the previous project's pan/zoom/grid/tool.
        applicationState.CurrentProjectChanged += (_, _) => ResetToDefaults();
    }

    public CanvasViewport Viewport { get; private set; } = CanvasViewport.Default;

    public event EventHandler? ViewportChanged;

    public bool IsGridVisible { get; private set; } = true;

    public double GridSpacing { get; } = DefaultGridSpacing;

    public bool IsSnapToGridEnabled { get; private set; } = true;

    public event EventHandler? GridSettingsChanged;

    public CanvasTool CurrentTool { get; private set; } = CanvasTool.Select;

    public event EventHandler? CurrentToolChanged;

    public CanvasWorldBounds WorldBounds { get; } = CanvasWorldBounds.Default;

    public void SetViewport(CanvasViewport viewport)
    {
        if (Viewport == viewport)
        {
            return;
        }

        Viewport = viewport;
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pan(CanvasPoint worldDelta) =>
        SetViewport(Viewport.WithOrigin(new CanvasPoint(Viewport.Origin.X + worldDelta.X, Viewport.Origin.Y + worldDelta.Y)));

    public void SetZoom(double zoom) => SetViewport(Viewport.WithZoom(zoom));

    public void ResetViewport() => SetViewport(CanvasViewport.Default);

    public void SetGridVisible(bool isVisible)
    {
        if (IsGridVisible == isVisible)
        {
            return;
        }

        IsGridVisible = isVisible;
        GridSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetSnapToGridEnabled(bool isEnabled)
    {
        if (IsSnapToGridEnabled == isEnabled)
        {
            return;
        }

        IsSnapToGridEnabled = isEnabled;
        GridSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetCurrentTool(CanvasTool tool)
    {
        if (CurrentTool == tool)
        {
            return;
        }

        CurrentTool = tool;
        CurrentToolChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetToDefaults()
    {
        SetViewport(CanvasViewport.Default);
        SetGridVisible(true);
        SetSnapToGridEnabled(true);
        SetCurrentTool(CanvasTool.Select);
    }
}
