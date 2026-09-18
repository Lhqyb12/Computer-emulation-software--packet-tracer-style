using NetSim.Core.Transport;

namespace NetSim.Core.Dns;

/// <summary>
/// Well-known constants for the simulated DNS service (brief section 37) - a single place a DNS
/// port/limit is defined rather than a literal scattered through the resolver/server, exactly like
/// the brief asks ("do not hard-code application services... create a clean DNS service/constant
/// abstraction"). A future protocol (DHCP in Phase 24) defines its own such constants independently.
/// </summary>
public static class DnsProtocol
{
    /// <summary>The standard DNS service port (53), used for both UDP and TCP.</summary>
    public static Port Port { get; } = Port.Create(53);

    /// <summary>
    /// The maximum number of CNAME hops either the server (chasing its own zone data) or the
    /// resolver (chasing a partial response across several round trips) will follow before giving
    /// up - protects against a CNAME cycle causing infinite recursion (brief section 24).
    /// </summary>
    public const int MaxCnameChainDepth = 8;

    /// <summary>A reasonable default TTL (5 minutes) for records that do not specify their own.</summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(300);
}
