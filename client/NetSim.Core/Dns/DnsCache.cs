using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Common;
using NetSim.Core.Devices;

namespace NetSim.Core.Dns;

/// <summary>Default <see cref="IDnsCache"/>. Expiration is lazy - the same "every read prunes" design as <see cref="Networking.ArpCache"/>.</summary>
public sealed class DnsCache : IDnsCache
{
    private readonly Dictionary<(EntityId DeviceId, DomainName Name, DnsRecordType Type), DnsCacheEntry> _entries = [];
    private readonly TimeProvider _timeProvider;

    public DnsCache(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    private DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public bool TryGet(NetworkDevice device, DomainName name, DnsRecordType type, [NotNullWhen(true)] out IReadOnlyList<DnsRecord>? records)
    {
        ArgumentNullException.ThrowIfNull(device);

        var key = (device.Id, name, type);
        if (_entries.TryGetValue(key, out var entry))
        {
            if (!entry.IsExpired(UtcNow))
            {
                records = entry.Records;
                return true;
            }

            _entries.Remove(key);
        }

        records = null;
        return false;
    }

    public void Put(NetworkDevice device, DomainName name, DnsRecordType type, IReadOnlyList<DnsRecord> records)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
        {
            return;
        }

        var ttl = records.Min(r => r.Ttl);
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        var now = UtcNow;
        _entries[(device.Id, name, type)] = new DnsCacheEntry(name, type, records, now, now + ttl);
    }

    public bool Remove(NetworkDevice device, DomainName name, DnsRecordType type)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _entries.Remove((device.Id, name, type));
    }

    public void Clear(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        foreach (var key in _entries.Keys.Where(k => k.DeviceId == device.Id).ToList())
        {
            _entries.Remove(key);
        }
    }

    public IReadOnlyList<DnsCacheEntry> PruneExpired(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var now = UtcNow;
        List<DnsCacheEntry>? removed = null;
        foreach (var pair in _entries.Where(p => p.Key.DeviceId == device.Id).ToArray())
        {
            if (pair.Value.IsExpired(now))
            {
                _entries.Remove(pair.Key);
                (removed ??= []).Add(pair.Value);
            }
        }

        return removed ?? (IReadOnlyList<DnsCacheEntry>)Array.Empty<DnsCacheEntry>();
    }
}
