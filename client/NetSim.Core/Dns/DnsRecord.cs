using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Dns;

/// <summary>
/// A DNS resource record (brief section 8): a <see cref="Name"/>, a <see cref="Type"/>, a
/// <see cref="Class"/> (always <see cref="DnsClass.IN"/> in this simulator), a <see cref="Ttl"/>
/// and the type-specific data <see cref="DnsARecord"/>/<see cref="DnsAAAARecord"/>/
/// <see cref="DnsCnameRecord"/>/<see cref="DnsNsRecord"/>/<see cref="DnsMxRecord"/> carry. Modelled
/// as a small class hierarchy (one leaf per record type) rather than a single record stuffed with
/// every possible field, so each type only exposes the data that actually applies to it.
///
/// TTL (brief section 10) is a <see cref="TimeSpan"/> rather than an absolute expiry - the record
/// itself has no notion of "when" it was created (that belongs to <see cref="DnsCache"/>, which
/// combines a record's TTL with the moment it was cached). No simulated clock exists yet
/// (<see cref="Simulation.ISimulationEngine"/> is still a stub) - <see cref="DnsCache"/> uses the
/// same injectable <see cref="TimeProvider"/> abstraction <see cref="Networking.ArpCache"/> already
/// established, so this can plug into a real simulated clock later without changing this type.
/// </summary>
public abstract class DnsRecord : IEquatable<DnsRecord>
{
    protected DnsRecord(DomainName name, DnsRecordType type, TimeSpan ttl, DnsClass @class = DnsClass.IN)
    {
        if (ttl < TimeSpan.Zero)
        {
            throw new DomainException($"A DNS record's TTL cannot be negative (was {ttl}).");
        }

        Name = name;
        Type = type;
        Class = @class;
        Ttl = ttl;
    }

    /// <summary>The owner name this record answers for, e.g. <c>www.example.com</c>.</summary>
    public DomainName Name { get; }

    public DnsRecordType Type { get; }

    public DnsClass Class { get; }

    /// <summary>How long a resolver may cache this record.</summary>
    public TimeSpan Ttl { get; }

    /// <summary>A short human-readable rendering of this record's type-specific data, e.g. an A record's IPv4 address.</summary>
    public abstract string DataText { get; }

    public bool Equals(DnsRecord? other) =>
        other is not null
        && GetType() == other.GetType()
        && Name == other.Name
        && Class == other.Class
        && Ttl == other.Ttl
        && DataText == other.DataText;

    public override bool Equals(object? obj) => Equals(obj as DnsRecord);

    public override int GetHashCode() => HashCode.Combine(GetType(), Name, Class, Ttl, DataText);

    public override string ToString() => $"{Name} {(int)Ttl.TotalSeconds} {Class} {Type} {DataText}";
}
