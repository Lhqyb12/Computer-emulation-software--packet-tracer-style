using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Switching;
using NetSim.Core.Topology;
using NetSim.Core.Vlans;

namespace NetSim.Core.Tests.Switching;

/// <summary>
/// Phase 26 - VLAN-aware frame delivery across whole segments, including a trunk between two
/// switches: a VLAN survives the trunk, the allowed-VLAN list filters what crosses, and two VLANs
/// share one trunk without leaking into each other.
/// </summary>
public class VlanSwitchedSegmentServiceTests
{
    private static readonly VlanId Vlan10 = new(10);
    private static readonly VlanId Vlan20 = new(20);

    private sealed class Lab
    {
        public Network Network { get; } = new("Lab");
        public SwitchedSegmentService Segment { get; }

        public Lab()
        {
            Segment = new SwitchedSegmentService(
                new EthernetTransmissionService(new PacketEngine()),
                new SwitchingEngine(new ManualSimulationClock()));
        }

        public NetworkDevice Add(DeviceType type, string name)
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

        public static NetworkInterface Nic(NetworkDevice d) => d.Interfaces.Single();

        public static NetworkInterface Port(NetworkDevice sw, int n) => sw.Interfaces.First(p => p.Name == $"FastEthernet0/{n}");

        public EthernetFrame Frame(NetworkDevice from, MacAddress dst) =>
            EthernetFrame.Create(Nic(from).MacAddress!.Value, dst, EtherType.IPv4, RawPayload.OfSize(64));

        public SegmentDeliveryResult Send(NetworkDevice from, MacAddress dst) =>
            Segment.TransmitAcrossSegment(Network, Nic(from), Frame(from, dst));
    }

    // -------- Single switch, multiple VLANs (brief section 27) --------------------------------

    private sealed record ThreePcLab(Lab Lab, NetworkDevice Pc1, NetworkDevice Pc2, NetworkDevice Pc3);

    private static ThreePcLab BuildThreePcLab()
    {
        var lab = new Lab();
        var sw = (Switch)lab.Add(DeviceType.Switch, "SW1");
        var pc1 = lab.Add(DeviceType.Pc, "PC1");
        var pc2 = lab.Add(DeviceType.Pc, "PC2");
        var pc3 = lab.Add(DeviceType.Pc, "PC3");
        lab.ConnectUp(Lab.Nic(pc1), Lab.Port(sw, 1));
        lab.ConnectUp(Lab.Nic(pc2), Lab.Port(sw, 2));
        lab.ConnectUp(Lab.Nic(pc3), Lab.Port(sw, 3));

        sw.CreateVlan(Vlan10);
        sw.CreateVlan(Vlan20);
        sw.ConfigureAccessPort(Lab.Port(sw, 1), Vlan10);
        sw.ConfigureAccessPort(Lab.Port(sw, 2), Vlan10);
        sw.ConfigureAccessPort(Lab.Port(sw, 3), Vlan20);
        return new ThreePcLab(lab, pc1, pc2, pc3);
    }

    [Fact]
    public void OneSwitch_SameVlanHostsReachEachOther()
    {
        var (lab, pc1, pc2, _) = BuildThreePcLab();

        var result = lab.Send(pc1, Lab.Nic(pc2).MacAddress!.Value);

        Assert.Single(result.Deliveries);
        Assert.Equal(Lab.Nic(pc2).Id, result.Deliveries[0].DestinationInterface.Id);
    }

    [Fact]
    public void OneSwitch_DifferentVlanHostsDoNotReachEachOther()
    {
        var (lab, pc1, _, pc3) = BuildThreePcLab();

        // Let PC3 speak first so the switch has learned it (in VLAN 20).
        lab.Send(pc3, MacAddress.Broadcast);

        var result = lab.Send(pc1, Lab.Nic(pc3).MacAddress!.Value);

        // The frame floods within VLAN 10 only; it never reaches the VLAN 20 host.
        Assert.DoesNotContain(result.Deliveries, d => d.DestinationInterface.Id == Lab.Nic(pc3).Id);
    }

    [Fact]
    public void OneSwitch_Vlan10Broadcast_DoesNotReachTheVlan20Host()
    {
        var (lab, pc1, pc2, pc3) = BuildThreePcLab();

        var result = lab.Send(pc1, MacAddress.Broadcast);

        Assert.Contains(result.Deliveries, d => d.DestinationInterface.Id == Lab.Nic(pc2).Id);
        Assert.DoesNotContain(result.Deliveries, d => d.DestinationInterface.Id == Lab.Nic(pc3).Id);
    }

    // -------- Two switches, trunk between them (brief sections 28, 29, 51) --------------------

    private sealed record FourPcLab(
        Lab Lab, Switch Sw1, Switch Sw2,
        NetworkDevice Pc1, NetworkDevice Pc2, NetworkDevice Pc3, NetworkDevice Pc4);

    private static FourPcLab BuildFourPcTrunkLab(IReadOnlyList<VlanId>? trunkAllowed = null)
    {
        var lab = new Lab();
        var sw1 = (Switch)lab.Add(DeviceType.Switch, "SW1");
        var sw2 = (Switch)lab.Add(DeviceType.Switch, "SW2");
        var pc1 = lab.Add(DeviceType.Pc, "PC1"); // SW1 VLAN 10
        var pc2 = lab.Add(DeviceType.Pc, "PC2"); // SW1 VLAN 20
        var pc3 = lab.Add(DeviceType.Pc, "PC3"); // SW2 VLAN 10
        var pc4 = lab.Add(DeviceType.Pc, "PC4"); // SW2 VLAN 20

        lab.ConnectUp(Lab.Nic(pc1), Lab.Port(sw1, 1));
        lab.ConnectUp(Lab.Nic(pc2), Lab.Port(sw1, 2));
        lab.ConnectUp(Lab.Port(sw1, 8), Lab.Port(sw2, 8)); // trunk
        lab.ConnectUp(Lab.Nic(pc3), Lab.Port(sw2, 1));
        lab.ConnectUp(Lab.Nic(pc4), Lab.Port(sw2, 2));

        foreach (var sw in new[] { sw1, sw2 })
        {
            sw.CreateVlan(Vlan10);
            sw.CreateVlan(Vlan20);
            sw.ConfigureAccessPort(Lab.Port(sw, 1), Vlan10);
            sw.ConfigureAccessPort(Lab.Port(sw, 2), Vlan20);
            sw.ConfigureTrunkPort(Lab.Port(sw, 8), nativeVlan: VlanId.Default, allowedVlans: trunkAllowed);
        }

        return new FourPcLab(lab, sw1, sw2, pc1, pc2, pc3, pc4);
    }

    [Fact]
    public void Trunk_SameVlanAcrossTwoSwitches_Communicate()
    {
        var f = BuildFourPcTrunkLab();

        var v10 = f.Lab.Send(f.Pc1, Lab.Nic(f.Pc3).MacAddress!.Value);
        var v20 = f.Lab.Send(f.Pc2, Lab.Nic(f.Pc4).MacAddress!.Value);

        Assert.Contains(v10.Deliveries, d => d.DestinationInterface.Id == Lab.Nic(f.Pc3).Id);
        Assert.Contains(v20.Deliveries, d => d.DestinationInterface.Id == Lab.Nic(f.Pc4).Id);
    }

    [Fact]
    public void Trunk_TheFrameArrivesUntaggedAtTheFarAccessHost()
    {
        var f = BuildFourPcTrunkLab();

        var result = f.Lab.Send(f.Pc1, MacAddress.Broadcast);

        var atPc3 = result.Deliveries.Single(d => d.DestinationInterface.Id == Lab.Nic(f.Pc3).Id);
        Assert.False(atPc3.Frame.IsVlanTagged);
    }

    [Fact]
    public void Trunk_DifferentVlansAcrossTwoSwitches_DoNotCommunicate()
    {
        var f = BuildFourPcTrunkLab();

        // Prime every host so the switches have learned them all in their own VLANs.
        foreach (var pc in new[] { f.Pc1, f.Pc2, f.Pc3, f.Pc4 })
        {
            f.Lab.Send(pc, MacAddress.Broadcast);
        }

        // A unicast to a host in another VLAN never reaches that host - the frame stays in the
        // sender's VLAN (flooding at most to same-VLAN hosts).
        Assert.DoesNotContain(f.Lab.Send(f.Pc1, Lab.Nic(f.Pc2).MacAddress!.Value).Deliveries,
            d => d.DestinationInterface.Id == Lab.Nic(f.Pc2).Id);
        Assert.DoesNotContain(f.Lab.Send(f.Pc1, Lab.Nic(f.Pc4).MacAddress!.Value).Deliveries,
            d => d.DestinationInterface.Id == Lab.Nic(f.Pc4).Id);
        Assert.DoesNotContain(f.Lab.Send(f.Pc2, Lab.Nic(f.Pc3).MacAddress!.Value).Deliveries,
            d => d.DestinationInterface.Id == Lab.Nic(f.Pc3).Id);
    }

    [Fact]
    public void Trunk_Vlan10BroadcastReachesOnlyVlan10Hosts_AcrossBothSwitches()
    {
        var f = BuildFourPcTrunkLab();

        var result = f.Lab.Send(f.Pc1, MacAddress.Broadcast);

        var reached = result.Deliveries.Select(d => d.DestinationInterface.Id).ToHashSet();
        Assert.Contains(Lab.Nic(f.Pc3).Id, reached); // VLAN 10 on SW2
        Assert.DoesNotContain(Lab.Nic(f.Pc2).Id, reached); // VLAN 20 on SW1
        Assert.DoesNotContain(Lab.Nic(f.Pc4).Id, reached); // VLAN 20 on SW2
    }

    [Fact]
    public void Trunk_AllowedVlanList_BlocksADisallowedVlanFromCrossing()
    {
        var f = BuildFourPcTrunkLab(trunkAllowed: [Vlan10]); // symmetric: only VLAN 10 crosses

        var v10 = f.Lab.Send(f.Pc1, MacAddress.Broadcast);
        var v20 = f.Lab.Send(f.Pc2, MacAddress.Broadcast);

        Assert.Contains(v10.Deliveries, d => d.DestinationInterface.Id == Lab.Nic(f.Pc3).Id);
        // VLAN 20 broadcast reaches nothing on SW2 - SW1's trunk does not carry VLAN 20.
        Assert.DoesNotContain(v20.Deliveries, d => d.DestinationInterface.Id == Lab.Nic(f.Pc4).Id);
    }

    [Fact]
    public void Trunk_TaggedFrameForADisallowedVlan_IsBlockedAtTheFarTrunkIngress()
    {
        // SW1's trunk allows VLAN 10 + 20; SW2's trunk allows only VLAN 10.
        var f = BuildFourPcTrunkLab();
        f.Sw2.SetTrunkAllowedVlans(Lab.Port(f.Sw2, 8), [Vlan10]);

        var v20 = f.Lab.Send(f.Pc2, MacAddress.Broadcast); // VLAN 20 broadcast from SW1

        // SW1 tags it VLAN 20 and puts it on the trunk; SW2 rejects it on ingress.
        Assert.DoesNotContain(v20.Deliveries, d => d.DestinationInterface.Id == Lab.Nic(f.Pc4).Id);
        Assert.Contains(v20.SwitchingResults, r =>
            r.Switch.Id == f.Sw2.Id && r.Decision == SwitchForwardingDecision.Blocked
            && r.DropReason == SwitchDropReasons.VlanNotAllowedOnTrunk);
    }

    [Fact]
    public void Trunk_BothVlansShareOneTrunkWithoutLeaking()
    {
        var f = BuildFourPcTrunkLab();

        // Prime learning both ways in both VLANs.
        f.Lab.Send(f.Pc3, Lab.Nic(f.Pc1).MacAddress!.Value);
        f.Lab.Send(f.Pc4, Lab.Nic(f.Pc2).MacAddress!.Value);

        var v10 = f.Lab.Send(f.Pc1, Lab.Nic(f.Pc3).MacAddress!.Value);
        var v20 = f.Lab.Send(f.Pc2, Lab.Nic(f.Pc4).MacAddress!.Value);

        Assert.Single(v10.Deliveries);
        Assert.Equal(Lab.Nic(f.Pc3).Id, v10.Deliveries[0].DestinationInterface.Id);
        Assert.Single(v20.Deliveries);
        Assert.Equal(Lab.Nic(f.Pc4).Id, v20.Deliveries[0].DestinationInterface.Id);

        // SW2 learned PC1 in VLAN 10 and PC2 in VLAN 20, both on its trunk port - never crossed.
        Assert.Equal(Lab.Port(f.Sw2, 8).Id, f.Sw2.MacAddressTable.Lookup(Vlan10, Lab.Nic(f.Pc1).MacAddress!.Value)!.Port.Id);
        Assert.Equal(Lab.Port(f.Sw2, 8).Id, f.Sw2.MacAddressTable.Lookup(Vlan20, Lab.Nic(f.Pc2).MacAddress!.Value)!.Port.Id);
        Assert.Null(f.Sw2.MacAddressTable.Lookup(Vlan20, Lab.Nic(f.Pc1).MacAddress!.Value));
    }
}
