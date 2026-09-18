using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Common;
using NetSim.Core.Networking;
using NetSim.Core.Vlans;

namespace NetSim.Core.Switching;

/// <summary>
/// A single switch's Layer 2 forwarding table: the <c>(VLAN, MAC address) -&gt; switch port</c>
/// mappings it has learned from received frames. One table per <see cref="Devices.Switch"/> (never
/// a global, network-wide table) - it is exposed through <see cref="Devices.Switch.MacAddressTable"/>.
///
/// The key is <c>(VLAN, MAC)</c> (Phase 26). The same MAC address may be learned in two VLANs at
/// once, on two different ports, and the two entries never interfere - this is what enforces VLAN
/// isolation at the forwarding layer. The MAC-only overloads act on <see cref="VlanId.Default"/>
/// and exist so pre-VLAN callers and tests keep working unchanged.
///
/// Time is passed in explicitly (<c>now</c>) rather than read from a clock inside the table, so
/// aging is fully deterministic in tests. Implementations must be safe for concurrent use.
/// </summary>
public interface IMacAddressTable
{
    /// <summary>
    /// How long a dynamic entry may go unused before <see cref="AgeEntries"/> removes it. Defaults
    /// to a realistic Ethernet-switch value; settable so a later configuration surface can change
    /// it. Must be positive.
    /// </summary>
    TimeSpan AgingTime { get; set; }

    /// <summary>The number of live entries currently in the table (across all VLANs).</summary>
    int Count { get; }

    /// <summary>
    /// Records that a frame in <paramref name="vlan"/> with source <paramref name="mac"/> arrived
    /// on <paramref name="port"/> at simulation time <paramref name="now"/>. New <c>(VLAN, MAC)</c>
    /// =&gt; a dynamic entry is added; same VLAN+MAC+port =&gt; refreshed; same VLAN+MAC, different
    /// port =&gt; moved. A non-unicast <paramref name="mac"/> is ignored
    /// (<see cref="MacLearnOutcome.Unchanged"/>).
    /// </summary>
    MacLearnResult Learn(VlanId vlan, MacAddress mac, NetworkInterface port, TimeSpan now);

    /// <summary>MAC-only overload acting on <see cref="VlanId.Default"/>.</summary>
    MacLearnResult Learn(MacAddress mac, NetworkInterface port, TimeSpan now);

    /// <summary>The entry for <paramref name="mac"/> in <paramref name="vlan"/>, or null.</summary>
    MacAddressTableEntry? Lookup(VlanId vlan, MacAddress mac);

    /// <summary>MAC-only overload acting on <see cref="VlanId.Default"/>.</summary>
    MacAddressTableEntry? Lookup(MacAddress mac);

    /// <summary>Non-throwing lookup of the egress port for <paramref name="mac"/> in <paramref name="vlan"/>.</summary>
    bool TryGetPort(VlanId vlan, MacAddress mac, [NotNullWhen(true)] out NetworkInterface? port);

    /// <summary>MAC-only overload acting on <see cref="VlanId.Default"/>.</summary>
    bool TryGetPort(MacAddress mac, [NotNullWhen(true)] out NetworkInterface? port);

    /// <summary>True when <paramref name="mac"/> is currently in the table in <paramref name="vlan"/>.</summary>
    bool Contains(VlanId vlan, MacAddress mac);

    /// <summary>MAC-only overload acting on <see cref="VlanId.Default"/>.</summary>
    bool Contains(MacAddress mac);

    /// <summary>Removes the entry for <paramref name="mac"/> in <paramref name="vlan"/>. Returns false when there was none.</summary>
    bool Remove(VlanId vlan, MacAddress mac);

    /// <summary>MAC-only overload acting on <see cref="VlanId.Default"/>.</summary>
    bool Remove(MacAddress mac);

    /// <summary>
    /// Removes every entry (in any VLAN) learned on the port with id <paramref name="portId"/> -
    /// used when a port goes down, is disconnected, or has its VLAN configuration changed. Returns
    /// the removed entries.
    /// </summary>
    IReadOnlyList<MacAddressTableEntry> RemoveEntriesForPort(EntityId portId);

    /// <summary>
    /// Removes every entry in <paramref name="vlan"/> learned on the port with id
    /// <paramref name="portId"/> - used when a single VLAN is removed from a trunk. Returns the
    /// removed entries.
    /// </summary>
    IReadOnlyList<MacAddressTableEntry> RemoveEntriesForVlanOnPort(VlanId vlan, EntityId portId);

    /// <summary>Removes every entry in <paramref name="vlan"/> (any port). Returns the removed entries.</summary>
    IReadOnlyList<MacAddressTableEntry> RemoveEntriesForVlan(VlanId vlan);

    /// <summary>Empties the table.</summary>
    void Clear();

    /// <summary>
    /// Removes every dynamic entry that has gone unused for at least <see cref="AgingTime"/> as of
    /// <paramref name="now"/> and returns them (so the caller can raise an "entry expired"
    /// diagnostic).
    /// </summary>
    IReadOnlyList<MacAddressTableEntry> AgeEntries(TimeSpan now);

    /// <summary>A snapshot of every entry, newest-learned first.</summary>
    IReadOnlyList<MacAddressTableEntry> GetEntries();

    /// <summary>Every entry learned on the port with id <paramref name="portId"/> (any VLAN).</summary>
    IReadOnlyList<MacAddressTableEntry> GetEntriesForPort(EntityId portId);
}
