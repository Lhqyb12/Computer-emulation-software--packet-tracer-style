using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Switching;
using NetSim.Core.Topology;
using NetSim.Core.Vlans;

namespace NetSim.Core.Tests.Switching;

/// <summary>
/// Phase 26 - the switching decision made VLAN-aware: ingress VLAN classification, VLAN-scoped
/// learning / lookup / flooding, VLAN isolation, trunk tagging/untagging, allowed-VLAN filtering
/// and native-VLAN behaviour. Pure decision logic - frames are not moved here (that is
/// <see cref="VlanSwitchedSegmentServiceTests"/>).
/// </summary>
public class VlanAwareSwitchingEngineTests
{
    private static readonly VlanId Vlan10 = new(10);
    private static readonly VlanId Vlan20 = new(20);

    private sealed class Fixture
    {
        public Network Network { get; } = new("Lab");
        public Switch Switch { get; }
        public ManualSimulationClock Clock { get; } = new();
        public SwitchingEngine Engine { get; }
        public NetworkInterface P1 { get; }
        public NetworkInterface P2 { get; }
        public NetworkInterface P3 { get; }
        public NetworkInterface P4 { get; }
        public NetworkInterface Trunk { get; }
        public MacAddress M1 { get; }
        public MacAddress M2 { get; }
        public MacAddress M3 { get; }
        public MacAddress M4 { get; }
        public MacAddress MTrunk { get; }

        public Fixture()
        {
            Engine = new SwitchingEngine(Clock);
            Switch = (Switch)NetworkDeviceFactory.Create(DeviceType.Switch, "SW1");
            Network.AddDevice(Switch);
            Switch.CreateVlan(Vlan10, "Students");
            Switch.CreateVlan(Vlan20, "Servers");

            var ports = new NetworkInterface[5];
            var macs = new MacAddress[5];
            for (var i = 0; i < 5; i++)
            {
                var peer = NetworkDeviceFactory.Create(i < 4 ? DeviceType.Pc : DeviceType.Switch, $"D{i + 1}");
                Network.AddDevice(peer);
                var peerPort = i < 4 ? peer.Interfaces.Single() : peer.Interfaces.First(p => p.Name == "FastEthernet0/1");
                var swPort = Switch.Interfaces.First(p => p.Name == $"FastEthernet0/{i + 1}");
                Network.Connect(peerPort, swPort);
                peerPort.BringUp();
                swPort.BringUp();
                ports[i] = swPort;
                macs[i] = peerPort.MacAddress!.Value;
            }

            (P1, P2, P3, P4, Trunk) = (ports[0], ports[1], ports[2], ports[3], ports[4]);
            (M1, M2, M3, M4, MTrunk) = (macs[0], macs[1], macs[2], macs[3], macs[4]);

            // Port 1,2 -> VLAN 10 access; Port 3,4 -> VLAN 20 access; Port 5 -> trunk (all VLANs).
            Switch.ConfigureAccessPort(P1, Vlan10);
            Switch.ConfigureAccessPort(P2, Vlan10);
            Switch.ConfigureAccessPort(P3, Vlan20);
            Switch.ConfigureAccessPort(P4, Vlan20);
            Switch.ConfigureTrunkPort(Trunk);
        }

        public EthernetFrame Frame(MacAddress src, MacAddress dst, VlanTag? tag = null) =>
            EthernetFrame.Create(src, dst, EtherType.IPv4, RawPayload.OfSize(64), tag);

        public SwitchingResult Process(NetworkInterface ingress, MacAddress src, MacAddress dst, VlanTag? tag = null) =>
            Engine.ProcessFrame(Network, Switch, ingress, Frame(src, dst, tag));
    }

    [Fact]
    public void AccessIngress_ClassifiesTheFrameIntoThePortsAccessVlan()
    {
        var f = new Fixture();
        SwitchingEventArgs? assigned = null;
        f.Engine.FrameVlanAssigned += (_, e) => assigned = e;

        var result = f.Process(f.P1, f.M1, MacAddress.Broadcast);

        Assert.Equal(Vlan10, result.Vlan);
        Assert.Equal(Vlan10, assigned!.Vlan);
    }

    [Fact]
    public void Broadcast_IsFloodedOnlyToPortsInTheSameVlan()
    {
        var f = new Fixture();

        var result = f.Process(f.P1, f.M1, MacAddress.Broadcast);

        Assert.Equal(SwitchForwardingDecision.BroadcastFlood, result.Decision);
        Assert.Contains(f.P2, result.EgressPorts);   // VLAN 10 access
        Assert.Contains(f.Trunk, result.EgressPorts); // trunk carries VLAN 10
        Assert.DoesNotContain(f.P3, result.EgressPorts); // VLAN 20 access - isolated
        Assert.DoesNotContain(f.P4, result.EgressPorts);
    }

    [Fact]
    public void Broadcast_InVlan20_DoesNotReachVlan10()
    {
        var f = new Fixture();

        var result = f.Process(f.P3, f.M3, MacAddress.Broadcast);

        Assert.Equal([f.P4.Id, f.Trunk.Id], result.EgressPorts.Select(p => p.Id));
    }

    [Fact]
    public void UnknownUnicast_IsFloodedOnlyWithinTheVlan()
    {
        var f = new Fixture();

        var result = f.Process(f.P1, f.M1, f.M3); // M3 is a VLAN 20 host, unknown in VLAN 10

        Assert.Equal(SwitchForwardingDecision.UnknownUnicastFlood, result.Decision);
        Assert.Equal([f.P2.Id, f.Trunk.Id], result.EgressPorts.Select(p => p.Id));
    }

    [Fact]
    public void Multicast_IsFloodedOnlyWithinTheVlan()
    {
        var f = new Fixture();

        var result = f.Process(f.P1, f.M1, MacAddress.Parse("01:00:5E:00:00:0A"));

        Assert.Equal(SwitchForwardingDecision.MulticastFlood, result.Decision);
        Assert.Equal([f.P2.Id, f.Trunk.Id], result.EgressPorts.Select(p => p.Id));
        Assert.DoesNotContain(f.P3, result.EgressPorts);
    }

    [Fact]
    public void KnownUnicast_WithinTheVlan_ForwardsToTheOneLearnedPort()
    {
        var f = new Fixture();
        f.Process(f.P2, f.M2, f.M1); // learn M2 on P2 in VLAN 10

        var result = f.Process(f.P1, f.M1, f.M2);

        Assert.Equal(SwitchForwardingDecision.KnownUnicast, result.Decision);
        Assert.Equal([f.P2], result.EgressPorts);
    }

    [Fact]
    public void MacLearning_IsScopedToTheVlan()
    {
        var f = new Fixture();

        f.Process(f.P1, f.M1, MacAddress.Broadcast); // M1 learned in VLAN 10 on P1

        Assert.Same(f.P1, f.Switch.MacAddressTable.Lookup(Vlan10, f.M1)!.Port);
        Assert.Null(f.Switch.MacAddressTable.Lookup(Vlan20, f.M1));
    }

    [Fact]
    public void AVlan20MacEntry_IsNeverUsedForAVlan10Frame()
    {
        var f = new Fixture();
        // Teach the switch M2 lives on P4 in VLAN 20 (contrived - normally M2 is a VLAN 10 host).
        f.Engine.ProcessFrame(f.Network, f.Switch, f.P4, f.Frame(f.M2, f.M3));
        Assert.True(f.Switch.MacAddressTable.Contains(Vlan20, f.M2));

        // A VLAN 10 frame for M2 must NOT be forwarded to P4 - it floods within VLAN 10 instead.
        var result = f.Process(f.P1, f.M1, f.M2);

        Assert.Equal(SwitchForwardingDecision.UnknownUnicastFlood, result.Decision);
        Assert.DoesNotContain(f.P4, result.EgressPorts);
    }

    [Fact]
    public void TrunkEgress_TagsTheFrame_WhileAccessEgressLeavesItUntagged()
    {
        var f = new Fixture();

        var result = f.Process(f.P1, f.M1, MacAddress.Broadcast);

        var toAccess = result.Egress.Single(e => e.Port.Id == f.P2.Id);
        var toTrunk = result.Egress.Single(e => e.Port.Id == f.Trunk.Id);

        Assert.False(toAccess.Frame.IsVlanTagged);
        Assert.True(toTrunk.Frame.IsVlanTagged);
        Assert.Equal(Vlan10, toTrunk.Frame.VlanId);
    }

    [Fact]
    public void TrunkIngress_TaggedFrame_IsClassifiedByItsTag()
    {
        var f = new Fixture();

        var result = f.Process(f.Trunk, f.MTrunk, MacAddress.Broadcast, VlanTag.For(Vlan20));

        Assert.Equal(Vlan20, result.Vlan);
        Assert.Equal([f.P3.Id, f.P4.Id], result.EgressPorts.Select(p => p.Id)); // VLAN 20 access ports only
        Assert.Same(f.Trunk, f.Switch.MacAddressTable.Lookup(Vlan20, f.MTrunk)!.Port);
    }

    [Fact]
    public void TrunkIngress_TaggedFrameForADisallowedVlan_IsBlocked_AndNotLearned()
    {
        var f = new Fixture();
        f.Switch.SetTrunkAllowedVlans(f.Trunk, [Vlan10]); // VLAN 20 no longer allowed

        SwitchingEventArgs? blockedEvent = null;
        f.Engine.FrameBlocked += (_, e) => blockedEvent = e;

        var result = f.Process(f.Trunk, f.MTrunk, MacAddress.Broadcast, VlanTag.For(Vlan20));

        Assert.Equal(SwitchForwardingDecision.Blocked, result.Decision);
        Assert.Equal(SwitchDropReasons.VlanNotAllowedOnTrunk, result.DropReason);
        Assert.Empty(result.EgressPorts);
        Assert.False(f.Switch.MacAddressTable.Contains(Vlan20, f.MTrunk));
        Assert.NotNull(blockedEvent);
    }

    [Fact]
    public void TrunkIngress_UntaggedFrame_IsClassifiedIntoTheNativeVlan()
    {
        var f = new Fixture();
        f.Switch.ConfigureTrunkPort(f.Trunk, nativeVlan: Vlan10, allowedVlans: [Vlan10, Vlan20]);

        var result = f.Process(f.Trunk, f.MTrunk, MacAddress.Broadcast); // untagged

        Assert.Equal(Vlan10, result.Vlan);
        Assert.Contains(f.P1, result.EgressPorts);
        Assert.Contains(f.P2, result.EgressPorts);
    }

    [Fact]
    public void ATaggedFrameOnAnAccessPortForAnotherVlan_IsBlocked()
    {
        var f = new Fixture();

        var result = f.Process(f.P1, f.M1, MacAddress.Broadcast, VlanTag.For(Vlan20));

        Assert.Equal(SwitchForwardingDecision.Blocked, result.Decision);
        Assert.Equal(SwitchDropReasons.TaggedFrameOnAccessPort, result.DropReason);
    }

    [Fact]
    public void Flood_WithNoOtherPortInTheVlan_Drops_ButStillLearnsTheSource()
    {
        var f = new Fixture();
        // Make P2 and the trunk not carry VLAN 10: move P2 to VLAN 20, restrict trunk to VLAN 20.
        f.Switch.ConfigureAccessPort(f.P2, Vlan20);
        f.Switch.SetTrunkAllowedVlans(f.Trunk, [Vlan20]);

        var result = f.Process(f.P1, f.M1, MacAddress.Broadcast);

        Assert.Equal(SwitchForwardingDecision.Drop, result.Decision);
        Assert.Equal(SwitchDropReasons.NoEgressPortsInVlan, result.DropReason);
        Assert.True(f.Switch.MacAddressTable.Contains(Vlan10, f.M1));
    }

    [Fact]
    public void AFrameInAnInactiveVlan_IsBlocked()
    {
        var f = new Fixture();
        f.Switch.Vlans.GetVlan(Vlan10)!.Deactivate();

        var result = f.Process(f.P1, f.M1, MacAddress.Broadcast);

        Assert.Equal(SwitchForwardingDecision.Blocked, result.Decision);
        Assert.Equal(SwitchDropReasons.VlanInactive, result.DropReason);
    }

    [Fact]
    public void ChangingAPortsAccessVlan_FlushesItsStaleMacEntries()
    {
        var f = new Fixture();
        f.Process(f.P1, f.M1, MacAddress.Broadcast); // M1 learned in VLAN 10 on P1
        Assert.True(f.Switch.MacAddressTable.Contains(Vlan10, f.M1));

        f.Switch.SetAccessVlan(f.P1, Vlan20);

        Assert.False(f.Switch.MacAddressTable.Contains(Vlan10, f.M1));
    }
}
