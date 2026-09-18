using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Switching;
using NetSim.Core.Topology;

namespace NetSim.Core.Tests.Switching;

/// <summary>
/// Phase 25 - frame forwarding across a whole Layer 2 segment: the switching decision driven
/// through the real single-hop Ethernet transmission pipeline, across one or more switches, plus
/// the simulation loop-safety mechanism for a physically looped topology.
/// </summary>
public class SwitchedSegmentServiceTests
{
    private sealed class Lab
    {
        public Network Network { get; } = new("Lab");
        public SwitchingEngine Engine { get; } = new(new ManualSimulationClock());
        public SwitchedSegmentService Segment { get; }

        public Lab()
        {
            Segment = new SwitchedSegmentService(new EthernetTransmissionService(new PacketEngine()), Engine);
        }

        public NetworkDevice AddDevice(DeviceType type, string name)
        {
            var device = NetworkDeviceFactory.Create(type, name);
            Network.AddDevice(device);
            return device;
        }

        public void ConnectUp(NetworkInterface a, NetworkInterface b)
        {
            Network.Connect(a, b);
            a.BringUp();
            b.BringUp();
        }

        public static NetworkInterface Nic(NetworkDevice endDevice) => endDevice.Interfaces.Single();

        public static NetworkInterface Port(NetworkDevice sw, int n) => sw.Interfaces.First(p => p.Name == $"FastEthernet0/{n}");

        public EthernetFrame Frame(NetworkDevice from, MacAddress dst) =>
            EthernetFrame.Create(Nic(from).MacAddress!.Value, dst, EtherType.IPv4, RawPayload.OfSize(64));

        public SegmentDeliveryResult Send(NetworkDevice from, MacAddress dst) =>
            Segment.TransmitAcrossSegment(Network, Nic(from), Frame(from, dst));
    }

    [Fact]
    public void PcSwitchPc_UnknownUnicast_IsFloodedToTheOtherHostOnly()
    {
        var lab = new Lab();
        var sw = lab.AddDevice(DeviceType.Switch, "SW1");
        var pc1 = lab.AddDevice(DeviceType.Pc, "PC1");
        var pc2 = lab.AddDevice(DeviceType.Pc, "PC2");
        lab.ConnectUp(Lab.Nic(pc1), Lab.Port(sw, 1));
        lab.ConnectUp(Lab.Nic(pc2), Lab.Port(sw, 2));

        var result = lab.Send(pc1, Lab.Nic(pc2).MacAddress!.Value);

        Assert.Single(result.Deliveries);
        Assert.Equal(Lab.Nic(pc2).Id, result.Deliveries[0].DestinationInterface.Id);
        Assert.Single(result.SwitchingResults);
        Assert.Equal(SwitchForwardingDecision.UnknownUnicastFlood, result.SwitchingResults[0].Decision);
        Assert.False(result.LoopSafetyTriggered);
    }

    [Fact]
    public void PcSwitchPc_AfterTheSwitchHasLearned_ForwardsAsKnownUnicast()
    {
        var lab = new Lab();
        var sw = lab.AddDevice(DeviceType.Switch, "SW1");
        var pc1 = lab.AddDevice(DeviceType.Pc, "PC1");
        var pc2 = lab.AddDevice(DeviceType.Pc, "PC2");
        lab.ConnectUp(Lab.Nic(pc1), Lab.Port(sw, 1));
        lab.ConnectUp(Lab.Nic(pc2), Lab.Port(sw, 2));

        // PC2 speaks first so the switch learns PC2's MAC on port 2.
        lab.Send(pc2, Lab.Nic(pc1).MacAddress!.Value);

        var result = lab.Send(pc1, Lab.Nic(pc2).MacAddress!.Value);

        Assert.Equal(SwitchForwardingDecision.KnownUnicast, result.SwitchingResults[0].Decision);
        Assert.Single(result.Deliveries);
        Assert.Equal(Lab.Nic(pc2).Id, result.Deliveries[0].DestinationInterface.Id);
    }

    [Fact]
    public void PcSwitchPc_Broadcast_ReachesEveryOtherHost()
    {
        var lab = new Lab();
        var sw = lab.AddDevice(DeviceType.Switch, "SW1");
        var pc1 = lab.AddDevice(DeviceType.Pc, "PC1");
        var pc2 = lab.AddDevice(DeviceType.Pc, "PC2");
        var pc3 = lab.AddDevice(DeviceType.Pc, "PC3");
        lab.ConnectUp(Lab.Nic(pc1), Lab.Port(sw, 1));
        lab.ConnectUp(Lab.Nic(pc2), Lab.Port(sw, 2));
        lab.ConnectUp(Lab.Nic(pc3), Lab.Port(sw, 3));

        var result = lab.Send(pc1, MacAddress.Broadcast);

        Assert.Equal(2, result.Deliveries.Count);
        Assert.Contains(result.Deliveries, d => d.DestinationInterface.Id == Lab.Nic(pc2).Id);
        Assert.Contains(result.Deliveries, d => d.DestinationInterface.Id == Lab.Nic(pc3).Id);
        Assert.DoesNotContain(result.Deliveries, d => d.DestinationInterface.Id == Lab.Nic(pc1).Id);
    }

    [Fact]
    public void MultiSwitch_UnknownUnicast_TraversesBothSwitches_AndEachLearnsIndependently()
    {
        var lab = new Lab();
        var sw1 = lab.AddDevice(DeviceType.Switch, "SW1");
        var sw2 = lab.AddDevice(DeviceType.Switch, "SW2");
        var pc1 = lab.AddDevice(DeviceType.Pc, "PC1");
        var pc2 = lab.AddDevice(DeviceType.Pc, "PC2");
        lab.ConnectUp(Lab.Nic(pc1), Lab.Port(sw1, 1));
        lab.ConnectUp(Lab.Port(sw1, 2), Lab.Port(sw2, 1));
        lab.ConnectUp(Lab.Nic(pc2), Lab.Port(sw2, 2));

        var result = lab.Send(pc1, Lab.Nic(pc2).MacAddress!.Value);

        Assert.Single(result.Deliveries);
        Assert.Equal(Lab.Nic(pc2).Id, result.Deliveries[0].DestinationInterface.Id);
        Assert.Equal(2, result.SwitchingResults.Count);

        var pc1Mac = Lab.Nic(pc1).MacAddress!.Value;
        Assert.Equal(Lab.Port(sw1, 1).Id, ((Switch)sw1).MacAddressTable.Lookup(pc1Mac)!.Port.Id);
        Assert.Equal(Lab.Port(sw2, 1).Id, ((Switch)sw2).MacAddressTable.Lookup(pc1Mac)!.Port.Id);
    }

    [Fact]
    public void MultiSwitch_Broadcast_ReachesTheFarHost()
    {
        var lab = new Lab();
        var sw1 = lab.AddDevice(DeviceType.Switch, "SW1");
        var sw2 = lab.AddDevice(DeviceType.Switch, "SW2");
        var pc1 = lab.AddDevice(DeviceType.Pc, "PC1");
        var pc2 = lab.AddDevice(DeviceType.Pc, "PC2");
        lab.ConnectUp(Lab.Nic(pc1), Lab.Port(sw1, 1));
        lab.ConnectUp(Lab.Port(sw1, 2), Lab.Port(sw2, 1));
        lab.ConnectUp(Lab.Nic(pc2), Lab.Port(sw2, 2));

        var result = lab.Send(pc1, MacAddress.Broadcast);

        Assert.Single(result.Deliveries);
        Assert.Equal(Lab.Nic(pc2).Id, result.Deliveries[0].DestinationInterface.Id);
    }

    [Fact]
    public void LoopedTopology_TerminatesAndReportsLoopSafety_WithoutDuplicatingDelivery()
    {
        var lab = new Lab();
        var sw1 = lab.AddDevice(DeviceType.Switch, "SW1");
        var sw2 = lab.AddDevice(DeviceType.Switch, "SW2");
        var sw3 = lab.AddDevice(DeviceType.Switch, "SW3");
        var pc1 = lab.AddDevice(DeviceType.Pc, "PC1");
        var pc2 = lab.AddDevice(DeviceType.Pc, "PC2");

        // Triangle of switches (a physical Layer 2 loop) with a host on SW1 and a host on SW3.
        lab.ConnectUp(Lab.Port(sw1, 1), Lab.Port(sw2, 1));
        lab.ConnectUp(Lab.Port(sw2, 2), Lab.Port(sw3, 1));
        lab.ConnectUp(Lab.Port(sw3, 2), Lab.Port(sw1, 2));
        lab.ConnectUp(Lab.Nic(pc1), Lab.Port(sw1, 3));
        lab.ConnectUp(Lab.Nic(pc2), Lab.Port(sw3, 3));

        var result = lab.Send(pc1, MacAddress.Broadcast);

        Assert.True(result.LoopSafetyTriggered);
        Assert.NotNull(result.LoopSafetyReason);
        // Each switch processed the frame at most once, so PC2 is reached exactly once.
        Assert.Equal(1, result.Deliveries.Count(d => d.DestinationInterface.Id == Lab.Nic(pc2).Id));
        Assert.True(result.SwitchingResults.Count <= 3);
    }

    [Fact]
    public void NoSwitch_DirectLink_ProducesExactlyOneDelivery()
    {
        var lab = new Lab();
        var pc1 = lab.AddDevice(DeviceType.Pc, "PC1");
        var pc2 = lab.AddDevice(DeviceType.Pc, "PC2");
        lab.ConnectUp(Lab.Nic(pc1), Lab.Nic(pc2));

        var result = lab.Send(pc1, Lab.Nic(pc2).MacAddress!.Value);

        Assert.Single(result.Deliveries);
        Assert.Empty(result.SwitchingResults);
        Assert.Equal(Lab.Nic(pc2).Id, result.Deliveries[0].DestinationInterface.Id);
    }

    [Fact]
    public void FirstHopFails_WhenTheSourceInterfaceIsDown()
    {
        var lab = new Lab();
        var sw = lab.AddDevice(DeviceType.Switch, "SW1");
        var pc1 = lab.AddDevice(DeviceType.Pc, "PC1");
        lab.ConnectUp(Lab.Nic(pc1), Lab.Port(sw, 1));
        Lab.Nic(pc1).BringDown();

        var result = lab.Send(pc1, MacAddress.Broadcast);

        Assert.Empty(result.Deliveries);
        Assert.NotNull(result.FirstHopDropReason);
    }
}
