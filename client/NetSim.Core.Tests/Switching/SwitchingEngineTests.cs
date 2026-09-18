using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Switching;
using NetSim.Core.Topology;

namespace NetSim.Core.Tests.Switching;

/// <summary>
/// Phase 25 - the core switching algorithm in isolation: MAC learning, the known-unicast /
/// unknown-unicast / broadcast / multicast decision, ingress-port exclusion, operational-state
/// and connectivity checks, MAC move, aging and port-down handling. Pure decision logic - no
/// frames are moved across cables here (that is <see cref="SwitchedSegmentServiceTests"/>).
/// </summary>
public class SwitchingEngineTests
{
    private sealed class Fixture
    {
        public Network Network { get; } = new("Lab");
        public NetworkDevice Switch { get; }
        public ManualSimulationClock Clock { get; } = new();
        public SwitchingEngine Engine { get; }
        public NetworkInterface P1 { get; }
        public NetworkInterface P2 { get; }
        public NetworkInterface P3 { get; }
        public NetworkInterface P4 { get; }
        public MacAddress M1 { get; }
        public MacAddress M2 { get; }
        public MacAddress M3 { get; }
        public MacAddress M4 { get; }

        public Fixture()
        {
            Engine = new SwitchingEngine(Clock);
            Switch = NetworkDeviceFactory.Create(DeviceType.Switch, "SW1");
            Network.AddDevice(Switch);

            var ports = new NetworkInterface[4];
            var macs = new MacAddress[4];
            for (var i = 0; i < 4; i++)
            {
                var pc = NetworkDeviceFactory.Create(DeviceType.Pc, $"PC{i + 1}");
                Network.AddDevice(pc);
                var pcPort = pc.Interfaces.Single();
                var swPort = Switch.Interfaces.First(p => p.Name == $"FastEthernet0/{i + 1}");
                Network.Connect(pcPort, swPort);
                pcPort.BringUp();
                swPort.BringUp();
                ports[i] = swPort;
                macs[i] = pcPort.MacAddress!.Value;
            }

            (P1, P2, P3, P4) = (ports[0], ports[1], ports[2], ports[3]);
            (M1, M2, M3, M4) = (macs[0], macs[1], macs[2], macs[3]);
        }

        public EthernetFrame Frame(MacAddress src, MacAddress dst) =>
            EthernetFrame.Create(src, dst, EtherType.IPv4, RawPayload.OfSize(64));

        public SwitchingResult Process(NetworkInterface ingress, MacAddress src, MacAddress dst) =>
            Engine.ProcessFrame(Network, Switch, ingress, Frame(src, dst));

        public void Disconnect(NetworkInterface port) => Network.RemoveConnection(Network.GetConnection(port)!);
    }

    [Fact]
    public void ProcessFrame_LearnsSourceMac_OnIngressPort_AndRaisesMacLearned()
    {
        var f = new Fixture();
        SwitchingEventArgs? learned = null;
        f.Engine.MacLearned += (_, e) => learned = e;

        var result = f.Process(f.P1, f.M1, f.M2);

        Assert.Equal(MacLearnOutcome.Added, result.SourceLearn.Outcome);
        var entry = ((Switch)f.Switch).MacAddressTable.Lookup(f.M1);
        Assert.NotNull(entry);
        Assert.Same(f.P1, entry!.Port);
        Assert.NotNull(learned);
        Assert.Equal(f.M1, learned!.Mac);
        Assert.Same(f.P1, learned.Port);
    }

    [Fact]
    public void ProcessFrame_UnknownUnicast_FloodsEveryEligiblePortExceptIngress()
    {
        var f = new Fixture();
        SwitchingEventArgs? flooded = null;
        f.Engine.UnknownUnicastFlooded += (_, e) => flooded = e;

        var result = f.Process(f.P1, f.M1, f.M2);

        Assert.Equal(SwitchForwardingDecision.UnknownUnicastFlood, result.Decision);
        Assert.Equal([f.P2, f.P3, f.P4], result.EgressPorts);
        Assert.DoesNotContain(f.P1, result.EgressPorts);
        Assert.NotNull(flooded);
    }

    [Fact]
    public void ProcessFrame_KnownUnicast_ForwardsOnlyToTheLearnedPort()
    {
        var f = new Fixture();
        f.Process(f.P2, f.M2, f.M1); // teach the switch that M2 is on P2

        SwitchingEventArgs? forwarded = null;
        f.Engine.FrameForwarded += (_, e) => forwarded = e;

        var result = f.Process(f.P1, f.M1, f.M2);

        Assert.Equal(SwitchForwardingDecision.KnownUnicast, result.Decision);
        Assert.Equal([f.P2], result.EgressPorts);
        Assert.NotNull(forwarded);
        Assert.Same(f.P2, forwarded!.Port);
    }

    [Fact]
    public void ProcessFrame_Broadcast_IsFlooded()
    {
        var f = new Fixture();
        var raised = false;
        f.Engine.BroadcastFlooded += (_, _) => raised = true;

        var result = f.Process(f.P1, f.M1, MacAddress.Broadcast);

        Assert.Equal(SwitchForwardingDecision.BroadcastFlood, result.Decision);
        Assert.Equal([f.P2, f.P3, f.P4], result.EgressPorts);
        Assert.True(raised);
    }

    [Fact]
    public void ProcessFrame_Multicast_IsFlooded()
    {
        var f = new Fixture();
        var raised = false;
        f.Engine.MulticastFlooded += (_, _) => raised = true;

        var result = f.Process(f.P1, f.M1, MacAddress.Parse("01:00:5E:00:00:0A"));

        Assert.Equal(SwitchForwardingDecision.MulticastFlood, result.Decision);
        Assert.Equal([f.P2, f.P3, f.P4], result.EgressPorts);
        Assert.True(raised);
    }

    [Fact]
    public void ProcessFrame_NeverFloodsOutAnAdministrativelyDownPort()
    {
        var f = new Fixture();
        f.P3.BringDown();

        var result = f.Process(f.P1, f.M1, MacAddress.Broadcast);

        Assert.Equal([f.P2, f.P4], result.EgressPorts);
    }

    [Fact]
    public void ProcessFrame_NeverFloodsOutADisconnectedPort()
    {
        var f = new Fixture();
        f.Disconnect(f.P4);

        var result = f.Process(f.P1, f.M1, MacAddress.Broadcast);

        Assert.Equal([f.P2, f.P3], result.EgressPorts);
    }

    [Fact]
    public void ProcessFrame_SameSourceOnADifferentPort_MovesTheEntry_AndRaisesMacMoved()
    {
        var f = new Fixture();
        f.Process(f.P1, f.M1, f.M2);

        SwitchingEventArgs? moved = null;
        f.Engine.MacMoved += (_, e) => moved = e;

        var result = f.Process(f.P3, f.M1, f.M2);

        Assert.Equal(MacLearnOutcome.Moved, result.SourceLearn.Outcome);
        Assert.Same(f.P3, ((Switch)f.Switch).MacAddressTable.Lookup(f.M1)!.Port);
        Assert.NotNull(moved);
        Assert.Same(f.P1, moved!.PreviousPort);
        Assert.Same(f.P3, moved.Port);
    }

    [Fact]
    public void ProcessFrame_DestinationKnownOnTheIngressPort_IsDropped()
    {
        var f = new Fixture();
        f.Process(f.P1, f.M1, f.M2); // M1 learned on P1

        var result = f.Process(f.P1, f.M3, f.M1); // now a frame for M1 arrives on P1 again

        Assert.Equal(SwitchForwardingDecision.Drop, result.Decision);
        Assert.Equal(SwitchDropReasons.DestinationOnIngressPort, result.DropReason);
        Assert.Empty(result.EgressPorts);
    }

    [Fact]
    public void ProcessFrame_AfterAgingRemovesTheEntry_KnownUnicastBecomesUnknownFlood()
    {
        var f = new Fixture();
        ((Switch)f.Switch).MacAddressTable.AgingTime = TimeSpan.FromSeconds(300);
        f.Process(f.P2, f.M2, f.M1); // learn M2 on P2 at t = 0

        SwitchingEventArgs? expired = null;
        f.Engine.MacEntryExpired += (_, e) => expired = e;

        f.Clock.Advance(TimeSpan.FromSeconds(301));
        var result = f.Process(f.P1, f.M1, f.M2);

        Assert.Equal(SwitchForwardingDecision.UnknownUnicastFlood, result.Decision);
        Assert.NotNull(expired);
        Assert.Equal(f.M2, expired!.Mac);
    }

    [Fact]
    public void ProcessFrame_WithAnInactivePort_FlushesEntriesLearnedThere_AndRaisesFlushed()
    {
        var f = new Fixture();
        f.Process(f.P2, f.M2, f.M1); // learn M2 on P2
        f.P2.BringDown();

        SwitchingEventArgs? flushed = null;
        f.Engine.MacEntriesFlushed += (_, e) => flushed = e;

        f.Process(f.P1, f.M1, f.M3);

        Assert.False(((Switch)f.Switch).MacAddressTable.Contains(f.M2));
        Assert.NotNull(flushed);
        Assert.Same(f.P2, flushed!.Port);
    }

    [Fact]
    public void ProcessFrame_OnANonSwitchDevice_IsDropped()
    {
        var f = new Fixture();
        var pc = f.Network.Devices.First(d => d.DeviceType == DeviceType.Pc);

        var result = f.Engine.ProcessFrame(f.Network, pc, pc.Interfaces.Single(), f.Frame(f.M1, f.M2));

        Assert.Equal(SwitchForwardingDecision.Drop, result.Decision);
        Assert.Equal(SwitchDropReasons.NotASwitch, result.DropReason);
    }

    [Fact]
    public void ProcessFrame_OnADownIngressPort_IsDropped_WithoutLearning()
    {
        var f = new Fixture();
        f.P1.BringDown();

        var result = f.Process(f.P1, f.M1, f.M2);

        Assert.Equal(SwitchDropReasons.IngressNotOperational, result.DropReason);
        Assert.Equal(MacLearnOutcome.Unchanged, result.SourceLearn.Outcome);
        Assert.False(((Switch)f.Switch).MacAddressTable.Contains(f.M1));
    }

    [Fact]
    public void ProcessFrame_WhenNoOtherPortIsEligible_DropsButStillLearnsTheSource()
    {
        var f = new Fixture();
        f.Disconnect(f.P2);
        f.Disconnect(f.P3);
        f.Disconnect(f.P4);

        var result = f.Process(f.P1, f.M1, MacAddress.Broadcast);

        Assert.Equal(SwitchForwardingDecision.Drop, result.Decision);
        Assert.Equal(SwitchDropReasons.NoEgressPorts, result.DropReason);
        Assert.True(((Switch)f.Switch).MacAddressTable.Contains(f.M1));
    }

    [Fact]
    public void ProcessFrame_RaisesFrameReceived()
    {
        var f = new Fixture();
        SwitchingEventArgs? received = null;
        f.Engine.FrameReceived += (_, e) => received = e;

        f.Process(f.P1, f.M1, f.M2);

        Assert.NotNull(received);
        Assert.Same(f.P1, received!.IngressPort);
    }

    [Fact]
    public void AgeMacTable_RemovesExpiredEntries_AndReturnsHowMany()
    {
        var f = new Fixture();
        var table = ((Switch)f.Switch).MacAddressTable;
        table.AgingTime = TimeSpan.FromSeconds(300);
        f.Process(f.P1, f.M1, f.M2);
        f.Process(f.P2, f.M2, f.M1);

        f.Clock.Advance(TimeSpan.FromSeconds(400));
        var removed = f.Engine.AgeMacTable(f.Switch);

        Assert.Equal(2, removed);
        Assert.Equal(0, table.Count);
    }

    [Fact]
    public void HandlePortInactive_FlushesThatPortsEntries()
    {
        var f = new Fixture();
        f.Process(f.P2, f.M2, f.M1);

        var removed = f.Engine.HandlePortInactive(f.P2);

        Assert.Equal(1, removed);
        Assert.False(((Switch)f.Switch).MacAddressTable.Contains(f.M2));
    }
}
