namespace NetSim.Core.Dns;

/// <summary>
/// The DNS header's RCODE field (RFC 1035 section 4.1.1, brief section 15) - how a resolver learns
/// whether a query succeeded and, if not, why.
/// </summary>
public enum DnsResponseCode
{
    /// <summary>The query completed successfully (there may still be zero answers - see the "NODATA" case in <see cref="DnsServer"/>).</summary>
    NoError = 0,

    /// <summary>The query message itself was malformed.</summary>
    FormatError = 1,

    /// <summary>The server encountered an internal problem answering the query.</summary>
    ServerFailure = 2,

    /// <summary>The queried domain name does not exist ("NXDOMAIN").</summary>
    NameError = 3,

    /// <summary>The server does not support the requested kind of query.</summary>
    NotImplemented = 4,

    /// <summary>The server refuses to answer this query for policy reasons (e.g. it is not authoritative for the name).</summary>
    Refused = 5,
}
