using NetSim.Core.Networking;

namespace NetSim.Core.Dhcp;

/// <summary>Why a <see cref="IDhcpLeaseManager.Commit"/> could not bind the requested address (drives the NAK reason, brief section 20).</summary>
public enum DhcpCommitOutcome
{
    /// <summary>The address is now an active lease for the client.</summary>
    Committed,

    /// <summary>The requested address is outside the configured pool and is not reserved for this client.</summary>
    OutOfRange,

    /// <summary>The requested address is an infrastructure / excluded / declined address, or reserved for a different client.</summary>
    Unavailable,

    /// <summary>The requested address is an active lease held by a different client.</summary>
    HeldByAnotherClient,
}

/// <summary>The result of a <see cref="IDhcpLeaseManager.Commit"/> attempt.</summary>
public sealed record DhcpCommitResult(DhcpCommitOutcome Outcome, DhcpLease? Lease)
{
    public bool IsCommitted => Outcome == DhcpCommitOutcome.Committed && Lease is not null;
}

/// <summary>
/// The address-allocation core of a DHCP server (brief sections 10, 13-15, 17): given a
/// <see cref="DhcpAddressPool"/> and a clock, it decides which address a client is offered and
/// committed, keeps at most one active/offered lease per client, never hands the same active
/// address to two clients, extends a lease on renewal, and returns addresses to the pool on
/// release/expiry. A plain data structure like <see cref="Udp.IUdpDeliveryManager"/> - no events,
/// no transport, no protocol messages.
/// </summary>
public interface IDhcpLeaseManager
{
    /// <summary>The address every lease this manager grants is stamped with (option 54).</summary>
    IPv4Address ServerIdentifier { get; }

    /// <summary>
    /// Reserves an address for <paramref name="clientId"/> and returns the pending
    /// <see cref="DhcpLeaseState.Offered"/> lease - preferring the client's reservation, then its
    /// current address, then <paramref name="requestedAddress"/>, then the lowest free address.
    /// Returns null when the pool has nothing left (brief section 43).
    /// </summary>
    DhcpLease? Offer(DhcpClientId clientId, IPv4Address? requestedAddress);

    /// <summary>
    /// Commits <paramref name="requestedAddress"/> to <paramref name="clientId"/> as an
    /// <see cref="DhcpLeaseState.Active"/> lease (also the renewal path - an existing holder simply
    /// has its clock restarted). The <see cref="DhcpCommitResult.Outcome"/> says why it failed.
    /// </summary>
    DhcpCommitResult Commit(DhcpClientId clientId, IPv4Address requestedAddress);

    /// <summary>Drops a pending <see cref="DhcpLeaseState.Offered"/> lease for a client whose REQUEST selected a different server (brief section 36). No effect on an active lease.</summary>
    bool CancelOffer(DhcpClientId clientId);

    /// <summary>Releases the client's lease and returns its address to the pool (brief section 18). False when the client held nothing.</summary>
    bool Release(DhcpClientId clientId);

    /// <summary>Marks <paramref name="address"/> as <see cref="DhcpLeaseState.Declined"/> so it is held out of the pool (brief section 19).</summary>
    bool Decline(DhcpClientId clientId, IPv4Address address);

    /// <summary>The client's current lease (offered, active or expired), or null.</summary>
    DhcpLease? GetLease(DhcpClientId clientId);

    /// <summary>Every lease the manager is tracking (active, offered, expired, declined).</summary>
    IReadOnlyCollection<DhcpLease> Leases { get; }

    /// <summary>The currently active leases only.</summary>
    IReadOnlyCollection<DhcpLease> ActiveLeases { get; }

    /// <summary>
    /// Moves any active lease whose time has run out to <see cref="DhcpLeaseState.Expired"/>,
    /// returning its address to the pool, and discards stale offers. Returns the leases that just
    /// expired (brief sections 14 &amp; 17). Lazy, like <see cref="Dns.DnsCache"/> - there is no timer.
    /// </summary>
    IReadOnlyList<DhcpLease> PruneExpired();

    /// <summary>How many pool addresses are free to allocate right now.</summary>
    int AvailableAddressCount { get; }

    /// <summary>True when <paramref name="address"/> is currently offered, leased or declined.</summary>
    bool IsAddressInUse(IPv4Address address);
}
