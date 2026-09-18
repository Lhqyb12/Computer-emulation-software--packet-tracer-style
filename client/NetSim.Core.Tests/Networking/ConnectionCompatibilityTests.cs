using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

public class ConnectionCompatibilityTests
{
    [Theory]
    [InlineData(InterfaceType.Ethernet, InterfaceType.Ethernet)]
    [InlineData(InterfaceType.Ethernet, InterfaceType.FastEthernet)]
    [InlineData(InterfaceType.FastEthernet, InterfaceType.GigabitEthernet)]
    [InlineData(InterfaceType.GigabitEthernet, InterfaceType.GigabitEthernet)]
    [InlineData(InterfaceType.Serial, InterfaceType.Serial)]
    [InlineData(InterfaceType.Console, InterfaceType.Console)]
    public void AreCompatible_ReturnsTrue_ForSupportedPairings(InterfaceType a, InterfaceType b)
    {
        Assert.True(ConnectionCompatibility.AreCompatible(a, b, out var reason));
        Assert.Null(reason);
    }

    [Theory]
    [InlineData(InterfaceType.GigabitEthernet, InterfaceType.Serial)]
    [InlineData(InterfaceType.Ethernet, InterfaceType.Console)]
    [InlineData(InterfaceType.Serial, InterfaceType.Console)]
    public void AreCompatible_ReturnsFalseWithReason_ForUnsupportedPairings(InterfaceType a, InterfaceType b)
    {
        Assert.False(ConnectionCompatibility.AreCompatible(a, b, out var reason));
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void InferConnectionType_SerialPair_IsSerial()
    {
        Assert.Equal(ConnectionType.Serial, ConnectionCompatibility.InferConnectionType(InterfaceType.Serial, InterfaceType.Serial));
    }

    [Fact]
    public void InferConnectionType_EthernetPair_IsCopper()
    {
        Assert.Equal(ConnectionType.Copper, ConnectionCompatibility.InferConnectionType(InterfaceType.GigabitEthernet, InterfaceType.FastEthernet));
    }

    // ----- Capability-driven compatibility -----

    [Fact]
    public void Compatibility_IsDrivenByOverlappingCapabilities()
    {
        // GigabitEthernet advertises Ethernet | Fiber; a plain Ethernet port advertises Ethernet.
        var caps = InterfaceTypeInfo.Capabilities(InterfaceType.GigabitEthernet);
        Assert.True(caps.HasFlag(InterfaceCapability.Ethernet));
        Assert.True(caps.HasFlag(InterfaceCapability.Fiber));

        Assert.True(ConnectionCompatibility.AreCompatible(InterfaceType.GigabitEthernet, InterfaceType.Ethernet));
        Assert.False(ConnectionCompatibility.AreCompatible(InterfaceType.Console, InterfaceType.Ethernet));
    }

    // ----- Interface overload: also enforces administrative state -----

    private static (NetworkInterface A, NetworkInterface B) EthernetPair()
    {
        var a = new Pc("PC0").AddInterface("Ethernet0", InterfaceType.Ethernet);
        var b = new Pc("PC1").AddInterface("Ethernet0", InterfaceType.Ethernet);
        return (a, b);
    }

    [Fact]
    public void AreCompatible_Interfaces_TrueForEnabledCompatiblePair()
    {
        var (a, b) = EthernetPair();

        Assert.True(ConnectionCompatibility.AreCompatible(a, b, out var reason));
        Assert.Null(reason);
    }

    [Fact]
    public void AreCompatible_Interfaces_FalseWhenEitherIsDisabled()
    {
        var (a, b) = EthernetPair();
        b.Disable();

        Assert.False(ConnectionCompatibility.AreCompatible(a, b, out var reason));
        Assert.Contains("administratively disabled", reason);
    }

    [Fact]
    public void AreCompatible_Interfaces_FalseForIncompatibleMedia()
    {
        var eth = new Pc("PC0").AddInterface("Ethernet0", InterfaceType.Ethernet);
        var serial = new Router("R0").AddInterface("Serial0/0", InterfaceType.Serial);

        Assert.False(ConnectionCompatibility.AreCompatible(eth, serial, out var reason));
        Assert.Contains("cannot be connected", reason);
    }
}
