using NetSim.Application.Canvas;

namespace NetSim.Application.Tests.Canvas;

public class CanvasGridSnapTests
{
    [Theory]
    [InlineData(143, 227, 20, 140, 220)]
    [InlineData(111, 89, 20, 120, 80)]
    [InlineData(0, 0, 20, 0, 0)]
    [InlineData(19, 21, 20, 20, 20)]
    public void Snap_RoundsToNearestGridMultiple(double x, double y, double gridSpacing, double expectedX, double expectedY)
    {
        var snapped = CanvasGridSnap.Snap(new CanvasPoint(x, y), gridSpacing);

        Assert.Equal(expectedX, snapped.X);
        Assert.Equal(expectedY, snapped.Y);
    }

    [Theory]
    [InlineData(-143, -227, 20, -140, -220)]
    [InlineData(-10, -10, 20, 0, 0)]
    public void Snap_HandlesNegativeCoordinates(double x, double y, double gridSpacing, double expectedX, double expectedY)
    {
        var snapped = CanvasGridSnap.Snap(new CanvasPoint(x, y), gridSpacing);

        Assert.Equal(expectedX, snapped.X);
        Assert.Equal(expectedY, snapped.Y);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(100)]
    [InlineData(1)]
    public void Snap_DifferentGridSizes_SnapsToThatSize(double gridSpacing)
    {
        var snapped = CanvasGridSnap.Snap(new CanvasPoint(gridSpacing * 3.4, gridSpacing * 3.4), gridSpacing);

        Assert.Equal(gridSpacing * 3, snapped.X);
        Assert.Equal(gridSpacing * 3, snapped.Y);
    }

    [Fact]
    public void Snap_ZeroOrNegativeSpacing_ReturnsPointUnchanged()
    {
        var point = new CanvasPoint(143.7, -227.3);

        Assert.Equal(point, CanvasGridSnap.Snap(point, 0));
        Assert.Equal(point, CanvasGridSnap.Snap(point, -5));
    }
}
