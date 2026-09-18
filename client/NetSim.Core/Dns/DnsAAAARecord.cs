using NetSim.Core.Networking;

namespace NetSim.Core.Dns;

/// <summary>Maps <see cref="DnsRecord.Name"/> to an IPv6 <see cref="Address"/> (brief section 9, "AAAA").</summary>
public sealed class DnsAAAARecord : DnsRecord
{
    public DnsAAAARecord(DomainName name, IPv6Address address, TimeSpan ttl)
        : base(name, DnsRecordType.AAAA, ttl)
    {
        Address = address;
    }

    public IPv6Address Address { get; }

    public override string DataText => Address.ToString();
}
