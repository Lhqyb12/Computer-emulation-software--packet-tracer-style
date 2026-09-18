namespace NetSim.Core.Dns;

/// <summary>
/// One cached answer set in a <see cref="DnsCache"/>: the records returned for a given
/// (name, type), when they were cached, and when they stop being valid. Immutable - a refreshed
/// answer replaces the entry rather than mutating it. Mirrors <see cref="Networking.ArpCacheEntry"/>'s
/// "expiration is data, not a scheduler" design (brief section 23).
/// </summary>
public sealed class DnsCacheEntry
{
    internal DnsCacheEntry(DomainName name, DnsRecordType type, IReadOnlyList<DnsRecord> records, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    {
        Name = name;
        Type = type;
        Records = records;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public DomainName Name { get; }

    public DnsRecordType Type { get; }

    public IReadOnlyList<DnsRecord> Records { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public bool IsExpired(DateTimeOffset asOfUtc) => asOfUtc >= ExpiresAtUtc;
}
