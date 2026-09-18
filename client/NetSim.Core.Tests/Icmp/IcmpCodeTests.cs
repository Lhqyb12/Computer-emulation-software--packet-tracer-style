using NetSim.Core.Icmp;

namespace NetSim.Core.Tests.Icmp;

public class IcmpCodeTests
{
    [Fact]
    public void Zero_IsTheEchoCode()
    {
        Assert.Equal(0, IcmpCode.Zero.Value);
    }

    [Fact]
    public void DestinationUnreachableCodes_HaveTheirRfc792Values()
    {
        Assert.Equal(0, IcmpCode.NetworkUnreachable.Value);
        Assert.Equal(1, IcmpCode.HostUnreachable.Value);
        Assert.Equal(2, IcmpCode.ProtocolUnreachable.Value);
        Assert.Equal(3, IcmpCode.PortUnreachable.Value);
    }

    [Fact]
    public void TimeExceededCode_HasItsRfc792Value()
    {
        Assert.Equal(0, IcmpCode.TimeToLiveExceededInTransit.Value);
    }

    [Theory]
    [InlineData(0, 0, "Echo Reply")]
    [InlineData(8, 0, "Echo Request")]
    [InlineData(3, 0, "Network Unreachable")]
    [InlineData(3, 1, "Host Unreachable")]
    [InlineData(3, 2, "Protocol Unreachable")]
    [InlineData(3, 3, "Port Unreachable")]
    [InlineData(11, 0, "TTL Exceeded in Transit")]
    public void NameFor_DependsOnTheMessageType(byte typeValue, byte codeValue, string expectedName)
    {
        var name = new IcmpCode(codeValue).NameFor(new IcmpType(typeValue));

        Assert.Equal(expectedName, name);
    }

    [Fact]
    public void NameFor_UnknownCombination_FallsBackToACodeNumber()
    {
        var name = new IcmpCode(99).NameFor(IcmpType.DestinationUnreachable);

        Assert.Equal("Code 99", name);
    }

    [Fact]
    public void ValueEquality_HoldsForTheSameValue()
    {
        Assert.Equal(IcmpCode.NetworkUnreachable, IcmpCode.Zero);
        Assert.NotEqual(IcmpCode.HostUnreachable, IcmpCode.PortUnreachable);
    }
}
