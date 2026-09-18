using NetSim.Core.Networking;

namespace NetSim.Core.Dhcp;

/// <summary>Default <see cref="IDhcpLeaseManager"/>. See the interface for the design; expiration is lazy, exactly like <see cref="Dns.DnsCache"/>.</summary>
public sealed class DhcpLeaseManager : IDhcpLeaseManager
{
    private readonly DhcpAddressPool _pool;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _leaseDuration;

    // At most one offered/active/expired lease per client. Declined addresses live in _declined
    // (they are a property of the address, not of any one client).
    private readonly Dictionary<DhcpClientId, DhcpLease> _leasesByClient = [];
    private readonly Dictionary<IPv4Address, DhcpClientId> _occupantByAddress = [];
    private readonly Dictionary<IPv4Address, DhcpLease> _declined = [];

    public DhcpLeaseManager(DhcpAddressPool pool, IPv4Address serverIdentifier, TimeSpan leaseDuration, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(pool);
        _pool = pool;
        ServerIdentifier = serverIdentifier;
        _leaseDuration = leaseDuration <= TimeSpan.Zero ? DhcpProtocol.DefaultLeaseDuration : leaseDuration;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IPv4Address ServerIdentifier { get; }

    private DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public IReadOnlyCollection<DhcpLease> Leases => _leasesByClient.Values.Concat(_declined.Values).ToList().AsReadOnly();

    public IReadOnlyCollection<DhcpLease> ActiveLeases =>
        _leasesByClient.Values.Where(l => l.State == DhcpLeaseState.Active).ToList().AsReadOnly();

    public DhcpLease? GetLease(DhcpClientId clientId) =>
        _leasesByClient.TryGetValue(clientId, out var lease) ? lease : null;

    public bool IsAddressInUse(IPv4Address address) =>
        _occupantByAddress.ContainsKey(address) || _declined.ContainsKey(address);

    public int AvailableAddressCount
    {
        get
        {
            PruneExpired();
            var probe = DhcpClientId.Create(0, MacAddress.Zero);
            return _pool.AssignableAddresses(probe).Count(a => !IsAddressInUse(a));
        }
    }

    public DhcpLease? Offer(DhcpClientId clientId, IPv4Address? requestedAddress)
    {
        PruneExpired();

        var address = ChooseAddress(clientId, requestedAddress);
        if (address is null)
        {
            return null;
        }

        // A short reservation window, not the full lease - a client that never REQUESTs must not
        // sterilise a pool address permanently.
        return Record(new DhcpLease(clientId, address.Value, UtcNow, DhcpProtocol.OfferReservationTimeout, ServerIdentifier, DhcpLeaseState.Offered));
    }

    public DhcpCommitResult Commit(DhcpClientId clientId, IPv4Address requestedAddress)
    {
        PruneExpired();

        var reservedForClient = _pool.ReservationFor(clientId) == requestedAddress;

        if (!_pool.IsInRange(requestedAddress) && !reservedForClient)
        {
            return new DhcpCommitResult(DhcpCommitOutcome.OutOfRange, null);
        }

        if (_declined.ContainsKey(requestedAddress)
            || _pool.IsReservedInfrastructure(requestedAddress)
            || _pool.IsExcluded(requestedAddress)
            || _pool.IsReservedForAnother(requestedAddress, clientId))
        {
            return new DhcpCommitResult(DhcpCommitOutcome.Unavailable, null);
        }

        if (_occupantByAddress.TryGetValue(requestedAddress, out var occupant) && occupant != clientId)
        {
            var other = GetLease(occupant);
            if (other is { State: DhcpLeaseState.Active })
            {
                return new DhcpCommitResult(DhcpCommitOutcome.HeldByAnotherClient, null);
            }

            // The occupant only had a stale offer on this address - take it over.
            FreeAddress(requestedAddress);
        }

        var lease = Record(new DhcpLease(clientId, requestedAddress, UtcNow, _leaseDuration, ServerIdentifier, DhcpLeaseState.Active));
        return new DhcpCommitResult(DhcpCommitOutcome.Committed, lease);
    }

    public bool CancelOffer(DhcpClientId clientId)
    {
        if (_leasesByClient.TryGetValue(clientId, out var lease) && lease.State == DhcpLeaseState.Offered)
        {
            _leasesByClient.Remove(clientId);
            FreeAddress(lease.Address);
            return true;
        }

        return false;
    }

    public bool Release(DhcpClientId clientId)
    {
        if (!_leasesByClient.Remove(clientId, out var lease))
        {
            return false;
        }

        FreeAddress(lease.Address);
        return true;
    }

    public bool Decline(DhcpClientId clientId, IPv4Address address)
    {
        if (_leasesByClient.TryGetValue(clientId, out var lease) && lease.Address == address)
        {
            _leasesByClient.Remove(clientId);
        }

        FreeAddress(address);

        // A declined address stays out of the pool for the life of the simulation session (brief
        // section 19 - no sophisticated conflict re-detection). A decade is effectively "forever"
        // here and keeps Expiration arithmetic safe.
        _declined[address] = new DhcpLease(clientId, address, UtcNow, TimeSpan.FromDays(3650), ServerIdentifier, DhcpLeaseState.Declined);
        return true;
    }

    public IReadOnlyList<DhcpLease> PruneExpired()
    {
        var now = UtcNow;
        List<DhcpLease>? expired = null;

        foreach (var (clientId, lease) in _leasesByClient.ToArray())
        {
            if (lease.IsExpired(now))
            {
                _leasesByClient[clientId] = lease.WithState(DhcpLeaseState.Expired);
                FreeAddress(lease.Address);
                (expired ??= []).Add(_leasesByClient[clientId]);
            }
            else if (lease.IsStaleOffer(now))
            {
                _leasesByClient.Remove(clientId);
                FreeAddress(lease.Address);
            }
        }

        return expired ?? (IReadOnlyList<DhcpLease>)Array.Empty<DhcpLease>();
    }

    private IPv4Address? ChooseAddress(DhcpClientId clientId, IPv4Address? requestedAddress)
    {
        // 1. A reservation always wins (brief section 38).
        if (_pool.ReservationFor(clientId) is { } reserved && IsFreeForClient(reserved, clientId))
        {
            return reserved;
        }

        // 2. Stick with the address the client already has, if it is still usable (brief section 15 - renewal keeps the address).
        if (_leasesByClient.TryGetValue(clientId, out var existing)
            && _pool.IsAssignableTo(existing.Address, clientId)
            && IsFreeForClient(existing.Address, clientId)
            && !_declined.ContainsKey(existing.Address))
        {
            return existing.Address;
        }

        // 3. Honour a specific request (brief section 7).
        if (requestedAddress is { } wanted
            && _pool.IsAssignableTo(wanted, clientId)
            && IsFreeForClient(wanted, clientId)
            && !_declined.ContainsKey(wanted))
        {
            return wanted;
        }

        // 4. The lowest free pool address.
        foreach (var candidate in _pool.AssignableAddresses(clientId))
        {
            if (IsFreeForClient(candidate, clientId) && !_declined.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private bool IsFreeForClient(IPv4Address address, DhcpClientId clientId) =>
        !_occupantByAddress.TryGetValue(address, out var occupant) || occupant == clientId;

    private DhcpLease Record(DhcpLease lease)
    {
        if (_leasesByClient.TryGetValue(lease.ClientId, out var previous) && previous.Address != lease.Address)
        {
            FreeAddress(previous.Address);
        }

        _leasesByClient[lease.ClientId] = lease;
        _occupantByAddress[lease.Address] = lease.ClientId;
        return lease;
    }

    private void FreeAddress(IPv4Address address)
    {
        if (!_declined.ContainsKey(address))
        {
            _occupantByAddress.Remove(address);
        }
    }
}
