using System.Text;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Packets;

public class RawPayloadTests
{
    [Fact]
    public void FromBytes_KeepsLengthAndContent()
    {
        var payload = RawPayload.FromBytes(new byte[] { 1, 2, 3, 4 });

        Assert.Equal(4, payload.Length);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, payload.Data.ToArray());
    }

    [Fact]
    public void FromText_EncodesUtf8()
    {
        var payload = RawPayload.FromText("hi");

        Assert.Equal(Encoding.UTF8.GetBytes("hi"), payload.Data.ToArray());
    }

    [Fact]
    public void OfSize_ProducesZeroFilledPayloadOfThatLength()
    {
        var payload = RawPayload.OfSize(1500);

        Assert.Equal(1500, payload.Length);
        Assert.All(payload.Data.ToArray(), b => Assert.Equal(0, b));
    }

    [Fact]
    public void OfSize_Negative_Throws()
    {
        Assert.Throws<DomainException>(() => RawPayload.OfSize(-1));
    }

    [Fact]
    public void EmptyVariants_ShareTheSingletonAndHaveZeroLength()
    {
        Assert.Same(RawPayload.Empty, RawPayload.FromBytes(ReadOnlySpan<byte>.Empty));
        Assert.Same(RawPayload.Empty, RawPayload.FromText(string.Empty));
        Assert.Same(RawPayload.Empty, RawPayload.OfSize(0));
        Assert.Equal(0, RawPayload.Empty.Length);
    }

    [Fact]
    public void IsALeaf_WithNoEncapsulatedPayload_AndAlwaysStructurallyValid()
    {
        var payload = RawPayload.OfSize(10);

        Assert.Null(payload.EncapsulatedPayload);
        Assert.Equal("Raw", payload.PayloadType);
        Assert.True(payload.Validate().IsValid);
    }
}
