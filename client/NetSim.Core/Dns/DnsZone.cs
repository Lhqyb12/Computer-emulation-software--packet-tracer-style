namespace NetSim.Core.Dns;

/// <summary>
/// A simplified DNS zone (brief section 18): an <see cref="Origin"/> domain plus the
/// <see cref="Records"/> a <see cref="DnsServer"/> hosts for it. Deliberately does not implement
/// zone-file parsing, delegation to child zones, or multi-zone authority checks - a
/// <see cref="DnsServer"/> answers from a single zone's <see cref="Records"/> directly; this type
/// exists purely to give that record set the "zone" identity/label the brief's vocabulary expects
/// (e.g. for a future UI showing "Zone: example.com").
/// </summary>
public sealed class DnsZone
{
    public DnsZone(DomainName origin, IDnsRecordStore? records = null)
    {
        Origin = origin;
        Records = records ?? new DnsRecordStore();
    }

    /// <summary>The zone's apex domain, e.g. <c>example.com</c>.</summary>
    public DomainName Origin { get; }

    public IDnsRecordStore Records { get; }
}
