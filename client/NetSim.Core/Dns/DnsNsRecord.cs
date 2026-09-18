namespace NetSim.Core.Dns;

/// <summary>Declares <see cref="NameServer"/> as an authoritative name server for <see cref="DnsRecord.Name"/> (brief section 9, "NS").</summary>
public sealed class DnsNsRecord : DnsRecord
{
    public DnsNsRecord(DomainName name, DomainName nameServer, TimeSpan ttl)
        : base(name, DnsRecordType.NS, ttl)
    {
        NameServer = nameServer;
    }

    public DomainName NameServer { get; }

    public override string DataText => NameServer.ToString();
}
