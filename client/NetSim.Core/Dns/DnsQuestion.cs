namespace NetSim.Core.Dns;

/// <summary>One entry in a <see cref="DnsMessage"/>'s Question section: what is being asked - a name, a record type and a class.</summary>
public readonly record struct DnsQuestion(DomainName Name, DnsRecordType Type, DnsClass Class)
{
    public DnsQuestion(DomainName name, DnsRecordType type)
        : this(name, type, DnsClass.IN)
    {
    }

    public override string ToString() => $"{Name} {Class} {Type}";
}
