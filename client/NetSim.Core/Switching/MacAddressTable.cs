using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;
using NetSim.Core.Vlans;

namespace NetSim.Core.Switching;

/// <summary>
/// Default <see cref="IMacAddressTable"/>: a <see cref="Dictionary{TKey,TValue}"/> keyed by
/// <c>(VLAN, MAC)</c>, guarded by a lock so MAC learning, aging, port-change pruning and the UI
/// reading a snapshot can happen from different threads once the simulation becomes asynchronous.
/// A plain data structure - no events, no clock, no forwarding logic (the
/// <see cref="SwitchingEngine"/> owns those), mirroring how <see cref="ArpCache"/> relates to the
/// ARP engine.
///
/// The VLAN in the key is what keeps two VLANs isolated: a lookup for a MAC in VLAN 10 can never
/// return a port that MAC was learned on in VLAN 20.
/// </summary>
public sealed class MacAddressTable : IMacAddressTable
{
    /// <summary>The classic Cisco Catalyst dynamic MAC aging default (300 seconds / 5 minutes).</summary>
    public static readonly TimeSpan DefaultAgingTime = TimeSpan.FromSeconds(300);

    private readonly object _gate = new();
    private readonly Dictionary<(VlanId Vlan, MacAddress Mac), MacAddressTableEntry> _entries = [];
    private TimeSpan _agingTime = DefaultAgingTime;

    public TimeSpan AgingTime
    {
        get
        {
            lock (_gate)
            {
                return _agingTime;
            }
        }

        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new DomainException("The MAC address table aging time must be positive.");
            }

            lock (_gate)
            {
                _agingTime = value;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public MacLearnResult Learn(MacAddress mac, NetworkInterface port, TimeSpan now) =>
        Learn(VlanId.Default, mac, port, now);

    public MacLearnResult Learn(VlanId vlan, MacAddress mac, NetworkInterface port, TimeSpan now)
    {
        ArgumentNullException.ThrowIfNull(port);

        // A switch only ever learns from a real host's unicast source address. Broadcast /
        // multicast / all-zero sources are never learned (and a valid EthernetFrame cannot carry
        // one as its source anyway - this is defence in depth).
        if (!mac.IsUnicast || mac.IsUnspecified)
        {
            return MacLearnResult.Unchanged;
        }

        var key = (vlan, mac);
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                if (existing.IsStatic)
                {
                    // A dynamic learn never displaces an operator-configured entry.
                    return MacLearnResult.Unchanged;
                }

                if (ReferenceEquals(existing.Port, port))
                {
                    var refreshed = new MacAddressTableEntry(
                        vlan, mac, port, MacTableEntryType.Dynamic, existing.LearnedAt, now);
                    _entries[key] = refreshed;
                    return new MacLearnResult(MacLearnOutcome.Refreshed, refreshed, null);
                }

                var moved = new MacAddressTableEntry(vlan, mac, port, MacTableEntryType.Dynamic, now, now);
                _entries[key] = moved;
                return new MacLearnResult(MacLearnOutcome.Moved, moved, existing.Port);
            }

            var added = new MacAddressTableEntry(vlan, mac, port, MacTableEntryType.Dynamic, now, now);
            _entries[key] = added;
            return new MacLearnResult(MacLearnOutcome.Added, added, null);
        }
    }

    public MacAddressTableEntry? Lookup(MacAddress mac) => Lookup(VlanId.Default, mac);

    public MacAddressTableEntry? Lookup(VlanId vlan, MacAddress mac)
    {
        lock (_gate)
        {
            return _entries.GetValueOrDefault((vlan, mac));
        }
    }

    public bool TryGetPort(MacAddress mac, [NotNullWhen(true)] out NetworkInterface? port) =>
        TryGetPort(VlanId.Default, mac, out port);

    public bool TryGetPort(VlanId vlan, MacAddress mac, [NotNullWhen(true)] out NetworkInterface? port)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue((vlan, mac), out var entry))
            {
                port = entry.Port;
                return true;
            }
        }

        port = null;
        return false;
    }

    public bool Contains(MacAddress mac) => Contains(VlanId.Default, mac);

    public bool Contains(VlanId vlan, MacAddress mac)
    {
        lock (_gate)
        {
            return _entries.ContainsKey((vlan, mac));
        }
    }

    public bool Remove(MacAddress mac) => Remove(VlanId.Default, mac);

    public bool Remove(VlanId vlan, MacAddress mac)
    {
        lock (_gate)
        {
            return _entries.Remove((vlan, mac));
        }
    }

    public IReadOnlyList<MacAddressTableEntry> RemoveEntriesForPort(EntityId portId)
    {
        lock (_gate)
        {
            var removed = _entries.Values.Where(e => e.Port.Id == portId).ToList();
            foreach (var entry in removed)
            {
                _entries.Remove((entry.Vlan, entry.MacAddress));
            }

            return removed;
        }
    }

    public IReadOnlyList<MacAddressTableEntry> RemoveEntriesForVlanOnPort(VlanId vlan, EntityId portId)
    {
        lock (_gate)
        {
            var removed = _entries.Values.Where(e => e.Vlan == vlan && e.Port.Id == portId).ToList();
            foreach (var entry in removed)
            {
                _entries.Remove((entry.Vlan, entry.MacAddress));
            }

            return removed;
        }
    }

    public IReadOnlyList<MacAddressTableEntry> RemoveEntriesForVlan(VlanId vlan)
    {
        lock (_gate)
        {
            var removed = _entries.Values.Where(e => e.Vlan == vlan).ToList();
            foreach (var entry in removed)
            {
                _entries.Remove((entry.Vlan, entry.MacAddress));
            }

            return removed;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }

    public IReadOnlyList<MacAddressTableEntry> AgeEntries(TimeSpan now)
    {
        lock (_gate)
        {
            var expired = _entries.Values.Where(e => e.IsExpired(now, _agingTime)).ToList();
            foreach (var entry in expired)
            {
                _entries.Remove((entry.Vlan, entry.MacAddress));
            }

            return expired;
        }
    }

    public IReadOnlyList<MacAddressTableEntry> GetEntries()
    {
        lock (_gate)
        {
            return _entries.Values.OrderByDescending(e => e.LearnedAt).ToList();
        }
    }

    public IReadOnlyList<MacAddressTableEntry> GetEntriesForPort(EntityId portId)
    {
        lock (_gate)
        {
            return _entries.Values.Where(e => e.Port.Id == portId).ToList();
        }
    }
}
