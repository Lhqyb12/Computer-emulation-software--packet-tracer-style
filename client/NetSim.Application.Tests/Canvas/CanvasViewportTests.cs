using NetSim.Application.Canvas;

namespace NetSim.Application.Tests.Canvas;

public class CanvasViewportTests
{
    [Fact]
    public void Default_Is100PercentZoom_AtWorldOrigin()
    {
        var viewport = CanvasViewport.Default;

        Assert.Equal(1.0, viewport.Zoom);
        Assert.Equal(0, viewport.Origin.X);
        Assert.Equal(0, viewport.Origin.Y);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(4.0)]
    public void Constructor_WithinLimits_KeepsExactValue(double zoom)
    {
        var viewport = new CanvasViewport(CanvasPoint.Zero, zoom);

        Assert.Equal(zoom, viewport.Zoom);
    }

    [Fact]
    public void Constructor_BelowMinZoom_ClampsToMinZoom()
    {
        var viewport = new CanvasViewport(CanvasPoint.Zero, 0.01);

        Assert.Equal(CanvasViewport.MinZoom, viewport.Zoom);
    }

    [Fact]
    public void Constructor_AboveMaxZoom_ClampsToMaxZoom()
    {
        var viewport = new CanvasViewport(CanvasPoint.Zero, 99);

        Assert.Equal(CanvasViewport.MaxZoom, viewport.Zoom);
    }

    [Fact]
    public void WithZoom_ClampsJustLikeTheConstructor()
    {
        var viewport = CanvasViewport.Default.WithZoom(1000);

        Assert.Equal(CanvasViewport.MaxZoom, viewport.Zoom);
    }

    [Fact]
    public void WithOrigin_ChangesOrigin_KeepsZoom()
    {
        var viewport = new CanvasViewport(CanvasPoint.Zero, 2.0).WithOrigin(new CanvasPoint(10, 20));

        Assert.Equal(new CanvasPoint(10, 20), viewport.Origin);
        Assert.Equal(2.0, viewport.Zoom);
    }

    [Fact]
    public void WorldToScreen_AtDefaultViewport_IsIdentity()
    {
        var viewport = CanvasViewport.Default;

        var screen = viewport.WorldToScreen(new CanvasPoint(50, 75));

        Assert.Equal(50, screen.X);
        Assert.Equal(75, screen.Y);
    }

    [Fact]
    public void WorldToScreen_AppliesZoomAndOrigin()
    {
        var viewport = new CanvasViewport(new CanvasPoint(100, 200), 2.0);

        var screen = viewport.WorldToScreen(new CanvasPoint(150, 250));

        // (world - origin) * zoom
        Assert.Equal(100, screen.X);
        Assert.Equal(100, screen.Y);
    }

    [Theory]
    [InlineData(0, 0, 0.1)]
    [InlineData(123.4, -56.7, 1.0)]
    [InlineData(-500, 800, 2.5)]
    [InlineData(9999, -9999, 4.0)]
    public void ScreenToWorld_IsTheInverseOfWorldToScreen(double worldX, double worldY, double zoom)
    {
        var viewport = new CanvasViewport(new CanvasPoint(42, -17), zoom);
        var world = new CanvasPoint(worldX, worldY);

        var screen = viewport.WorldToScreen(world);
        var roundTripped = viewport.ScreenToWorld(screen);

        Assert.Equal(world.X, roundTripped.X, precision: 9);
        Assert.Equal(world.Y, roundTripped.Y, precision: 9);
    }

    [Fact]
    public void RecordEquality_SameOriginAndZoom_AreEqual()
    {
        var a = new CanvasViewport(new CanvasPoint(1, 2), 1.5);
        var b = new CanvasViewport(new CanvasPoint(1, 2), 1.5);

        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData(0, 0, 1.0, 2.0)]
    [InlineData(400, 300, 1.0, 2.0)]
    [InlineData(123, 456, 0.5, 4.0)]
    [InlineData(800, 0, 2.0, 0.5)]
    public void ZoomAround_KeepsWorldPointUnderCursorFixedOnScreen(double screenX, double screenY, double startZoom, double newZoom)
    {
        var viewport = new CanvasViewport(new CanvasPoint(-50, 30), startZoom);
        var screenPoint = new CanvasPoint(screenX, screenY);
        var worldBeforeZoom = viewport.ScreenToWorld(screenPoint);

        var zoomed = viewport.ZoomAround(screenPoint, newZoom);

        Assert.Equal(newZoom, zoomed.Zoom, precision: 9);
        var worldAfterZoom = zoomed.ScreenToWorld(screenPoint);
        Assert.Equal(worldBeforeZoom.X, worldAfterZoom.X, precision: 6);
        Assert.Equal(worldBeforeZoom.Y, worldAfterZoom.Y, precision: 6);
    }

    [Fact]
    public void ZoomAround_ClampsToMaxZoom()
    {
        var viewport = CanvasViewport.Default;

        var zoomed = viewport.ZoomAround(new CanvasPoint(100, 100), 999);

        Assert.Equal(CanvasViewport.MaxZoom, zoomed.Zoom);
    }

    [Fact]
    public void ZoomAround_ClampsToMinZoom()
    {
        var viewport = CanvasViewport.Default;

        var zoomed = viewport.ZoomAround(new CanvasPoint(100, 100), 0.0001);

        Assert.Equal(CanvasViewport.MinZoom, zoomed.Zoom);
    }

    [Fact]
    public void Fit_CentersContentAndZoomsToFillTheSmallerAxis()
    {
        var fitted = CanvasViewport.Fit(new CanvasPoint(-100, -50), new CanvasPoint(100, 50), 800, 600);

        var screenOfContentCenter = fitted.WorldToScreen(CanvasPoint.Zero);
        Assert.Equal(400, screenOfContentCenter.X, precision: 6);
        Assert.Equal(300, screenOfContentCenter.Y, precision: 6);

        // Content is 200x100 world units with 10% padding on each side (240x120 total); fitting
        // an 800x600 screen is width-bound: 800/240 < 600/120.
        Assert.Equal(800.0 / 240.0, fitted.Zoom, precision: 6);
    }

    [Fact]
    public void Fit_ResultingZoom_IsClampedToViewportLimits()
    {
        // Tiny content in a huge viewport would otherwise compute a zoom far above MaxZoom.
        var fitted = CanvasViewport.Fit(new CanvasPoint(0, 0), new CanvasPoint(1, 1), 2000, 2000);

        Assert.Equal(CanvasViewport.MaxZoom, fitted.Zoom);
    }
}
