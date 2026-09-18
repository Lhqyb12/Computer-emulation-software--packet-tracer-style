using NetSim.Core.Networking;

namespace NetSim.Core.Dhcp;

/// <summary>
/// A single address binding tracked by a DHCP server's <see cref="DhcpLeaseManager"/> (brief
/// section 12): which client holds which address, from when, for how long, from which server, and
/// in what <see cref="DhcpLeaseState"/>. Immutable - state changes and renewals produce a new
/// instance, mirroring how <see cref="Networking.Ipv4InterfaceConfiguration"/> and
/// <see cref="Dns.DnsCacheEntry"/> are treated in this codebase.
/// </summary>
public sealed class DhcpLease
{
    public DhcpLease(
        DhcpClientId clientId,
        IPv4Address address,
        DateTimeOffset start,
        TimeSpan duration,
        IPv4Address serverIdentifier,
        DhcpLeaseState state)
    {
        ClientId = clientId;
        Address = address;
        Start = start;
        Duration = duration;
        ServerIdentifier = serverIdentifier;
        State = state;
    }

    public DhcpClientId ClientId { get; }

    public IPv4Address Address { get; }

    public DateTimeOffset Start { get; }

    public TimeSpan Duration { get; }

    public IPv4Address ServerIdentifier { get; }

    public DhcpLeaseState State { get; }

    /// <summary>When this lease ends. For an <see cref="DhcpLeaseState.Offered"/> lease this is the offer-reservation deadline.</summary>
    public DateTimeOffset Expiration => Start + Duration;

    /// <summary>How long until the lease expires, clamped at zero.</summary>
    public TimeSpan RemainingAt(DateTimeOffset now) => Expiration > now ? Expiration - now : TimeSpan.Zero;

    /// <summary>True when an <see cref="DhcpLeaseState.Active"/> lease has run out (brief section 17).</summary>
    public bool IsExpired(DateTimeOffset now) => State == DhcpLeaseState.Active && now >= Expiration;

    /// <summary>True when a pending <see cref="DhcpLeaseState.Offered"/> reservation has gone stale (the client never REQUESTed it).</summary>
    public bool IsStaleOffer(DateTimeOffset now) => State == DhcpLeaseState.Offered && now >= Expiration;

    /// <summary>Whether this lease currently occupies its address (so another client cannot be given it).</summary>
    public bool OccupiesAddress => State is DhcpLeaseState.Offered or DhcpLeaseState.Active or DhcpLeaseState.Declined;

    public DhcpLease WithState(DhcpLeaseState state) => new(ClientId, Address, Start, Duration, ServerIdentifier, state);

    /// <summary>Returns an <see cref="DhcpLeaseState.Active"/> copy with the clock restarted at <paramref name="now"/> for <paramref name="duration"/>.</summary>
    public DhcpLease RenewedAt(DateTimeOffset now, TimeSpan duration) =>
        new(ClientId, Address, now, duration, ServerIdentifier, DhcpLeaseState.Active);

    public override string ToString() => $"{Address} -> {ClientId} [{State}] until {Expiration:u}";
}
