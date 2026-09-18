using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Tests.Common.Exceptions;

public class DomainExceptionTests
{
    [Fact]
    public void Constructor_SetsMessage()
    {
        var exception = new DomainException("Invalid topology.");

        Assert.Equal("Invalid topology.", exception.Message);
    }

    [Fact]
    public void Constructor_WithInnerException_SetsInnerException()
    {
        var inner = new InvalidOperationException("root cause");

        var exception = new DomainException("Invalid topology.", inner);

        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void DomainException_IsANetworkSimulatorException()
    {
        var exception = new DomainException("Invalid topology.");

        Assert.IsAssignableFrom<NetworkSimulatorException>(exception);
    }
}
