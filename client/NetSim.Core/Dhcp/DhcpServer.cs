using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;

namespace NetSim.Core.Dhcp;

/// <summary>Default <see cref="IDhcpServer"/>. See the interface for the overall design.</summary>
public sealed class DhcpServer : IDhcpServer
{
    private readonly DhcpLeaseManager _leases;

    /// <summary>
    /// Builds a server from a configuration. Throws <see cref="DomainException"/> if the
    /// configuration is invalid (brief section 27) - callers validate through
    /// <see cref="DhcpServerConfiguration.Validate"/> and surface the errors before getting here.
    /// </summary>
    public DhcpServer(DhcpServerConfiguration configuration, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var validation = configuration.Validate();
        if (!validation.IsValid)
        {
            throw new DomainException($"Invalid DHCP server configuration: {validation}");
        }

        Configuration = configuration;
        _leases = new DhcpLeaseManager(configuration.BuildPool(), configuration.ServerAddress, configuration.LeaseDuration, timeProvider);
    }

    public DhcpServerConfiguration Configuration { get; }

    public IPv4Address ServerAddress => Configuration.ServerAddress;

    public IDhcpLeaseManager Leases => _leases;

    public event EventHandler<DhcpEventArgs>? DiscoverReceived;

    public event EventHandler<DhcpEventArgs>? OfferCreated;

    public event EventHandler<DhcpEventArgs>? RequestReceived;

    public event EventHandler<DhcpEventArgs>? AckCreated;

    public event EventHandler<DhcpEventArgs>? NakCreated;

    public event EventHandler<DhcpEventArgs>? LeaseAllocated;

    public event EventHandler<DhcpEventArgs>? LeaseReleased;

    public event EventHandler<DhcpEventArgs>? LeaseExpired;

    public event EventHandler<DhcpEventArgs>? AddressDeclined;

    public event EventHandler<DhcpEventArgs>? PoolExhausted;

    public IReadOnlyList<DhcpLease> PruneExpiredLeases()
    {
        var expired = _leases.PruneExpired();
        foreach (var lease in expired)
        {
            LeaseExpired?.Invoke(this, new DhcpEventArgs(
                clientId: lease.ClientId, address: lease.Address, lease: lease, detail: $"Lease for {lease.Address} expired."));
        }

        return expired;
    }

    public DhcpMessage? HandleMessage(DhcpMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        PruneExpiredLeases();

        if (request.Validate() is { IsValid: false })
        {
            return null;
        }

        return request.MessageType switch
        {
            DhcpMessageType.Discover => HandleDiscover(request),
            DhcpMessageType.Request => HandleRequest(request),
            DhcpMessageType.Release => HandleRelease(request),
            DhcpMessageType.Decline => HandleDecline(request),
            _ => null,
        };
    }

    private DhcpMessage? HandleDiscover(DhcpMessage request)
    {
        var clientId = request.ClientId;
        DiscoverReceived?.Invoke(this, new DhcpEventArgs(request, clientId: clientId, detail: $"DISCOVER from {clientId}"));

        var lease = _leases.Offer(clientId, request.Options.RequestedIpAddress);
        if (lease is null)
        {
            PoolExhausted?.Invoke(this, new DhcpEventArgs(
                request, clientId: clientId, detail: $"No addresses available to offer {clientId} - pool exhausted."));
            return null;
        }

        var offer = DhcpMessage.CreateOffer(request.TransactionId, request.ClientHardwareAddress, lease.Address, ServerAddress, BuildAssignmentOptions());
        OfferCreated?.Invoke(this, new DhcpEventArgs(offer, clientId: clientId, address: lease.Address, lease: lease, detail: offer.ToString()));
        return offer;
    }

    private DhcpMessage? HandleRequest(DhcpMessage request)
    {
        var clientId = request.ClientId;
        RequestReceived?.Invoke(this, new DhcpEventArgs(request, clientId: clientId, detail: $"REQUEST from {clientId}"));

        // Brief section 36: a REQUEST that names a different server means our OFFER was not selected.
        if (request.Options.ServerIdentifier is { } selectedServer && selectedServer != ServerAddress)
        {
            _leases.CancelOffer(clientId);
            return null;
        }

        var requested = request.Options.RequestedIpAddress
            ?? (request.ClientIpAddress.IsUnspecified ? (IPv4Address?)null : request.ClientIpAddress);

        if (requested is null)
        {
            return Nak(request, clientId, "REQUEST did not specify an address.");
        }

        var commit = _leases.Commit(clientId, requested.Value);
        if (!commit.IsCommitted)
        {
            var reason = commit.Outcome switch
            {
                DhcpCommitOutcome.OutOfRange => $"{requested} is outside this server's pool.",
                DhcpCommitOutcome.HeldByAnotherClient => $"{requested} is leased to another client.",
                _ => $"{requested} cannot be assigned by this server.",
            };

            return Nak(request, clientId, reason);
        }

        var lease = commit.Lease!;
        var ack = DhcpMessage.CreateAck(request.TransactionId, request.ClientHardwareAddress, lease.Address, ServerAddress, BuildAssignmentOptions());
        AckCreated?.Invoke(this, new DhcpEventArgs(ack, clientId: clientId, address: lease.Address, lease: lease, detail: ack.ToString()));
        LeaseAllocated?.Invoke(this, new DhcpEventArgs(clientId: clientId, address: lease.Address, lease: lease, detail: $"Lease {lease.Address} -> {clientId} active."));
        return ack;
    }

    private DhcpMessage? HandleRelease(DhcpMessage request)
    {
        var clientId = request.ClientId;
        var lease = _leases.GetLease(clientId);
        if (_leases.Release(clientId))
        {
            LeaseReleased?.Invoke(this, new DhcpEventArgs(
                request, clientId: clientId, address: lease?.Address, lease: lease, detail: $"RELEASE from {clientId} - {lease?.Address} returned to the pool."));
        }

        return null;
    }

    private DhcpMessage? HandleDecline(DhcpMessage request)
    {
        var clientId = request.ClientId;
        var declined = request.Options.RequestedIpAddress
            ?? (request.ClientIpAddress.IsUnspecified ? (IPv4Address?)null : request.ClientIpAddress);

        if (declined is { } address)
        {
            _leases.Decline(clientId, address);
            AddressDeclined?.Invoke(this, new DhcpEventArgs(
                request, clientId: clientId, address: address, detail: $"DECLINE from {clientId} - {address} marked unusable."));
        }

        return null;
    }

    private DhcpMessage Nak(DhcpMessage request, DhcpClientId clientId, string reason)
    {
        var nak = DhcpMessage.CreateNak(request.TransactionId, request.ClientHardwareAddress, ServerAddress, reason);
        NakCreated?.Invoke(this, new DhcpEventArgs(nak, clientId: clientId, detail: reason));
        return nak;
    }

    private DhcpOptions BuildAssignmentOptions()
    {
        var lease = Configuration.LeaseDuration;
        return new DhcpOptions.Builder
        {
            SubnetMask = Configuration.SubnetMask,
            Router = Configuration.DefaultGateway,
            DomainNameServers = Configuration.DnsServer is { } dns ? [dns] : [],
            ServerIdentifier = ServerAddress,
            IpAddressLeaseTime = lease,
            RenewalTime = Configuration.RenewalTime ?? lease * DhcpProtocol.RenewalTimeFactor,
            RebindingTime = Configuration.RebindingTime ?? lease * DhcpProtocol.RebindingTimeFactor,
        }.Build();
    }
}
