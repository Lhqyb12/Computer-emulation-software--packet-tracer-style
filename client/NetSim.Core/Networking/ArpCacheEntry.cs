using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Networking;

/// <summary>
/// One IPv4 -&gt; MAC mapping in an <see cref="ArpCache"/>: the <see cref="ProtocolAddress"/>, the
/// <see cref="HardwareAddress"/> it resolves to, how the entry was learned
/// (<see cref="State"/>), when it was learned (<see cref="CreatedAtUtc"/>) and - for a dynamic
/// entry - when it stops being valid (<see cref="ExpiresAtUtc"/>). Immutable value: an update
/// replaces the entry rather than mutating it, so a mapping and its age can never disagree.
///
/// Expiration is expressed as data (<see cref="CreatedAtUtc"/> / <see cref="ExpiresAtUtc"/> /
/// <see cref="IsExpired"/>), not a scheduler - the cache prunes lazily on access. This is the
/// "clean abstraction, no complex scheduler" the Phase 20 brief asks for.
/// </summary>
public sealed class ArpCacheEntry
{
    /// <summary>
    /// Lifetime a dynamic entry gets when the cache is not told otherwise (4 hours - a common
    /// router default). Long enough that a stable simulated network never re-ARPs mid-scenario.
    /// </summary>
    public static readonly TimeSpan DefaultDynamicLifetime = TimeSpan.FromHours(4);

    private ArpCacheEntry(
        IPv4Address protocolAddress,
        MacAddress hardwareAddress,
        ArpCacheEntryState state,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? expiresAtUtc)
    {
        ProtocolAddress = protocolAddress;
        HardwareAddress = hardwareAddress;
        State = state;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>The IPv4 address this entry resolves.</summary>
    public IPv4Address ProtocolAddress { get; }

    /// <summary>The MAC address <see cref="ProtocolAddress"/> maps to.</summary>
    public MacAddress HardwareAddress { get; }

    /// <summary>Whether the entry is dynamic (learned, ages out) or static (configured, permanent).</summary>
    public ArpCacheEntryState State { get; }

    /// <summary>When the entry was created / last refreshed.</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>When the entry stops being valid, or null for a <see cref="ArpCacheEntryState.Static"/> entry (never expires).</summary>
    public DateTimeOffset? ExpiresAtUtc { get; }

    public bool IsDynamic => State == ArpCacheEntryState.Dynamic;

    public bool IsStatic => State == ArpCacheEntryState.Static;

    /// <summary>True when <paramref name="asOfUtc"/> is at or past <see cref="ExpiresAtUtc"/>. Always false for a static entry.</summary>
    public bool IsExpired(DateTimeOffset asOfUtc) => ExpiresAtUtc is { } expiry && asOfUtc >= expiry;

    /// <summary>
    /// Remaining lifetime at <paramref name="asOfUtc"/> - <see cref="TimeSpan.Zero"/> once expired,
    /// null when the entry never expires.
    /// </summary>
    public TimeSpan? TimeToLive(DateTimeOffset asOfUtc) =>
        ExpiresAtUtc is { } expiry ? (expiry > asOfUtc ? expiry - asOfUtc : TimeSpan.Zero) : null;

    /// <summary>
    /// Builds a dynamic entry that expires <paramref name="lifetime"/> after
    /// <paramref name="createdAtUtc"/>. Throws <see cref="DomainException"/> for an unusable IPv4
    /// address (0.0.0.0, broadcast, multicast), a non-unicast MAC, or a non-positive lifetime.
    /// </summary>
    public static ArpCacheEntry Dynamic(
        IPv4Address protocolAddress, MacAddress hardwareAddress, DateTimeOffset createdAtUtc, TimeSpan lifetime)
    {
        GuardAddresses(protocolAddress, hardwareAddress);
        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainException("An ARP cache entry lifetime must be positive.");
        }

        return new ArpCacheEntry(
            protocolAddress, hardwareAddress, ArpCacheEntryState.Dynamic, createdAtUtc, createdAtUtc + lifetime);
    }

    /// <summary>
    /// Builds a static entry that never expires. Same address validation as
    /// <see cref="Dynamic"/>. The foundation for a future "configure a permanent ARP entry" feature.
    /// </summary>
    public static ArpCacheEntry Static(IPv4Address protocolAddress, MacAddress hardwareAddress, DateTimeOffset createdAtUtc)
    {
        GuardAddresses(protocolAddress, hardwareAddress);
        return new ArpCacheEntry(
            protocolAddress, hardwareAddress, ArpCacheEntryState.Static, createdAtUtc, expiresAtUtc: null);
    }

    private static void GuardAddresses(IPv4Address protocolAddress, MacAddress hardwareAddress)
    {
        if (protocolAddress.IsUnspecified)
        {
            throw new DomainException("An ARP cache entry's IPv4 address must be set (not 0.0.0.0).");
        }

        if (protocolAddress.IsLimitedBroadcast || protocolAddress.IsMulticast)
        {
            throw new DomainException($"An ARP cache entry's IPv4 address '{protocolAddress}' is not a host address.");
        }

        if (hardwareAddress.IsUnspecified || !hardwareAddress.IsUnicast)
        {
            throw new DomainException(
                $"An ARP cache entry's MAC address must be a unicast address, but was '{hardwareAddress}'.");
        }
    }

    public override string ToString() => $"{ProtocolAddress} -> {HardwareAddress} ({State})";
}
