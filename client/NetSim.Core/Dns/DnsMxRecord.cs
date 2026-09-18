namespace NetSim.Core.Dns;

/// <summary>
/// A mail exchange for <see cref="DnsRecord.Name"/> (brief section 9, "MX"): the mail server
/// <see cref="Exchange"/> and its <see cref="Preference"/> (lower values are preferred). DNS-only -
/// no SMTP is implemented or implied.
/// </summary>
public sealed class DnsMxRecord : DnsRecord
{
    public DnsMxRecord(DomainName name, ushort preference, DomainName exchange, TimeSpan ttl)
        : base(name, DnsRecordType.MX, ttl)
    {
        Preference = preference;
        Exchange = exchange;
    }

    public ushort Preference { get; }

    public DomainName Exchange { get; }

    public override string DataText => $"{Preference} {Exchange}";
}
