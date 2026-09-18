using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Devices;

namespace NetSim.Core.Dns;

/// <summary>
/// A resolver's DNS cache (brief section 22), scoped per <see cref="NetworkDevice"/> - like
/// <see cref="Udp.IUdpDeliveryManager"/>, host-scoped rather than a field on the device itself, so
/// two devices resolving the same name never share (or fight over) one cache entry.
/// </summary>
public interface IDnsCache
{
    /// <summary>Looks up a live (non-expired) answer set for (<paramref name="name"/>, <paramref name="type"/>). An expired entry is treated as absent and dropped.</summary>
    bool TryGet(NetworkDevice device, DomainName name, DnsRecordType type, [NotNullWhen(true)] out IReadOnlyList<DnsRecord>? records);

    /// <summary>
    /// Caches <paramref name="records"/> for (<paramref name="name"/>, <paramref name="type"/>),
    /// expiring after the smallest TTL among them. A record set with a zero TTL, or an empty set, is
    /// not cached.
    /// </summary>
    void Put(NetworkDevice device, DomainName name, DnsRecordType type, IReadOnlyList<DnsRecord> records);

    bool Remove(NetworkDevice device, DomainName name, DnsRecordType type);

    void Clear(NetworkDevice device);

    /// <summary>Drops every expired entry for <paramref name="device"/> and returns them (so a caller can raise a "record expired" diagnostic).</summary>
    IReadOnlyList<DnsCacheEntry> PruneExpired(NetworkDevice device);
}
