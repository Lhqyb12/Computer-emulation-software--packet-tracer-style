using NetSim.Core.Networking;
using NetSim.Core.Vlans;

namespace NetSim.Core.Switching;

/// <summary>
/// One row of a <see cref="MacAddressTable"/>: the <see cref="Vlan"/> and <see cref="MacAddress"/>
/// a switch has learned, the <see cref="Port"/> it was learned on, how it got there
/// (<see cref="Type"/>), and the simulation-time instants it was first learned
/// (<see cref="LearnedAt"/>) and most recently refreshed (<see cref="LastSeenAt"/>). Immutable: a
/// refresh or a move replaces the entry rather than mutating it, so a mapping and its age can
/// never disagree - the same shape as <see cref="ArpCacheEntry"/>.
///
/// The row is scoped to a VLAN (Phase 26): the same MAC address can appear in two different VLANs
/// on the same switch, mapped to different ports, and the two rows are completely independent.
///
/// Aging is expressed as data (<see cref="Age"/> / <see cref="IsExpired"/>), not a scheduler; the
/// table prunes lazily and on an explicit sweep. A <see cref="MacTableEntryType.Static"/> entry
/// never expires.
/// </summary>
public sealed class MacAddressTableEntry
{
    public MacAddressTableEntry(
        VlanId vlan,
        MacAddress macAddress,
        NetworkInterface port,
        MacTableEntryType type,
        TimeSpan learnedAt,
        TimeSpan lastSeenAt)
    {
        ArgumentNullException.ThrowIfNull(port);
        Vlan = vlan;
        MacAddress = macAddress;
        Port = port;
        Type = type;
        LearnedAt = learnedAt;
        LastSeenAt = lastSeenAt;
    }

    /// <summary>The VLAN this row belongs to.</summary>
    public VlanId Vlan { get; }

    /// <summary>The learned unicast MAC address this row resolves.</summary>
    public MacAddress MacAddress { get; }

    /// <summary>The switch port a frame for <see cref="MacAddress"/> should be forwarded out of.</summary>
    public NetworkInterface Port { get; }

    public MacTableEntryType Type { get; }

    public bool IsDynamic => Type == MacTableEntryType.Dynamic;

    public bool IsStatic => Type == MacTableEntryType.Static;

    /// <summary>Simulation time when the address was first learned on <see cref="Port"/>.</summary>
    public TimeSpan LearnedAt { get; }

    /// <summary>Simulation time when a frame from <see cref="MacAddress"/> was last seen on <see cref="Port"/>.</summary>
    public TimeSpan LastSeenAt { get; }

    /// <summary>How long the entry has gone unused as of <paramref name="now"/> (never negative).</summary>
    public TimeSpan Age(TimeSpan now) => now > LastSeenAt ? now - LastSeenAt : TimeSpan.Zero;

    /// <summary>
    /// True when a dynamic entry has gone unused for at least <paramref name="agingTime"/> as of
    /// <paramref name="now"/>. Always false for a static entry.
    /// </summary>
    public bool IsExpired(TimeSpan now, TimeSpan agingTime) => IsDynamic && Age(now) >= agingTime;

    public override string ToString() => $"VLAN {Vlan} {MacAddress} -> {Port.Device.Name}/{Port.Name} ({Type})";
}
