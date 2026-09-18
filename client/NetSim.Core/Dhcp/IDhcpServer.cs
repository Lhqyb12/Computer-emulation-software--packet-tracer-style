using NetSim.Core.Networking;

namespace NetSim.Core.Dhcp;

/// <summary>
/// The transport-agnostic DHCP server protocol logic (brief sections 6-8, 10, 20, 31) - exactly
/// like <see cref="Dns.IDnsServer"/> answers a query without knowing how it was transmitted.
/// <see cref="HandleMessage"/> takes a decoded <see cref="DhcpMessage"/> and returns the reply to
/// send back (an OFFER, ACK or NAK), or null when the message needs no reply (RELEASE, DECLINE, a
/// REQUEST that selected a different server, or a message this server should ignore).
///
/// It owns its <see cref="Leases"/> (an <see cref="IDhcpLeaseManager"/> built from
/// <see cref="Configuration"/>) - it is a per-device domain object an application workflow creates
/// directly, not a DI singleton, mirroring <c>new DnsServer(origin)</c>.
/// </summary>
public interface IDhcpServer
{
    /// <summary>The settings this server runs with (brief section 26).</summary>
    DhcpServerConfiguration Configuration { get; }

    /// <summary>The server's own address / DHCP server identifier (option 54).</summary>
    IPv4Address ServerAddress { get; }

    /// <summary>The lease table this server allocates from (brief section 10).</summary>
    IDhcpLeaseManager Leases { get; }

    /// <summary>
    /// Processes an inbound client message and returns the reply to transmit, or null for no reply.
    /// Never throws for a malformed or unexpected message (brief section 67) - it is ignored.
    /// </summary>
    DhcpMessage? HandleMessage(DhcpMessage request);

    /// <summary>Expires any lease whose time has run out (brief section 17), raising <see cref="LeaseExpired"/> for each. Lazy - there is no timer.</summary>
    IReadOnlyList<DhcpLease> PruneExpiredLeases();

    event EventHandler<DhcpEventArgs>? DiscoverReceived;

    event EventHandler<DhcpEventArgs>? OfferCreated;

    event EventHandler<DhcpEventArgs>? RequestReceived;

    event EventHandler<DhcpEventArgs>? AckCreated;

    event EventHandler<DhcpEventArgs>? NakCreated;

    event EventHandler<DhcpEventArgs>? LeaseAllocated;

    event EventHandler<DhcpEventArgs>? LeaseReleased;

    event EventHandler<DhcpEventArgs>? LeaseExpired;

    event EventHandler<DhcpEventArgs>? AddressDeclined;

    event EventHandler<DhcpEventArgs>? PoolExhausted;
}
