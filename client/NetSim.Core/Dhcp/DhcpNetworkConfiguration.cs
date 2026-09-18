using NetSim.Core.Networking;

namespace NetSim.Core.Dhcp;

/// <summary>
/// The network configuration a DHCP client applies to its interface after an ACK (brief sections
/// 8, 28-30, 44). Built from the ACK's <c>yiaddr</c> plus its options, validated before it is
/// applied so the interface never lands in a half-configured state (brief section 44).
///
/// This is configuration <em>data</em> only: the gateway and DNS server are stored, not acted on -
/// routing is Phase 27 and the DNS resolver is the Phase 23 code, unchanged.
/// </summary>
public sealed record DhcpNetworkConfiguration
{
    public DhcpNetworkConfiguration(
        IPv4Address address,
        SubnetMask subnetMask,
        IPv4Address? gateway,
        IPv4Address? dnsServer,
        TimeSpan leaseDuration,
        IPv4Address serverIdentifier,
        DateTimeOffset leaseObtained,
        TimeSpan? renewalTime = null,
        TimeSpan? rebindingTime = null)
    {
        Address = address;
        SubnetMask = subnetMask;
        Gateway = gateway;
        DnsServer = dnsServer;
        LeaseDuration = leaseDuration;
        ServerIdentifier = serverIdentifier;
        LeaseObtained = leaseObtained;
        RenewalTime = renewalTime ?? leaseDuration * DhcpProtocol.RenewalTimeFactor;
        RebindingTime = rebindingTime ?? leaseDuration * DhcpProtocol.RebindingTimeFactor;
    }

    public IPv4Address Address { get; }

    public SubnetMask SubnetMask { get; }

    public int PrefixLength => SubnetMask.PrefixLength;

    public IPv4Address? Gateway { get; }

    public IPv4Address? DnsServer { get; }

    public TimeSpan LeaseDuration { get; }

    public IPv4Address ServerIdentifier { get; }

    public DateTimeOffset LeaseObtained { get; }

    /// <summary>T1 - when the client should begin renewing.</summary>
    public TimeSpan RenewalTime { get; }

    /// <summary>T2 - when the client should begin rebinding.</summary>
    public TimeSpan RebindingTime { get; }

    /// <summary>When the lease expires and the address must no longer be used (brief section 17).</summary>
    public DateTimeOffset LeaseExpiration => LeaseObtained + LeaseDuration;

    /// <summary>When the client should start trying to renew.</summary>
    public DateTimeOffset RenewAt => LeaseObtained + RenewalTime;

    public bool IsExpired(DateTimeOffset now) => now >= LeaseExpiration;

    /// <summary>True once the client has passed T1 and should attempt a renewal.</summary>
    public bool ShouldRenew(DateTimeOffset now) => now >= RenewAt;

    /// <summary>The equivalent interface IPv4 configuration - integrates with the existing Phase 18 model, no second address system.</summary>
    public Ipv4InterfaceConfiguration ToInterfaceConfiguration() =>
        Ipv4InterfaceConfiguration.Create(Address, SubnetMask.PrefixLength);

    /// <summary>Returns a copy with the lease clock restarted at <paramref name="now"/> for <paramref name="duration"/> (a successful renewal).</summary>
    public DhcpNetworkConfiguration Renewed(DateTimeOffset now, TimeSpan duration) =>
        new(Address, SubnetMask, Gateway, DnsServer, duration, ServerIdentifier, now);

    public override string ToString() =>
        $"{Address}/{PrefixLength}" +
        (Gateway is { } gw ? $" gw={gw}" : string.Empty) +
        (DnsServer is { } dns ? $" dns={dns}" : string.Empty) +
        $" lease={(int)LeaseDuration.TotalSeconds}s from {ServerIdentifier}";
}
