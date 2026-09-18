using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tests.Packets;
using NetSim.Core.Topology;

namespace NetSim.Core.Tests.Ethernet;

/// <summary>
/// Phase 17 transmission foundation: one Ethernet frame across one cable, validated against the
/// topology and driven through the Phase 16 packet engine. No switching / forwarding.
/// </summary>
public class EthernetTransmissionServiceTests
{
    private sealed class Fixture
    {
        public Network Network { get; } = new("Lab");
        public PacketEngine PacketEngine { get; } = new();
        public EthernetTransmissionService Service { get; }
        public NetworkInterface PcPort { get; }
        public NetworkInterface SwitchPort { get; }
        public Connection? Link { get; }

        public Fixture(bool bringUp = true, bool connect = true)
        {
            Service = new EthernetTransmissionService(PacketEngine);

            var pc = NetworkDeviceFactory.Create(DeviceType.Pc, "PC0");
            var sw = NetworkDeviceFactory.Create(DeviceType.Switch, "Switch0");
            Network.AddDevice(pc);
            Network.AddDevice(sw);

            PcPort = pc.Interfaces.Single(i => i.Name == "Ethernet0");
            SwitchPort = sw.Interfaces.First(i => i.Name == "FastEthernet0/1");

            if (connect)
            {
                Link = Network.Connect(PcPort, SwitchPort);
                if (bringUp)
                {
                    PcPort.BringUp();
                    SwitchPort.BringUp();
                }
            }
        }

        public EthernetFrame UnicastFrame() =>
            EthernetFrame.Create(PcPort.MacAddress!.Value, SwitchPort.MacAddress!.Value, EtherType.IPv4, RawPayload.OfSize(64));

        public EthernetFrame BroadcastFrame() =>
            EthernetFrame.Create(PcPort.MacAddress!.Value, MacAddress.Broadcast, EtherType.Arp);
    }

    // ---- Happy path -------------------------------------------------------------------------

    [Fact]
    public void Transmit_ValidUnicastFrame_IsDelivered_AndDrivesATrackedPacket()
    {
        var f = new Fixture();
        var transmitted = 0;
        var delivered = 0;
        f.Service.FrameTransmitted += (_, _) => transmitted++;
        f.Service.FrameDelivered += (_, _) => delivered++;

        var result = f.Service.Transmit(f.Network, f.PcPort, f.UnicastFrame());

        Assert.True(result.IsSuccess);
        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.Same(f.PcPort, result.SourceInterface);
        Assert.Same(f.SwitchPort, result.DestinationInterface);
        Assert.Null(result.DropReason);

        Assert.NotNull(result.Packet);
        Assert.Equal("Ethernet", result.Packet!.Protocol);
        Assert.Equal(PacketState.Delivered, result.Packet.State);
        Assert.IsType<EthernetFrame>(result.Packet.Payload);
        Assert.Contains(result.Packet, f.PacketEngine.Packets);
        Assert.Contains(
            result.Packet.StateHistory.Select(t => t.To),
            s => s == PacketState.Transmitted);
        Assert.Contains(result.Packet.StateHistory.Select(t => t.To), s => s == PacketState.InTransit);

        Assert.Equal(1, transmitted);
        Assert.Equal(1, delivered);
    }

    [Fact]
    public void Transmit_BroadcastFrame_IsRecognisedAsBroadcast_AndDelivered()
    {
        var f = new Fixture();
        var frame = f.BroadcastFrame();

        Assert.True(frame.IsBroadcast);
        Assert.False(frame.IsUnicast);
        Assert.True(frame.Validate().IsValid);

        var result = f.Service.Transmit(f.Network, f.PcPort, frame);

        Assert.True(result.IsSuccess);
        Assert.Contains("broadcast", result.Detail);
    }

    [Fact]
    public void CreateFrame_RaisesFrameCreated_AndReturnsAValidFrame()
    {
        var f = new Fixture();
        EthernetFrameEventArgs? raised = null;
        f.Service.FrameCreated += (_, e) => raised = e;

        var frame = f.Service.CreateFrame(
            MacAddress.Parse("02:00:00:00:00:01"), MacAddress.Broadcast, EtherType.Arp);

        Assert.NotNull(raised);
        Assert.Same(frame, raised!.Frame);
        Assert.True(frame.Validate().IsValid);
    }

    // ---- Invalid transmission -------------------------------------------------------------------

    [Fact]
    public void Transmit_SourceInterfaceDown_IsDropped()
    {
        var f = new Fixture(bringUp: false);
        var dropped = 0;
        f.Service.FrameDropped += (_, _) => dropped++;

        var result = f.Service.Transmit(f.Network, f.PcPort, f.UnicastFrame());

        Assert.False(result.IsSuccess);
        Assert.Same(EthernetDropReasons.SourceInterfaceDown, result.DropReason);
        Assert.Null(result.Packet);
        Assert.Empty(f.PacketEngine.Packets);
        Assert.Equal(1, dropped);
    }

    [Fact]
    public void Transmit_DestinationInterfaceDown_IsDropped()
    {
        var f = new Fixture(bringUp: false);
        f.PcPort.BringUp(); // only the source end is up

        var result = f.Service.Transmit(f.Network, f.PcPort, f.UnicastFrame());

        Assert.Same(EthernetDropReasons.DestinationInterfaceDown, result.DropReason);
    }

    [Fact]
    public void Transmit_NoCable_IsDropped()
    {
        var f = new Fixture(connect: false);
        f.PcPort.BringUp();

        var result = f.Service.Transmit(f.Network, f.PcPort, f.UnicastFrame());

        Assert.Same(EthernetDropReasons.NoPhysicalConnection, result.DropReason);
    }

    [Fact]
    public void Transmit_RemovedConnection_IsDropped()
    {
        var f = new Fixture();
        f.Network.RemoveConnection(f.Link!);

        var result = f.Service.Transmit(f.Network, f.PcPort, f.UnicastFrame());

        Assert.Same(EthernetDropReasons.NoPhysicalConnection, result.DropReason);
    }

    [Fact]
    public void Transmit_RemovedDestinationDevice_IsDropped()
    {
        var f = new Fixture();
        f.Network.RemoveDevice(f.SwitchPort.Device);

        var result = f.Service.Transmit(f.Network, f.PcPort, f.UnicastFrame());

        Assert.Same(EthernetDropReasons.NoPhysicalConnection, result.DropReason);
    }

    [Fact]
    public void Transmit_SourceInterfaceNotInTopology_IsDropped()
    {
        var f = new Fixture();
        var stray = NetworkDeviceFactory.Create(DeviceType.Pc, "Stray");
        var strayPort = stray.Interfaces.Single(i => i.Name == "Ethernet0");
        strayPort.BringUp();

        var result = f.Service.Transmit(f.Network, strayPort, f.UnicastFrame());

        Assert.Same(EthernetDropReasons.SourceInterfaceNotInTopology, result.DropReason);
    }

    [Fact]
    public void Transmit_NonEthernetSourceInterface_IsDropped()
    {
        var network = new Network("Serial");
        var r0 = NetworkDeviceFactory.Create(DeviceType.Router, "R0");
        var r1 = NetworkDeviceFactory.Create(DeviceType.Router, "R1");
        network.AddDevice(r0);
        network.AddDevice(r1);
        var s0 = r0.Interfaces.Single(i => i.Name == "Serial0/0");
        var s1 = r1.Interfaces.Single(i => i.Name == "Serial0/0");
        network.Connect(s0, s1);
        s0.BringUp();
        s1.BringUp();

        var service = new EthernetTransmissionService(new PacketEngine());
        var frame = EthernetFrame.Create(MacAddress.Parse("02:00:00:00:00:01"), MacAddress.Broadcast, EtherType.IPv4);

        var result = service.Transmit(network, s0, frame);

        Assert.Same(EthernetDropReasons.NotEthernetCapable, result.DropReason);
        Assert.Null(result.Packet);
    }

    [Fact]
    public void Transmit_StructurallyInvalidFrame_IsDropped_WithoutCreatingAPacket()
    {
        var f = new Fixture();
        var badInner = new StubPayload { ValidationResult = PacketValidationResult.Invalid("broken") };
        var frame = EthernetFrame.Create(f.PcPort.MacAddress!.Value, f.SwitchPort.MacAddress!.Value, EtherType.IPv4, badInner);

        var result = f.Service.Transmit(f.Network, f.PcPort, frame);

        Assert.Same(EthernetDropReasons.InvalidFrame, result.DropReason);
        Assert.Null(result.Packet);
        Assert.Empty(f.PacketEngine.Packets);
    }

    [Fact]
    public void Transmit_NullArguments_Throw()
    {
        var f = new Fixture();

        Assert.Throws<ArgumentNullException>(() => f.Service.Transmit(null!, f.PcPort, f.UnicastFrame()));
        Assert.Throws<ArgumentNullException>(() => f.Service.Transmit(f.Network, null!, f.UnicastFrame()));
        Assert.Throws<ArgumentNullException>(() => f.Service.Transmit(f.Network, f.PcPort, null!));
    }

    [Fact]
    public void Constructor_NullEngine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new EthernetTransmissionService(null!));
    }
}
