using NetSim.Core.Transport;

namespace NetSim.Core.Dhcp;

/// <summary>
/// Well-known constants for the simulated DHCP service (brief sections 3 &amp; 33). A single place
/// the DHCP ports/timers are defined rather than a literal scattered through the client/server -
/// exactly the "clean protocol constant abstraction" the DNS phase established with
/// <see cref="Dns.DnsProtocol"/>, and independent of it (DHCP defines its own 67/68 rather than
/// sharing DNS's 53).
/// </summary>
public static class DhcpProtocol
{
    /// <summary>The port a DHCP server listens on (67) - <c>bootps</c>.</summary>
    public static Port ServerPort { get; } = Port.Create(67);

    /// <summary>The port a DHCP client listens on (68) - <c>bootpc</c>.</summary>
    public static Port ClientPort { get; } = Port.Create(68);

    /// <summary>Hardware type for a 10 Mb Ethernet address in the BOOTP <c>htype</c> field (RFC 1700).</summary>
    public const byte EthernetHardwareType = 1;

    /// <summary>Length in bytes of an Ethernet client hardware address (<c>hlen</c>).</summary>
    public const byte EthernetHardwareAddressLength = 6;

    /// <summary>The lease duration a server hands out when its configuration does not specify one (1 hour, brief section 26).</summary>
    public static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromSeconds(3600);

    /// <summary>
    /// How long a pending <see cref="DhcpMessageType.Offer"/> stays reserved for a client before the
    /// server may hand the address to someone else (the client never sent a matching REQUEST). Keeps
    /// a crashed/never-returning client from permanently sterilising a pool address.
    /// </summary>
    public static readonly TimeSpan OfferReservationTimeout = TimeSpan.FromSeconds(120);

    /// <summary>T1 as a fraction of the lease time - when a bound client should start renewing (RFC 2131).</summary>
    public const double RenewalTimeFactor = 0.5;

    /// <summary>T2 as a fraction of the lease time - when a renewing client should start rebinding (RFC 2131).</summary>
    public const double RebindingTimeFactor = 0.875;
}
