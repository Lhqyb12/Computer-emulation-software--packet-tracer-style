using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Topology;

namespace NetSim.Core.Tests.Packets;

public class PacketMovedEventTests
{
    private static NetworkInterface Port(NetworkDevice device, string name) =>
        device.Interfaces.Single(i => i.Name == name);

    private static (Network Net, PacketEngine Engine, NetworkDevice Pc, NetworkDevice Sw) TwoNode()
    {
        var net = new Network("Lab");
        var pc = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var sw = NetworkDeviceFactory.Create(DeviceType.Switch, "Switch0");
        net.AddDevice(pc);
        net.AddDevice(sw);
        net.Connect(Port(pc, "Ethernet0"), Port(sw, "FastEthernet0/1"));
        return (net, new PacketEngine(), pc, sw);
    }

    [Fact]
    public void PacketMoved_FiresOncePerSuccessfulMove_WithTheHop()
    {
        var (net, engine, pc, sw) = TwoNode();
        var events = new List<PacketEventArgs>();
        engine.PacketMoved += (_, e) => events.Add(e);

        var packet = engine.CreatePacket(pc, sw, "P");
        Assert.Empty(events); // not raised on create

        engine.MovePacket(net, packet.Id, Port(pc, "Ethernet0"));

        var moved = Assert.Single(events);
        Assert.Same(packet, moved.Packet);
        Assert.NotNull(moved.Hop);
        Assert.Same(sw, moved.Hop!.ToDevice);
        Assert.Null(moved.Transition);
    }

    [Fact]
    public void PacketMoved_DoesNotFire_WhenAMoveIsRejected()
    {
        var (net, engine, pc, sw) = TwoNode();
        var count = 0;
        engine.PacketMoved += (_, _) => count++;
        var packet = engine.CreatePacket(pc, sw, "P");

        engine.MovePacket(net, packet.Id, sw.Interfaces.First(i => !i.IsConnected)); // rejected: not on current device

        Assert.Equal(0, count);
    }

    [Fact]
    public void PacketMoved_DoesNotFire_OnDeliverOrDrop()
    {
        var (net, engine, pc, sw) = TwoNode();
        var moved = 0;
        engine.PacketMoved += (_, _) => moved++;
        var packet = engine.CreatePacket(pc, sw, "P");
        engine.MovePacket(net, packet.Id, Port(pc, "Ethernet0"));

        engine.MarkTransmitted(packet);
        engine.MarkDelivered(packet);

        Assert.Equal(1, moved); // only the one real move
    }
}
