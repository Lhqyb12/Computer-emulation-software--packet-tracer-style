using NetSim.Core.Icmp;

namespace NetSim.Core.Tests.Icmp;

public class IcmpTypeTests
{
    [Theory]
    [InlineData(0, "Echo Reply")]
    [InlineData(3, "Destination Unreachable")]
    [InlineData(8, "Echo Request")]
    [InlineData(11, "Time Exceeded")]
    public void WellKnownValues_HaveTheExpectedNameAndAreKnown(byte value, string expectedName)
    {
        var type = new IcmpType(value);

        Assert.True(type.IsKnown);
        Assert.Equal(expectedName, type.Name);
    }

    [Fact]
    public void StaticFactories_MatchTheRfc792Values()
    {
        Assert.Equal(0, IcmpType.EchoReply.Value);
        Assert.Equal(3, IcmpType.DestinationUnreachable.Value);
        Assert.Equal(8, IcmpType.EchoRequest.Value);
        Assert.Equal(11, IcmpType.TimeExceeded.Value);
    }

    [Fact]
    public void UnknownValue_IsRepresentable_ButNotKnown()
    {
        var type = new IcmpType(200);

        Assert.False(type.IsKnown);
        Assert.Equal("Unknown", type.Name);
    }

    [Fact]
    public void ValueEquality_HoldsForTheSameValue()
    {
        Assert.Equal(new IcmpType(8), IcmpType.EchoRequest);
        Assert.NotEqual(IcmpType.EchoRequest, IcmpType.EchoReply);
    }

    [Fact]
    public void ToString_IncludesNameAndValue()
    {
        Assert.Equal("Echo Request (8)", IcmpType.EchoRequest.ToString());
    }
}
