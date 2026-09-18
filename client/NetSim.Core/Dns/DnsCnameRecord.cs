namespace NetSim.Core.Dns;

/// <summary>An alias: <see cref="DnsRecord.Name"/> is canonically known as <see cref="Target"/> (brief section 9, "CNAME").</summary>
public sealed class DnsCnameRecord : DnsRecord
{
    public DnsCnameRecord(DomainName name, DomainName target, TimeSpan ttl)
        : base(name, DnsRecordType.CNAME, ttl)
    {
        Target = target;
    }

    public DomainName Target { get; }

    public override string DataText => Target.ToString();
}
