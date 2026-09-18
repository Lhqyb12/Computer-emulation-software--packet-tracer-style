using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Packets;

public class PacketLocationTests
{
    [Fact]
    public void AtDevice_HasDeviceButNoInterface()
    {
        var pc = new Pc("PC0");

        var location = PacketLocation.AtDevice(pc);

        Assert.Equal(PacketLocationKind.AtDevice, location.Kind);
        Assert.Same(pc, location.Device);
        Assert.Null(location.Interface);
        Assert.True(location.IsPositioned);
        Assert.Equal("PC0", location.ToString());
    }

    [Fact]
    public void AtInterface_DerivesDeviceFromTheInterface()
    {
        var pc = new Pc("PC0");
        var iface = pc.AddInterface("Ethernet0", InterfaceType.Ethernet);

        var location = PacketLocation.AtInterface(iface);

        Assert.Equal(PacketLocationKind.AtInterface, location.Kind);
        Assert.Same(pc, location.Device);
        Assert.Same(iface, location.Interface);
        Assert.Equal("PC0/Ethernet0", location.ToString());
    }

    [Fact]
    public void TerminalAndNoneLocations_AreSingletonsWithNoDevice()
    {
        Assert.Same(PacketLocation.None, PacketLocation.None);
        Assert.Null(PacketLocation.Delivered.Device);
        Assert.Null(PacketLocation.Dropped.Device);
        Assert.False(PacketLocation.None.IsPositioned);
        Assert.False(PacketLocation.Delivered.IsPositioned);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var pc = new Pc("PC0");

        Assert.Equal(PacketLocation.AtDevice(pc), PacketLocation.AtDevice(pc));
        Assert.NotEqual(PacketLocation.AtDevice(pc), PacketLocation.AtDevice(new Pc("PC1")));
        Assert.Equal(PacketLocation.Delivered, PacketLocation.Delivered);
        Assert.NotEqual(PacketLocation.Delivered, PacketLocation.Dropped);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => PacketLocation.AtDevice(null!));
        Assert.Throws<ArgumentNullException>(() => PacketLocation.AtInterface(null!));
    }
}
