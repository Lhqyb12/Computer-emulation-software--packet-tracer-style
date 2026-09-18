using NetSim.Core.Networking;

namespace NetSim.Core.Dhcp;

/// <summary>A single MAC-&gt;address reservation in a <see cref="DhcpServerConfiguration"/> (brief section 38).</summary>
public sealed record DhcpReservation(MacAddress HardwareAddress, IPv4Address Address)
{
    public DhcpClientId ClientId => DhcpClientId.FromHardwareAddress(HardwareAddress);
}

/// <summary>
/// Everything a simulated DHCP server needs to run (brief section 26): its own address, the subnet
/// it serves, the dynamic pool range, the optional gateway/DNS it hands to clients, the lease
/// duration, and any excluded addresses / reservations. A proper model, not values scattered
/// through the code - and it validates itself (brief section 27) before a <see cref="DhcpServer"/>
/// will accept it.
///
/// Persistence-friendly: it is plain data with value-typed fields, so a future phase can map it into
/// the existing LiteDB project store without redesigning anything (brief section 72).
/// </summary>
public sealed class DhcpServerConfiguration
{
    public DhcpServerConfiguration(
        IPv4Address serverAddress,
        int prefixLength,
        IPv4Address poolStart,
        IPv4Address poolEnd,
        IPv4Address? defaultGateway = null,
        IPv4Address? dnsServer = null,
        TimeSpan? leaseDuration = null,
        IReadOnlyList<IPv4Address>? excludedAddresses = null,
        IReadOnlyList<DhcpReservation>? reservations = null,
        TimeSpan? renewalTime = null,
        TimeSpan? rebindingTime = null)
    {
        ServerAddress = serverAddress;
        PrefixLength = prefixLength;
        PoolStart = poolStart;
        PoolEnd = poolEnd;
        DefaultGateway = defaultGateway;
        DnsServer = dnsServer;
        LeaseDuration = leaseDuration ?? DhcpProtocol.DefaultLeaseDuration;
        ExcludedAddresses = excludedAddresses ?? [];
        Reservations = reservations ?? [];
        RenewalTime = renewalTime;
        RebindingTime = rebindingTime;
    }

    public IPv4Address ServerAddress { get; }

    public int PrefixLength { get; }

    public IPv4Address PoolStart { get; }

    public IPv4Address PoolEnd { get; }

    public IPv4Address? DefaultGateway { get; }

    public IPv4Address? DnsServer { get; }

    public TimeSpan LeaseDuration { get; }

    public IReadOnlyList<IPv4Address> ExcludedAddresses { get; }

    public IReadOnlyList<DhcpReservation> Reservations { get; }

    public TimeSpan? RenewalTime { get; }

    public TimeSpan? RebindingTime { get; }

    /// <summary>The subnet mask the pool's prefix length corresponds to. Only meaningful once <see cref="Validate"/> passes.</summary>
    public SubnetMask SubnetMask => SubnetMask.FromPrefixLength(PrefixLength);

    /// <summary>The subnet the server serves, derived from its own address and prefix.</summary>
    public IPv4Network Subnet => IPv4Network.Create(ServerAddress, PrefixLength);

    /// <summary>
    /// Checks every rule from brief section 27 and returns them all. Never throws - a bad prefix is
    /// reported as an error rather than an exception.
    /// </summary>
    public DhcpConfigurationValidationResult Validate()
    {
        var errors = new List<string>();

        if (PrefixLength is < 0 or > 30)
        {
            errors.Add($"Prefix length must be between 0 and 30 (a subnet with a network and broadcast address), but was {PrefixLength}.");

            // Nothing else can be checked without a usable subnet.
            return new DhcpConfigurationValidationResult(errors);
        }

        var subnet = Subnet;
        var mask = SubnetMask.FromPrefixLength(PrefixLength);

        if (LeaseDuration <= TimeSpan.Zero)
        {
            errors.Add($"Lease duration must be positive, but was {LeaseDuration}.");
        }

        if (PoolStart.Value > PoolEnd.Value)
        {
            errors.Add($"Pool start {PoolStart} is after pool end {PoolEnd}.");
        }

        if (!subnet.Contains(PoolStart))
        {
            errors.Add($"Pool start {PoolStart} is outside the server subnet {subnet}.");
        }

        if (!subnet.Contains(PoolEnd))
        {
            errors.Add($"Pool end {PoolEnd} is outside the server subnet {subnet}.");
        }

        if (RangeContains(subnet.NetworkAddress))
        {
            errors.Add($"The pool includes the network address {subnet.NetworkAddress}.");
        }

        if (subnet.BroadcastAddress is { } broadcast && RangeContains(broadcast))
        {
            errors.Add($"The pool includes the broadcast address {broadcast}.");
        }

        if (RangeContains(ServerAddress))
        {
            errors.Add($"The pool includes the server's own address {ServerAddress}.");
        }

        var duplicateExclusions = ExcludedAddresses
            .GroupBy(a => a)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        foreach (var duplicate in duplicateExclusions)
        {
            errors.Add($"Excluded address {duplicate} is listed more than once.");
        }

        var reservationAddresses = new HashSet<IPv4Address>();
        var reservationClients = new HashSet<DhcpClientId>();
        foreach (var reservation in Reservations)
        {
            if (!subnet.Contains(reservation.Address))
            {
                errors.Add($"Reservation {reservation.Address} for {reservation.HardwareAddress} is outside the server subnet {subnet}.");
            }

            if (reservation.Address == subnet.NetworkAddress
                || reservation.Address == subnet.BroadcastAddress
                || reservation.Address == ServerAddress)
            {
                errors.Add($"Reservation {reservation.Address} for {reservation.HardwareAddress} is a reserved infrastructure address.");
            }

            if (!reservationAddresses.Add(reservation.Address))
            {
                errors.Add($"More than one reservation uses the address {reservation.Address}.");
            }

            if (!reservationClients.Add(reservation.ClientId))
            {
                errors.Add($"More than one reservation is defined for {reservation.HardwareAddress}.");
            }
        }

        _ = mask;
        return errors.Count == 0 ? DhcpConfigurationValidationResult.Valid : new DhcpConfigurationValidationResult(errors);
    }

    /// <summary>Builds the <see cref="DhcpAddressPool"/> this configuration describes. Call <see cref="Validate"/> first.</summary>
    public DhcpAddressPool BuildPool()
    {
        var reservations = Reservations.ToDictionary(r => r.ClientId, r => r.Address);
        return new DhcpAddressPool(PoolStart, PoolEnd, Subnet, ServerAddress, ExcludedAddresses, reservations);
    }

    private bool RangeContains(IPv4Address address) =>
        address.Value >= PoolStart.Value && address.Value <= PoolEnd.Value;
}
