using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Topology;
using NetSim.Core.Vlans;

namespace NetSim.Core.Switching;

/// <summary>
/// Default <see cref="ISwitchingEngine"/>. Stateless apart from its event subscribers - every bit
/// of Layer 2 forwarding state lives in the per-switch <see cref="Devices.Switch.MacAddressTable"/>
/// and <see cref="Devices.Switch.Vlans"/> configuration, so two switches in the same topology
/// never share learning. Simulated time comes from the injected <see cref="ISimulationClock"/>
/// (only MAC aging depends on it).
///
/// <para><b>VLAN awareness (Phase 26).</b> Every frame is classified into a VLAN from the ingress
/// port's <see cref="SwitchPortVlanConfiguration"/> before anything else happens: an access port
/// uses its access VLAN; a trunk uses the frame's 802.1Q tag, or the trunk's native VLAN when the
/// frame is untagged. MAC learning, MAC lookup and flooding are all scoped to that VLAN, and a
/// frame only ever leaves ports that carry its VLAN - so a broadcast in VLAN 10 never reaches a
/// VLAN 20 access port, and there is no path between VLANs (inter-VLAN routing is a later phase).
/// Frames leaving a trunk for a non-native VLAN are 802.1Q-tagged; everything else leaves
/// untagged.</para>
/// </summary>
public sealed class SwitchingEngine : ISwitchingEngine
{
    private readonly ISimulationClock _clock;

    public SwitchingEngine(ISimulationClock? clock = null)
    {
        _clock = clock ?? new SystemSimulationClock();
    }

    public event EventHandler<SwitchingEventArgs>? FrameReceived;
    public event EventHandler<SwitchingEventArgs>? FrameVlanAssigned;
    public event EventHandler<SwitchingEventArgs>? FrameBlocked;
    public event EventHandler<SwitchingEventArgs>? MacLearned;
    public event EventHandler<SwitchingEventArgs>? MacMoved;
    public event EventHandler<SwitchingEventArgs>? MacEntryExpired;
    public event EventHandler<SwitchingEventArgs>? MacEntriesFlushed;
    public event EventHandler<SwitchingEventArgs>? FrameForwarded;
    public event EventHandler<SwitchingEventArgs>? UnknownUnicastFlooded;
    public event EventHandler<SwitchingEventArgs>? BroadcastFlooded;
    public event EventHandler<SwitchingEventArgs>? MulticastFlooded;
    public event EventHandler<SwitchingEventArgs>? FrameDropped;

    public SwitchingResult ProcessFrame(
        ITopologyView topology,
        NetworkDevice switchDevice,
        NetworkInterface ingressPort,
        EthernetFrame frame)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(switchDevice);
        ArgumentNullException.ThrowIfNull(ingressPort);
        ArgumentNullException.ThrowIfNull(frame);

        if (switchDevice is not Switch @switch)
        {
            return DropBeforeLearning(switchDevice, ingressPort, frame, SwitchDropReasons.NotASwitch,
                $"'{switchDevice.Name}' is a {switchDevice.DeviceType}, not a switch.");
        }

        var validation = frame.Validate();
        if (!validation.IsValid)
        {
            return DropBeforeLearning(@switch, ingressPort, frame, SwitchDropReasons.InvalidFrame,
                string.Join("; ", validation.Errors));
        }

        if (topology.GetInterface(ingressPort.Id) is null)
        {
            return DropBeforeLearning(@switch, ingressPort, frame, SwitchDropReasons.IngressNotInTopology,
                $"Ingress port '{ingressPort.Name}' is not part of the topology.");
        }

        if (ingressPort.Device.Id != @switch.Id)
        {
            return DropBeforeLearning(@switch, ingressPort, frame, SwitchDropReasons.IngressNotOnSwitch,
                $"Ingress port '{ingressPort.Name}' belongs to '{ingressPort.Device.Name}', not '{@switch.Name}'.");
        }

        if (!ingressPort.SupportsEthernet || !ingressPort.IsOperational)
        {
            return DropBeforeLearning(@switch, ingressPort, frame, SwitchDropReasons.IngressNotOperational,
                $"Ingress port '{ingressPort.Name}' on '{@switch.Name}' is not an operational Ethernet port.");
        }

        var table = @switch.MacAddressTable;
        var now = _clock.Now;

        FrameReceived?.Invoke(this, new SwitchingEventArgs(
            @switch, frame, ingressPort,
            detail: $"[Switch {@switch.Name}] Received frame on {ingressPort.Name}: {frame.SourceMac} -> {frame.DestinationMac} [{frame.EtherType.Name}]"));

        AgeTable(@switch, table, now);
        PruneInactivePorts(@switch, table, topology);

        // ---- Step: classify the frame into a VLAN from the ingress port's configuration ----
        var ingressConfig = @switch.GetPortVlanConfiguration(ingressPort);
        var classification = ClassifyIngressVlan(@switch, ingressPort, ingressConfig, frame);
        if (classification.Blocked is { } blocked)
        {
            FrameBlocked?.Invoke(this, new SwitchingEventArgs(
                @switch, frame, ingressPort, SwitchForwardingDecision.Blocked,
                dropReason: blocked.DropReason, vlan: classification.Vlan,
                detail: $"[Switch {@switch.Name}] Blocked frame on {ingressPort.Name}: {blocked.Reason}"));
            return blocked;
        }

        var vlan = classification.Vlan;

        FrameVlanAssigned?.Invoke(this, new SwitchingEventArgs(
            @switch, frame, ingressPort, vlan: vlan,
            detail: $"[Switch {@switch.Name}] Frame received on {ingressPort.Name} assigned to VLAN {vlan}."));

        if (@switch.Vlans.GetVlan(vlan) is { IsActive: false })
        {
            var inactive = SwitchingResult.Blocked(
                @switch, ingressPort, frame, vlan, SwitchDropReasons.VlanInactive,
                $"VLAN {vlan} is administratively inactive on {@switch.Name}.");
            FrameBlocked?.Invoke(this, new SwitchingEventArgs(
                @switch, frame, ingressPort, SwitchForwardingDecision.Blocked,
                dropReason: SwitchDropReasons.VlanInactive, vlan: vlan,
                detail: $"[Switch {@switch.Name}] Blocked frame on {ingressPort.Name}: VLAN {vlan} is inactive."));
            return inactive;
        }

        // ---- Step: learn the source MAC on the ingress port, scoped to the VLAN ----
        var learn = table.Learn(vlan, frame.SourceMac, ingressPort, now);
        switch (learn.Outcome)
        {
            case MacLearnOutcome.Added:
            case MacLearnOutcome.Refreshed:
                MacLearned?.Invoke(this, new SwitchingEventArgs(
                    @switch, frame, ingressPort, mac: frame.SourceMac, port: ingressPort, vlan: vlan,
                    detail: $"[Switch {@switch.Name}] Learned {frame.SourceMac} on {ingressPort.Name} in VLAN {vlan} ({learn.Outcome})."));
                break;
            case MacLearnOutcome.Moved:
                MacMoved?.Invoke(this, new SwitchingEventArgs(
                    @switch, frame, ingressPort, mac: frame.SourceMac, port: ingressPort, previousPort: learn.PreviousPort, vlan: vlan,
                    detail: $"[Switch {@switch.Name}] {frame.SourceMac} moved from {learn.PreviousPort!.Name} to {ingressPort.Name} in VLAN {vlan}."));
                break;
        }

        // ---- Step: classify the destination and decide (within the VLAN) ----
        return frame.DestinationMac.Kind switch
        {
            MacAddressKind.Broadcast => Flood(
                topology, @switch, ingressPort, frame, vlan, learn,
                SwitchForwardingDecision.BroadcastFlood, $"Broadcast flooded within VLAN {vlan}", BroadcastFlooded),

            MacAddressKind.Multicast => Flood(
                topology, @switch, ingressPort, frame, vlan, learn,
                SwitchForwardingDecision.MulticastFlood,
                $"Multicast {frame.DestinationMac} flooded within VLAN {vlan} (no IGMP snooping)", MulticastFlooded),

            _ => SwitchUnicast(topology, @switch, ingressPort, frame, vlan, learn, table),
        };
    }

    public int AgeMacTable(NetworkDevice switchDevice)
    {
        ArgumentNullException.ThrowIfNull(switchDevice);
        if (switchDevice is not Switch @switch)
        {
            return 0;
        }

        return AgeTable(@switch, @switch.MacAddressTable, _clock.Now);
    }

    public int HandlePortInactive(NetworkInterface port)
    {
        ArgumentNullException.ThrowIfNull(port);
        if (port.Device is not Switch @switch)
        {
            return 0;
        }

        var removed = @switch.MacAddressTable.RemoveEntriesForPort(port.Id);
        if (removed.Count > 0)
        {
            MacEntriesFlushed?.Invoke(this, new SwitchingEventArgs(
                @switch, port: port,
                detail: $"[Switch {@switch.Name}] Flushed {removed.Count} MAC entr{(removed.Count == 1 ? "y" : "ies")} learned on {port.Name} (port inactive)."));
        }

        return removed.Count;
    }

    // -----------------------------------------------------------------------------------------

    private readonly record struct VlanClassification(VlanId Vlan, SwitchingResult? Blocked);

    // Works out which VLAN the ingress port places this frame in, or produces a Blocked result when
    // the VLAN boundary rejects it outright.
    private static VlanClassification ClassifyIngressVlan(
        Switch @switch, NetworkInterface ingressPort, SwitchPortVlanConfiguration config, EthernetFrame frame)
    {
        if (config.IsAccess)
        {
            if (frame.IsVlanTagged && frame.VlanTag!.Value.VlanId != config.AccessVlan)
            {
                return new VlanClassification(config.AccessVlan, SwitchingResult.Blocked(
                    @switch, ingressPort, frame, frame.VlanTag.Value.VlanId, SwitchDropReasons.TaggedFrameOnAccessPort,
                    $"A tagged frame for VLAN {frame.VlanTag.Value.VlanId} arrived on access port {ingressPort.Name} (access VLAN {config.AccessVlan})."));
            }

            return new VlanClassification(config.AccessVlan, null);
        }

        // Trunk port.
        if (frame.IsVlanTagged)
        {
            var tagged = frame.VlanTag!.Value.VlanId;
            if (!config.IsVlanAllowed(tagged))
            {
                return new VlanClassification(tagged, SwitchingResult.Blocked(
                    @switch, ingressPort, frame, tagged, SwitchDropReasons.VlanNotAllowedOnTrunk,
                    $"VLAN {tagged} is not allowed on trunk {ingressPort.Name}."));
            }

            return new VlanClassification(tagged, null);
        }

        var native = config.NativeVlan;
        if (!config.IsVlanAllowed(native))
        {
            return new VlanClassification(native, SwitchingResult.Blocked(
                @switch, ingressPort, frame, native, SwitchDropReasons.VlanNotAllowedOnTrunk,
                $"Native VLAN {native} is not allowed on trunk {ingressPort.Name}."));
        }

        return new VlanClassification(native, null);
    }

    private SwitchingResult SwitchUnicast(
        ITopologyView topology,
        Switch @switch,
        NetworkInterface ingressPort,
        EthernetFrame frame,
        VlanId vlan,
        MacLearnResult learn,
        IMacAddressTable table)
    {
        var entry = table.Lookup(vlan, frame.DestinationMac);

        if (entry is not null && entry.Port.Id == ingressPort.Id)
        {
            // The destination lives on the same port the frame came in on (host on a shared
            // segment, or source == destination). A real switch filters this - it never echoes a
            // frame back out the ingress port.
            table.Remove(vlan, frame.DestinationMac); // keep the entry honest; it will be re-learned
            return Drop(@switch, ingressPort, frame, vlan, learn, SwitchDropReasons.DestinationOnIngressPort,
                $"Destination {frame.DestinationMac} is on the ingress port {ingressPort.Name} in VLAN {vlan}; frame filtered.");
        }

        if (entry is not null
            && IsEligibleEgress(topology, entry.Port, ingressPort)
            && PortCarriesVlan(@switch, entry.Port, vlan))
        {
            var egress = BuildEgress(@switch, [entry.Port], vlan, frame);
            var result = SwitchingResult.Forward(
                @switch, ingressPort, frame, vlan, SwitchForwardingDecision.KnownUnicast, egress,
                $"Destination MAC {frame.DestinationMac} found on {entry.Port.Name} in VLAN {vlan}", learn);

            FrameForwarded?.Invoke(this, new SwitchingEventArgs(
                @switch, frame, ingressPort, SwitchForwardingDecision.KnownUnicast, egress.Select(e => e.Port).ToList(),
                mac: frame.DestinationMac, port: entry.Port, vlan: vlan,
                detail: $"[Switch {@switch.Name}] Forwarding frame for {frame.DestinationMac} to {entry.Port.Name} in VLAN {vlan}."));
            return result;
        }

        if (entry is not null)
        {
            // Learned, but the port is no longer usable / no longer carries the VLAN - drop the
            // stale entry and flood instead.
            table.Remove(vlan, frame.DestinationMac);
        }

        return Flood(topology, @switch, ingressPort, frame, vlan, learn,
            SwitchForwardingDecision.UnknownUnicastFlood,
            $"Destination MAC {frame.DestinationMac} not in table for VLAN {vlan}", UnknownUnicastFlooded);
    }

    private SwitchingResult Flood(
        ITopologyView topology,
        Switch @switch,
        NetworkInterface ingressPort,
        EthernetFrame frame,
        VlanId vlan,
        MacLearnResult learn,
        SwitchForwardingDecision decision,
        string reason,
        EventHandler<SwitchingEventArgs>? floodEvent)
    {
        var egressPorts = EligibleEgressPorts(topology, @switch, ingressPort, vlan);
        if (egressPorts.Count == 0)
        {
            var anyPhysicalEgress = @switch.Interfaces.Any(p => IsEligibleEgress(topology, p, ingressPort));
            var dropReason = anyPhysicalEgress ? SwitchDropReasons.NoEgressPortsInVlan : SwitchDropReasons.NoEgressPorts;
            var detail = anyPhysicalEgress
                ? $"{reason}, but no other operational port carries VLAN {vlan}."
                : $"{reason}, but the switch has no other operational, connected port.";
            return Drop(@switch, ingressPort, frame, vlan, learn, dropReason, detail);
        }

        var egress = BuildEgress(@switch, egressPorts, vlan, frame);
        var result = SwitchingResult.Forward(@switch, ingressPort, frame, vlan, decision, egress, reason, learn);
        floodEvent?.Invoke(this, new SwitchingEventArgs(
            @switch, frame, ingressPort, decision, egressPorts, mac: frame.DestinationMac, vlan: vlan,
            detail: $"[Switch {@switch.Name}] {reason}: flooding to {string.Join(", ", egressPorts.Select(p => p.Name))}."));
        return result;
    }

    private static IReadOnlyList<SwitchEgress> BuildEgress(
        Switch @switch, IReadOnlyList<NetworkInterface> ports, VlanId vlan, EthernetFrame frame) =>
        ports.Select(p => new SwitchEgress(p, EgressFrameFor(@switch, p, vlan, frame))).ToList();

    // The per-port wire form: a trunk carrying this VLAN as a non-native VLAN sends it 802.1Q-tagged;
    // an access port, or a trunk's native VLAN, sends it untagged - so the host at the far end of an
    // access port never has to understand VLAN tagging.
    private static EthernetFrame EgressFrameFor(Switch @switch, NetworkInterface port, VlanId vlan, EthernetFrame frame)
    {
        var config = @switch.GetPortVlanConfiguration(port);
        if (config.IsTrunk && config.NativeVlan != vlan)
        {
            var tag = frame.VlanTag is { } existing
                ? new VlanTag(vlan, existing.PriorityCodePoint, existing.DropEligible)
                : VlanTag.For(vlan);
            return frame.Tagged(tag);
        }

        return frame.Untagged();
    }

    private IReadOnlyList<NetworkInterface> EligibleEgressPorts(
        ITopologyView topology, Switch @switch, NetworkInterface ingressPort, VlanId vlan) =>
        @switch.Interfaces
            .Where(p => IsEligibleEgress(topology, p, ingressPort) && PortCarriesVlan(@switch, p, vlan))
            .ToList();

    private static bool PortCarriesVlan(Switch @switch, NetworkInterface port, VlanId vlan) =>
        @switch.GetPortVlanConfiguration(port).CarriesVlan(vlan);

    private static bool IsEligibleEgress(ITopologyView topology, NetworkInterface port, NetworkInterface ingressPort)
    {
        if (port.Id == ingressPort.Id)
        {
            return false;
        }

        return port.SupportsEthernet
            && port.IsOperational
            && port.IsConnected
            && topology.GetInterface(port.Id) is not null
            && topology.GetConnection(port) is not null;
    }

    // Realistic behaviour (brief section 21): an entry learned on a port that is now down or
    // unplugged must not keep steering traffic into a dead port. Only ports that actually hold an
    // entry are examined, so this is not a full port scan on every frame.
    private void PruneInactivePorts(Switch @switch, IMacAddressTable table, ITopologyView topology)
    {
        var entries = table.GetEntries();
        if (entries.Count == 0)
        {
            return;
        }

        var deadPorts = entries
            .Select(e => e.Port)
            .DistinctBy(p => p.Id)
            .Where(p => !IsPortUsable(p, topology))
            .ToList();

        foreach (var port in deadPorts)
        {
            var removed = table.RemoveEntriesForPort(port.Id);
            if (removed.Count > 0)
            {
                MacEntriesFlushed?.Invoke(this, new SwitchingEventArgs(
                    @switch, port: port,
                    detail: $"[Switch {@switch.Name}] Flushed {removed.Count} MAC entr{(removed.Count == 1 ? "y" : "ies")} on inactive port {port.Name}."));
            }
        }
    }

    private static bool IsPortUsable(NetworkInterface port, ITopologyView topology) =>
        port.SupportsEthernet
        && port.IsOperational
        && port.IsConnected
        && topology.GetConnection(port) is not null;

    private int AgeTable(Switch @switch, IMacAddressTable table, TimeSpan now)
    {
        var expired = table.AgeEntries(now);
        foreach (var entry in expired)
        {
            MacEntryExpired?.Invoke(this, new SwitchingEventArgs(
                @switch, mac: entry.MacAddress, port: entry.Port, vlan: entry.Vlan,
                detail: $"[Switch {@switch.Name}] MAC {entry.MacAddress} on {entry.Port.Name} (VLAN {entry.Vlan}) expired (unused {entry.Age(now).TotalSeconds:F0}s)."));
        }

        return expired.Count;
    }

    private SwitchingResult DropBeforeLearning(
        NetworkDevice switchDevice, NetworkInterface ingressPort, EthernetFrame frame,
        Packets.PacketDropReason reason, string detail)
    {
        FrameDropped?.Invoke(this, new SwitchingEventArgs(
            switchDevice, frame, ingressPort, SwitchForwardingDecision.Drop, dropReason: reason,
            detail: $"[Switch {switchDevice.Name}] Dropped frame on {ingressPort.Name}: {detail}"));
        return SwitchingResult.Drop(switchDevice, ingressPort, frame, reason, detail, MacLearnResult.Unchanged);
    }

    private SwitchingResult Drop(
        Switch @switch, NetworkInterface ingressPort, EthernetFrame frame, VlanId vlan, MacLearnResult learn,
        Packets.PacketDropReason reason, string detail)
    {
        FrameDropped?.Invoke(this, new SwitchingEventArgs(
            @switch, frame, ingressPort, SwitchForwardingDecision.Drop, dropReason: reason, vlan: vlan,
            detail: $"[Switch {@switch.Name}] Dropped frame on {ingressPort.Name}: {detail}"));
        return SwitchingResult.Drop(@switch, ingressPort, frame, reason, detail, learn, vlan);
    }
}
