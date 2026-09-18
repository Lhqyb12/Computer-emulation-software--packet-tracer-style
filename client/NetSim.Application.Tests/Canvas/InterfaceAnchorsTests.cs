using NetSim.Application.Canvas;

namespace NetSim.Application.Tests.Canvas;

public class InterfaceAnchorsTests
{
    private static readonly CanvasPoint Origin = new(100, 100);
    private static readonly CanvasSize Size = new(96, 72);

    [Fact]
    public void ForInterface_PlacesAnchorsOnTheDeviceBottomEdge()
    {
        var anchor = InterfaceAnchors.ForInterface(Origin, Size, index: 0, count: 3);

        Assert.Equal(Origin.Y + Size.Height, anchor.Y, precision: 6);
    }

    [Fact]
    public void ForInterface_DistributesAnchorsEvenlyAcrossTheWidth()
    {
        var first = InterfaceAnchors.ForInterface(Origin, Size, 0, 4);
        var second = InterfaceAnchors.ForInterface(Origin, Size, 1, 4);
        var third = InterfaceAnchors.ForInterface(Origin, Size, 2, 4);

        Assert.Equal(second.X - first.X, third.X - second.X, precision: 6);
        Assert.InRange(first.X, Origin.X, Origin.X + Size.Width);
        Assert.InRange(third.X, Origin.X, Origin.X + Size.Width);
    }

    [Fact]
    public void ForInterface_SingleInterface_IsCentredHorizontally()
    {
        var anchor = InterfaceAnchors.ForInterface(Origin, Size, 0, 1);

        Assert.Equal(Origin.X + (Size.Width / 2), anchor.X, precision: 6);
    }

    [Fact]
    public void ForInterface_MovesRigidlyWithTheDevice()
    {
        var before = InterfaceAnchors.ForInterface(Origin, Size, 1, 3);
        var after = InterfaceAnchors.ForInterface(new CanvasPoint(Origin.X + 50, Origin.Y + 20), Size, 1, 3);

        Assert.Equal(before.X + 50, after.X, precision: 6);
        Assert.Equal(before.Y + 20, after.Y, precision: 6);
    }
}
