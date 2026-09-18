using NetSim.Core.Networking;

namespace NetSim.Core.Dns;

/// <summary>
/// A simulated authoritative DNS server (brief section 16): transport-agnostic protocol logic that
/// turns an incoming <see cref="DnsMessage"/> query into a response by consulting its
/// <see cref="Zone"/>. Deliberately has no network I/O of its own - exactly like
/// <see cref="Icmp.IIcmpLayer"/> builds an Echo Reply without knowing how it gets transmitted, this
/// only builds the <see cref="DnsMessage"/> response; the application-layer orchestrator
/// (<c>Application.Dns.IDnsResolver</c>) drives the actual UDP/TCP/IPv4/Ethernet round trip and
/// hands the received query here.
/// </summary>
public interface IDnsServer
{
    /// <summary>The single zone this server answers from (brief section 18 - no multi-zone delegation in this phase).</summary>
    DnsZone Zone { get; }

    /// <summary>Convenience over <see cref="Zone"/>'s record store.</summary>
    IDnsRecordStore Records { get; }

    /// <summary>Adds a record to <see cref="Zone"/> and raises <see cref="RecordAdded"/>.</summary>
    void AddRecord(DnsRecord record);

    /// <summary>Removes a record from <see cref="Zone"/> and raises <see cref="RecordRemoved"/> if it was present.</summary>
    bool RemoveRecord(DnsRecord record);

    /// <summary>
    /// Builds the response to <paramref name="query"/> (from <paramref name="querierAddress"/>, used
    /// only for diagnostics/events): resolves the question against <see cref="Zone"/>, following any
    /// CNAME chain up to <see cref="DnsProtocol.MaxCnameChainDepth"/> hops, and returns NOERROR (with
    /// the matching records), NOERROR with an empty answer section ("NODATA" - the name exists but
    /// not for this type), or NXDOMAIN. Never throws for a malformed or unanswerable query - an
    /// invalid question count/class yields FORMERR (brief section 30).
    /// </summary>
    DnsMessage HandleQuery(DnsMessage query, IPv4Address querierAddress);

    event EventHandler<DnsEventArgs>? QueryReceived;

    event EventHandler<DnsEventArgs>? ResponseCreated;

    event EventHandler<DnsEventArgs>? RecordAdded;

    event EventHandler<DnsEventArgs>? RecordRemoved;

    /// <summary>Raised whenever this server answers a question with NXDOMAIN.</summary>
    event EventHandler<DnsEventArgs>? NxDomain;
}
