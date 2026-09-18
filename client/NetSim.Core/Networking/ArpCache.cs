using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Networking;

/// <summary>What an <see cref="ArpCache"/> mutation did to the table.</summary>
public enum ArpCacheChange
{
    /// <summary>Nothing changed (e.g. a dynamic learn that would have overwritten a static entry).</summary>
    None,

    /// <summary>A mapping for an IPv4 address that was not present has been added.</summary>
    Added,

    /// <summary>An existing mapping has been replaced or refreshed.</summary>
    Updated,
}

/// <summary>
/// The outcome of an <see cref="ArpCache"/> add/update: what changed, the resulting
/// <see cref="Entry"/>, and (for an update) the MAC that was there before -
/// <see cref="IsConflict"/> is true when that previous MAC differs from the new one.
/// </summary>
public readonly record struct ArpCacheUpdateResult(
    ArpCacheChange Change, ArpCacheEntry? Entry, MacAddress? PreviousHardwareAddress)
{
    public static ArpCacheUpdateResult None { get; } = new(ArpCacheChange.None, null, null);

    /// <summary>True when an existing mapping was replaced by a <em>different</em> MAC address.</summary>
    public bool IsConflict =>
        Change == ArpCacheChange.Updated
        && PreviousHardwareAddress is { } previous
        && Entry is { } entry
        && previous != entry.HardwareAddress;
}

/// <summary>
/// An interface's ARP table: the IPv4 -&gt; MAC mappings it has learned (or been configured with)
/// on its own Layer 2 segment. A plain data structure - no events, no threads, no protocol logic
/// (the <c>ArpProcessor</c> / <c>ArpLayer</c> own that). One cache per
/// <see cref="NetworkInterface"/>, so a multi-interface device never mixes mappings across
/// unrelated segments.
///
/// Expiration is lazy: every read (<see cref="TryGet"/> / <see cref="Contains"/> /
/// <see cref="Entries"/> / <see cref="Count"/>) drops entries whose lifetime has passed, using
/// the injected <see cref="TimeProvider"/> (real time in production, a fake clock in tests). No
/// background scheduler - the Phase 20 brief explicitly does not want one yet.
///
/// Conflict handling is deterministic: a fresh dynamic learn for an address already mapped
/// <em>replaces</em> the mapping (last writer wins) and reports
/// <see cref="ArpCacheUpdateResult.IsConflict"/> when the MAC actually changed. A dynamic learn
/// never overwrites a <see cref="ArpCacheEntryState.Static"/> entry.
/// </summary>
public sealed class ArpCache
{
    private readonly Dictionary<IPv4Address, ArpCacheEntry> _entries = [];
    private readonly TimeProvider _timeProvider;

    public ArpCache(TimeProvider? timeProvider = null, TimeSpan? defaultDynamicLifetime = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        DefaultDynamicLifetime = defaultDynamicLifetime ?? ArpCacheEntry.DefaultDynamicLifetime;

        if (DefaultDynamicLifetime <= TimeSpan.Zero)
        {
            throw new DomainException("The default dynamic ARP entry lifetime must be positive.");
        }
    }

    /// <summary>Lifetime applied to a dynamic entry by the parameterless <see cref="AddOrUpdateDynamic(IPv4Address, MacAddress)"/>.</summary>
    public TimeSpan DefaultDynamicLifetime { get; }

    private DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    /// <summary>Number of live (non-expired) entries. Prunes expired entries as a side effect.</summary>
    public int Count
    {
        get
        {
            PruneExpired();
            return _entries.Count;
        }
    }

    /// <summary>A snapshot of every live entry, in no particular order. Prunes expired entries as a side effect.</summary>
    public IReadOnlyCollection<ArpCacheEntry> Entries
    {
        get
        {
            PruneExpired();
            return _entries.Values.ToArray();
        }
    }

    /// <summary>True when a live mapping for <paramref name="protocolAddress"/> exists.</summary>
    public bool Contains(IPv4Address protocolAddress) => TryGet(protocolAddress, out _);

    /// <summary>
    /// Looks up the live entry for <paramref name="protocolAddress"/>. An entry found to be
    /// expired is removed and treated as absent.
    /// </summary>
    public bool TryGet(IPv4Address protocolAddress, [NotNullWhen(true)] out ArpCacheEntry? entry)
    {
        if (_entries.TryGetValue(protocolAddress, out var found))
        {
            if (!found.IsExpired(UtcNow))
            {
                entry = found;
                return true;
            }

            _entries.Remove(protocolAddress);
        }

        entry = null;
        return false;
    }

    /// <summary>Convenience over <see cref="TryGet"/> that yields just the MAC address.</summary>
    public bool TryResolve(IPv4Address protocolAddress, out MacAddress hardwareAddress)
    {
        if (TryGet(protocolAddress, out var entry))
        {
            hardwareAddress = entry.HardwareAddress;
            return true;
        }

        hardwareAddress = default;
        return false;
    }

    /// <summary>Adds or refreshes a dynamic mapping using <see cref="DefaultDynamicLifetime"/>.</summary>
    public ArpCacheUpdateResult AddOrUpdateDynamic(IPv4Address protocolAddress, MacAddress hardwareAddress) =>
        AddOrUpdateDynamic(protocolAddress, hardwareAddress, DefaultDynamicLifetime);

    /// <summary>
    /// Adds or refreshes a dynamic mapping with an explicit <paramref name="lifetime"/>. Returns
    /// <see cref="ArpCacheChange.None"/> (and changes nothing) when a live static entry already
    /// owns the address.
    /// </summary>
    public ArpCacheUpdateResult AddOrUpdateDynamic(IPv4Address protocolAddress, MacAddress hardwareAddress, TimeSpan lifetime)
    {
        var entry = ArpCacheEntry.Dynamic(protocolAddress, hardwareAddress, UtcNow, lifetime);
        return Upsert(protocolAddress, entry, isStaticWrite: false);
    }

    /// <summary>Adds or replaces a static (never-expiring) mapping. Always wins over an existing dynamic or static entry.</summary>
    public ArpCacheUpdateResult AddOrUpdateStatic(IPv4Address protocolAddress, MacAddress hardwareAddress)
    {
        var entry = ArpCacheEntry.Static(protocolAddress, hardwareAddress, UtcNow);
        return Upsert(protocolAddress, entry, isStaticWrite: true);
    }

    private ArpCacheUpdateResult Upsert(IPv4Address key, ArpCacheEntry newEntry, bool isStaticWrite)
    {
        if (_entries.TryGetValue(key, out var existing) && !existing.IsExpired(UtcNow))
        {
            if (existing.IsStatic && !isStaticWrite)
            {
                return ArpCacheUpdateResult.None;
            }

            _entries[key] = newEntry;
            return new ArpCacheUpdateResult(ArpCacheChange.Updated, newEntry, existing.HardwareAddress);
        }

        _entries[key] = newEntry;
        return new ArpCacheUpdateResult(ArpCacheChange.Added, newEntry, null);
    }

    /// <summary>Removes the mapping for <paramref name="protocolAddress"/>. Returns false when there was none.</summary>
    public bool Remove(IPv4Address protocolAddress) => _entries.Remove(protocolAddress);

    /// <summary>Empties the cache.</summary>
    public void Clear() => _entries.Clear();

    /// <summary>
    /// Drops every entry whose lifetime has passed and returns them (so a caller can raise an
    /// "entry expired" diagnostic). Called automatically by every read; exposed for an explicit sweep.
    /// </summary>
    public IReadOnlyList<ArpCacheEntry> PruneExpired()
    {
        var now = UtcNow;
        List<ArpCacheEntry>? removed = null;

        foreach (var pair in _entries.ToArray())
        {
            if (pair.Value.IsExpired(now))
            {
                _entries.Remove(pair.Key);
                (removed ??= []).Add(pair.Value);
            }
        }

        return removed ?? (IReadOnlyList<ArpCacheEntry>)Array.Empty<ArpCacheEntry>();
    }
}
