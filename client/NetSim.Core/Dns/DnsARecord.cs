using NetSim.Core.Networking;

namespace NetSim.Core.Dns;

/// <summary>Maps <see cref="DnsRecord.Name"/> to an IPv4 <see cref="Address"/> (brief section 9, "A").</summary>
public sealed class DnsARecord : DnsRecord
{
    public DnsARecord(DomainName name, IPv4Address address, TimeSpan ttl)
        : base(name, DnsRecordType.A, ttl)
    {
        Address = address;
    }

    public IPv4Address Address { get; }

    public override string DataText => Address.ToString();
}
