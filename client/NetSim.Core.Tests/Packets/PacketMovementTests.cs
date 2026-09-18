using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Topology;

namespace NetSim.Core.Tests.Packets;

/// <summary>
/// Phase 16 movement foundation: the engine advances a packet one connection at a time, validating
/// each move against the topology. It is purely structural - no operational-state check, no
/// routing, no forwarding decision (those are Phase 17 / 25 / 27).
/// </summary>
public class PacketMovementTests
{
    private static NetworkInterface Port(NetworkDevice device, string name) =>
        device.Interfaces.Single(i => i.Name == name);

    private static NetworkInterface FreePort(NetworkDevice device) =>
        device.Interfaces.First(i => !i.IsConnected);

    // PC0.Ethernet0 -- Switch0.Fa0/1 ; Switch0.Fa0/2 -- Router0.G0/0
    private static (Network Net, PacketEngine Engine, NetworkDevice Pc, NetworkDevice Sw, NetworkDevice Rt) Linear()
    {
        var net = new Network("Lab");
        var pc = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
        var sw = NetworkDeviceFactory.Create(DeviceType.Switch, "Switch0");
        var rt = NetworkDeviceFactory.Create(DeviceType.Router, "Router0");
        net.AddDevice(pc);
        net.AddDevice(sw);
        net.AddDevice(rt);
        net.Connect(Port(pc, "Ethernet0"), Port(sw, "FastEthernet0/1"));
        net.Connect(Port(sw, "FastEthernet0/2"), Port(rt, "GigabitEthernet0/0"));
        return (net, new PacketEngine(), pc, sw, rt);
    }

    // ----- 47. Topology movement -----

    [Fact]
    public void NewPacket_StartsAtItsSourceDevice()
    {
        var (_, engine, pc, _, rt) = Linear();

        var packet = engine.CreatePacket(pc, rt, "Test");

        Assert.Equal(PacketLocationKind.AtDevice, packet.Location.Kind);
        Assert.Same(pc, packet.Location.Device);
        Assert.Empty(packet.Hops);
    }

    [Fact]
    public void Move_AlongALinearPath_UpdatesLocationAndRecordsHops()
    {
        var (net, engine, pc, sw, rt) = Linear();
        var packet = engine.CreatePacket(pc, rt, "Test");

        var first = engine.MovePacket(net, packet.Id, Port(pc, "Ethernet0"));

        Assert.True(first.IsSuccess);
        Assert.Equal(PacketMovementStatus.Moved, first.Status);
        Assert.Same(sw, packet.Location.Device);
        Assert.Equal("FastEthernet0/1", packet.Location.Interface!.Name);
        Assert.Equal(1, packet.HopCount);
        Assert.Same(pc, packet.Hops[0].FromDevice);
        Assert.Same(sw, packet.Hops[0].ToDevice);
        Assert.Equal(1, packet.Hops[0].Sequence);

        var second = engine.MovePacket(net, packet.Id, Port(sw, "FastEthernet0/2"));

        Assert.True(second.IsSuccess);
        Assert.Same(rt, packet.Location.Device);
        Assert.Equal(2, packet.HopCount);
        Assert.Equal(2, packet.Hops[1].Sequence);
    }

    [Fact]
    public void Move_ByDestinationDevice_ResolvesTheConnectingInterface()
    {
        var (net, engine, pc, sw, _) = Linear();
        var packet = engine.CreatePacket(pc, sw, "Test");

        var result = engine.MovePacket(net, packet.Id, sw);

        Assert.True(result.IsSuccess);
        Assert.Same(sw, packet.Location.Device);
        Assert.Same(net.FindConnection(Port(pc, "Ethernet0"), Port(sw, "FastEthernet0/1")), packet.Hops[0].Connection);
    }

    [Fact]
    public void Move_RunsAgainstASnapshotToo()
    {
        var (net, engine, pc, sw, rt) = Linear();
        var packet = engine.CreatePacket(pc, rt, "Test");
        ITopologyView snapshot = net.CreateSnapshot();

        var result = engine.MovePacket(snapshot, packet.Id, Port(pc, "Ethernet0"));

        Assert.True(result.IsSuccess);
        Assert.Same(sw, packet.Location.Device);
    }

    [Fact]
    public void Delivered_Location_BecomesTerminal()
    {
        var (net, engine, pc, _, rt) = Linear();
        var packet = engine.CreatePacket(pc, rt, "Test");
        engine.MovePacket(net, packet.Id, Port(pc, "Ethernet0"));

        engine.MarkTransmitted(packet);
        engine.MarkDelivered(packet);

        Assert.Equal(PacketLocationKind.Delivered, packet.Location.Kind);
    }

    // ----- 48. Invalid movement -----

    [Fact]
    public void Move_AcrossAnInterfaceNotOnTheCurrentDevice_IsRejected()
    {
        var (net, engine, pc, sw, rt) = Linear();
        var packet = engine.CreatePacket(pc, rt, "Test");

        var result = engine.MovePacket(net, packet.Id, Port(sw, "FastEthernet0/2"));

        Assert.False(result.IsSuccess);
        Assert.Equal(PacketMovementStatus.InterfaceNotOnDevice, result.Status);
        Assert.Empty(packet.Hops);
    }

    [Fact]
    public void Move_AcrossAnUnconnectedInterface_IsRejected()
    {
        var (net, engine, pc, sw, rt) = Linear();
        var packet = engine.CreatePacket(pc, rt, "Test");
        engine.MovePacket(net, packet.Id, Port(pc, "Ethernet0")); // packet now at Switch0

        var result = engine.MovePacket(net, packet.Id, FreePort(sw)); // a switch port with nothing plugged in

        Assert.False(result.IsSuccess);
        Assert.Equal(PacketMovementStatus.NoConnection, result.Status);
    }

    [Fact]
    public void Move_TowardAnUnconnectedDevice_IsRejected()
    {
        var (net, engine, pc, _, rt) = Linear();
        var lonePc = NetworkDeviceFactory.Create(DeviceType.Pc, "Lonely");
        net.AddDevice(lonePc);
        var packet = engine.CreatePacket(pc, rt, "Test");

        var result = engine.MovePacket(net, packet.Id, lonePc);

        Assert.False(result.IsSuccess);
        Assert.Equal(PacketMovementStatus.NoConnection, result.Status);
    }

    [Fact]
    public void Move_OfAnUnknownPacket_Throws()
    {
        var (net, engine, pc, _, _) = Linear();

        Assert.Throws<DomainException>(() => engine.MovePacket(net, EntityId.New(), Port(pc, "Ethernet0")));
    }

    [Fact]
    public void Move_OfADeliveredPacket_Throws()
    {
        var (net, engine, pc, _, rt) = Linear();
        var packet = engine.CreatePacket(pc, rt, "Test");
        engine.MarkTransmitted(packet);
        engine.MarkDelivered(packet);

        Assert.Throws<DomainException>(() => engine.MovePacket(net, packet.Id, Port(pc, "Ethernet0")));
    }

    [Fact]
    public void Move_OfADroppedPacket_Throws()
    {
        var (net, engine, pc, _, rt) = Linear();
        var packet = engine.CreatePacket(pc, rt, "Test");
        engine.Drop(packet, PacketDropReason.InvalidPacket);

        Assert.Throws<DomainException>(() => engine.MovePacket(net, packet.Id, Port(pc, "Ethernet0")));
    }

    [Fact]
    public void Move_AfterTheFarDeviceIsRemoved_IsRejected()
    {
        var (net, engine, pc, sw, rt) = Linear();
        var packet = engine.CreatePacket(pc, rt, "Test");
        engine.MovePacket(net, packet.Id, Port(pc, "Ethernet0")); // at Switch0

        net.RemoveDevice(rt); // tears down Switch0.Fa0/2 -- Router0.G0/0

        var result = engine.MovePacket(net, packet.Id, Port(sw, "FastEthernet0/2"));

        Assert.False(result.IsSuccess);
        // RemoveDevice detaches the connection, so the switch port now simply has no connection.
        Assert.Equal(PacketMovementStatus.NoConnection, result.Status);
    }

    [Fact]
    public void Move_AcrossAnInterfaceFromADeviceNotInThisTopology_IsRejected()
    {
        var (net, engine, pc, _, rt) = Linear();
        var strayNet = new Network("Other");
        var strayA = NetworkDeviceFactory.Create(DeviceType.Pc, "SA");
        var strayB = NetworkDeviceFactory.Create(DeviceType.Switch, "SB");
        strayNet.AddDevice(strayA);
        strayNet.AddDevice(strayB);
        strayNet.Connect(Port(strayA, "Ethernet0"), Port(strayB, "FastEthernet0/1"));

        var packet = engine.CreatePacket(strayA, rt, "Test"); // packet's current device (strayA) is NOT in net

        var result = engine.MovePacket(net, packet.Id, Port(strayA, "Ethernet0"));

        Assert.False(result.IsSuccess);
        Assert.Equal(PacketMovementStatus.DeviceNotInTopology, result.Status);
    }

    // ----- 49. Graph cycles -----

    [Fact]
    public void Traversal_DoesNotAssumeATree_APacketCanFollowACycleBackToItsStart()
    {
        // Triangle: R0 -- S0 -- R1 -- R0
        var net = new Network("Ring");
        var r0 = NetworkDeviceFactory.Create(DeviceType.Router, "R0");
        var s0 = NetworkDeviceFactory.Create(DeviceType.Switch, "S0");
        var r1 = NetworkDeviceFactory.Create(DeviceType.Router, "R1");
        net.AddDevice(r0);
        net.AddDevice(s0);
        net.AddDevice(r1);
        net.Connect(Port(r0, "GigabitEthernet0/0"), Port(s0, "FastEthernet0/1"));
        net.Connect(Port(s0, "FastEthernet0/2"), Port(r1, "GigabitEthernet0/0"));
        net.Connect(Port(r1, "GigabitEthernet0/1"), Port(r0, "GigabitEthernet0/1"));

        var engine = new PacketEngine();
        var packet = engine.CreatePacket(r0, r1, "Test");

        Assert.True(engine.MovePacket(net, packet.Id, r0.Interfaces.Single(i => i.Name == "GigabitEthernet0/0")).IsSuccess); // R0 -> S0
        Assert.True(engine.MovePacket(net, packet.Id, s0.Interfaces.Single(i => i.Name == "FastEthernet0/2")).IsSuccess);    // S0 -> R1
        Assert.True(engine.MovePacket(net, packet.Id, r1.Interfaces.Single(i => i.Name == "GigabitEthernet0/1")).IsSuccess); // R1 -> R0

        Assert.Equal(3, packet.HopCount);
        Assert.Same(r0, packet.Location.Device); // back where it started - no infinite loop, no auto-routing
    }
}
