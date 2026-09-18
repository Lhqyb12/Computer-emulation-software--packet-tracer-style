namespace NetSim.Core.Dns;

/// <summary>
/// A DNS zone's record repository (brief section 17): add/remove/find records, keyed by
/// (name, type) so the common "find the A records for this name" lookup an incoming query needs is
/// a dictionary hit rather than a scan of every record (brief section 56).
/// </summary>
public interface IDnsRecordStore
{
    /// <summary>Adds <paramref name="record"/>. Throws <see cref="Common.Exceptions.DomainException"/> for an exact duplicate (same name, type, class, TTL and data).</summary>
    void AddRecord(DnsRecord record);

    /// <summary>Removes the exact <paramref name="record"/>. Returns false when no matching record was stored.</summary>
    bool RemoveRecord(DnsRecord record);

    /// <summary>Removes every record for <paramref name="name"/>, optionally restricted to <paramref name="type"/>. Returns how many were removed.</summary>
    int RemoveRecords(DomainName name, DnsRecordType? type = null);

    /// <summary>Every record for <paramref name="name"/> of exactly <paramref name="type"/> - the primary query-time lookup. Empty (never null) when there are none.</summary>
    IReadOnlyList<DnsRecord> FindRecords(DomainName name, DnsRecordType type);

    /// <summary>Every record for <paramref name="name"/>, of any type.</summary>
    IReadOnlyList<DnsRecord> FindByName(DomainName name);

    /// <summary>True when at least one record (of any type) exists for <paramref name="name"/> - distinguishes NXDOMAIN from "name exists but not this type" (NODATA).</summary>
    bool ContainsName(DomainName name);

    /// <summary>Removes every record.</summary>
    void Clear();

    /// <summary>Every record currently stored, in no particular order - diagnostics/UI use.</summary>
    IReadOnlyCollection<DnsRecord> AllRecords { get; }
}
