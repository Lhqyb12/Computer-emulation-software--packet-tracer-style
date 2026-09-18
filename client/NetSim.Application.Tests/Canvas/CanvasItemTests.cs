using NetSim.Application.Canvas;
using NetSim.Core.Common;

namespace NetSim.Application.Tests.Canvas;

public class CanvasItemTests
{
    private static CanvasItem CreateItem() => new(EntityId.New(), "Item", new CanvasPoint(100, 100), new CanvasSize(50, 30));

    [Theory]
    [InlineData(100, 100)] // top-left corner, inclusive
    [InlineData(150, 130)] // bottom-right corner, inclusive
    [InlineData(125, 115)] // center
    public void Contains_PointInsideBounds_ReturnsTrue(double x, double y)
    {
        var item = CreateItem();

        Assert.True(item.Contains(new CanvasPoint(x, y)));
    }

    [Theory]
    [InlineData(99, 115)]
    [InlineData(151, 115)]
    [InlineData(125, 99)]
    [InlineData(125, 131)]
    public void Contains_PointOutsideBounds_ReturnsFalse(double x, double y)
    {
        var item = CreateItem();

        Assert.False(item.Contains(new CanvasPoint(x, y)));
    }

    [Fact]
    public void MoveTo_UpdatesPosition()
    {
        var item = CreateItem();

        item.MoveTo(new CanvasPoint(-40, 60));

        Assert.Equal(new CanvasPoint(-40, 60), item.Position);
    }

    [Fact]
    public void Rename_UpdatesLabel()
    {
        var item = CreateItem();

        item.Rename("CoreRouter");

        Assert.Equal("CoreRouter", item.Label);
    }

    [Fact]
    public void Intersects_RectangleFullyContainingItem_ReturnsTrue()
    {
        var item = CreateItem();

        Assert.True(item.Intersects(new CanvasPoint(0, 0), new CanvasPoint(300, 300)));
    }

    [Fact]
    public void Intersects_RectanglePartiallyOverlappingItem_ReturnsTrue()
    {
        var item = CreateItem();

        Assert.True(item.Intersects(new CanvasPoint(120, 110), new CanvasPoint(400, 400)));
    }

    [Fact]
    public void Intersects_RectangleNotTouchingItem_ReturnsFalse()
    {
        var item = CreateItem();

        Assert.False(item.Intersects(new CanvasPoint(200, 200), new CanvasPoint(300, 300)));
    }

    [Fact]
    public void Intersects_RectangleFullyInsideItem_ReturnsTrue()
    {
        var item = CreateItem();

        Assert.True(item.Intersects(new CanvasPoint(110, 110), new CanvasPoint(120, 120)));
    }
}
