using NetSim.Core.Common.Exceptions;
using NetSim.Core.Transport;

namespace NetSim.Core.Tests.Transport;

public class PortTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(80)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(49151)]
    [InlineData(49152)]
    [InlineData(65535)]
    public void Create_ValidPortNumbers_Succeed(int value)
    {
        var port = Port.Create(value);

        Assert.Equal((ushort)value, port.Value);
    }

    [Fact]
    public void Create_NegativeValue_Throws()
    {
        Assert.Throws<DomainException>(() => Port.Create(-1));
    }

    [Fact]
    public void Create_AboveMaxValue_Throws()
    {
        Assert.Throws<DomainException>(() => Port.Create(65536));
    }

    [Fact]
    public void TryCreate_OutOfRange_ReturnsFalse()
    {
        Assert.False(Port.TryCreate(-1, out _));
        Assert.False(Port.TryCreate(70000, out _));
    }

    [Fact]
    public void TryCreate_InRange_ReturnsTrueAndThePort()
    {
        Assert.True(Port.TryCreate(8080, out var port));
        Assert.Equal(8080, port.Value);
    }

    [Theory]
    [InlineData(0, PortCategory.WellKnown)]
    [InlineData(1023, PortCategory.WellKnown)]
    [InlineData(1024, PortCategory.Registered)]
    [InlineData(49151, PortCategory.Registered)]
    [InlineData(49152, PortCategory.DynamicOrPrivate)]
    [InlineData(65535, PortCategory.DynamicOrPrivate)]
    public void Category_ClassifiesByIanaRange(int value, PortCategory expected)
    {
        var port = Port.Create(value);

        Assert.Equal(expected, port.Category);
    }

    [Fact]
    public void ImplicitConversion_FromUshort_Works()
    {
        Port port = (ushort)443;

        Assert.Equal(443, port.Value);
    }

    [Fact]
    public void Equality_SamePortNumber_AreEqual()
    {
        Assert.Equal(Port.Create(80), Port.Create(80));
        Assert.NotEqual(Port.Create(80), Port.Create(443));
    }

    [Fact]
    public void ComparisonOperators_OrderByValue()
    {
        Assert.True(Port.Create(80) < Port.Create(443));
        Assert.True(Port.Create(443) > Port.Create(80));
        Assert.True(Port.Create(80) <= Port.Create(80));
        Assert.True(Port.Create(80) >= Port.Create(80));
    }
}
