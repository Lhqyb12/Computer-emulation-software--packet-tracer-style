using NetSim.Core.Dns;
using NetSim.Core.Networking;

namespace NetSim.Application.Dns;

/// <summary>
/// The DNS client/resolver (brief section 19): drives the full simulated resolution workflow (brief
/// section 21) from a device's interface - cache lookup, query, ARP + Ethernet + IPv4 + UDP
/// transport to the device's configured DNS server (<see cref="IDnsClientConfigurationStore"/>),
/// transaction-id verification, CNAME following, and caching the final answer - entirely inside the
/// simulator, exactly like <c>IPingService</c> drives ICMP.
/// </summary>
public interface IDnsResolver
{
    /// <summary>
    /// Resolves <paramref name="name"/> for <paramref name="type"/> using
    /// <paramref name="sourceInterface"/>'s device's configured DNS server(s). Never throws for an
    /// expected simulation failure (no configured server, unreachable server, NXDOMAIN, a CNAME
    /// loop, ...) - every such case is reported through the returned <see cref="DnsResolutionResult"/>.
    /// </summary>
    DnsResolutionResult Resolve(NetworkInterface sourceInterface, DomainName name, DnsRecordType type = DnsRecordType.A, bool useCache = true);

    event EventHandler<DnsEventArgs>? QueryCreated;

    event EventHandler<DnsEventArgs>? QuerySent;

    event EventHandler<DnsEventArgs>? ResponseReceived;

    event EventHandler<DnsEventArgs>? ResolutionStarted;

    event EventHandler<DnsEventArgs>? ResolutionSucceeded;

    event EventHandler<DnsEventArgs>? ResolutionFailed;

    event EventHandler<DnsEventArgs>? CacheHit;

    event EventHandler<DnsEventArgs>? CacheMiss;

    event EventHandler<DnsEventArgs>? CnameFollowed;

    event EventHandler<DnsEventArgs>? ServerUnavailable;

    event EventHandler<DnsEventArgs>? InvalidResponse;
}
