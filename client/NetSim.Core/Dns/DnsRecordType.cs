namespace NetSim.Core.Dns;

/// <summary>
/// The DNS resource record types this simulator supports (brief section 9) - the real IANA
/// numbers are used purely for familiarity, not because any wire-format decoder needs them.
/// </summary>
public enum DnsRecordType
{
    /// <summary>Maps a name to an IPv4 address.</summary>
    A = 1,

    /// <summary>Delegates a name to an authoritative name server.</summary>
    NS = 2,

    /// <summary>An alias from one name to another (canonical) name.</summary>
    CNAME = 5,

    /// <summary>A mail exchange for the domain.</summary>
    MX = 15,

    /// <summary>Maps a name to an IPv6 address.</summary>
    AAAA = 28,
}
