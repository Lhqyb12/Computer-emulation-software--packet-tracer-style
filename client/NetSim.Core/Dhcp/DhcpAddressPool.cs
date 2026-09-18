using NetSim.Core.Networking;

namespace NetSim.Core.Dhcp;

/// <summary>
/// The range of addresses a DHCP server may hand out (brief section 11): a start/end pair inside a
/// subnet, minus the addresses that can never be a host assignment - the network and directed-
/// broadcast address, the server's own address, and any operator-excluded addresses (brief section
/// 39) - plus a set of MAC-&gt;address reservations (brief section 38) that are pinned to one client.
///
/// This type is pure address arithmetic and set membership - it holds no leases and no clock. Which
/// in-range address is actually free right now is <see cref="DhcpLeaseManager"/>'s decision,
/// combining this pool with the live lease table.
/// </summary>
public sealed class DhcpAddressPool
{
    private readonly HashSet<IPv4Address> _excluded;
    private readonly Dictionary<DhcpClientId, IPv4Address> _reservations;
    private readonly HashSet<IPv4Address> _reservedAddresses;

    public DhcpAddressPool(
        IPv4Address start,
        IPv4Address end,
        IPv4Network subnet,
        IPv4Address serverAddress,
        IEnumerable<IPv4Address>? excludedAddresses = null,
        IReadOnlyDictionary<DhcpClientId, IPv4Address>? reservations = null)
    {
        ArgumentNullException.ThrowIfNull(subnet);

        Start = start;
        End = end;
        Subnet = subnet;
        ServerAddress = serverAddress;
        _excluded = excludedAddresses is null ? [] : [.. excludedAddresses];
        _reservations = reservations is null ? [] : new Dictionary<DhcpClientId, IPv4Address>(reservations);
        _reservedAddresses = [.. _reservations.Values];
    }

    public IPv4Address Start { get; }

    public IPv4Address End { get; }

    public IPv4Network Subnet { get; }

    public IPv4Address ServerAddress { get; }

    public IReadOnlyCollection<IPv4Address> ExcludedAddresses => _excluded;

    public IReadOnlyDictionary<DhcpClientId, IPv4Address> Reservations => _reservations;

    /// <summary>The number of distinct addresses in the [start, end] range, before exclusions.</summary>
    public long RangeSize => End.Value >= Start.Value ? (long)End.Value - Start.Value + 1 : 0;

    /// <summary>True when <paramref name="address"/> falls in the [start, end] range.</summary>
    public bool IsInRange(IPv4Address address) => address.Value >= Start.Value && address.Value <= End.Value;

    /// <summary>True when <paramref name="address"/> is the subnet's network or directed-broadcast address, or the server's own address.</summary>
    public bool IsReservedInfrastructure(IPv4Address address) =>
        address == Subnet.NetworkAddress || address == Subnet.BroadcastAddress || address == ServerAddress;

    /// <summary>True when the operator explicitly excluded <paramref name="address"/> (brief section 39).</summary>
    public bool IsExcluded(IPv4Address address) => _excluded.Contains(address);

    /// <summary>The address reserved for <paramref name="clientId"/>, or null (brief section 38).</summary>
    public IPv4Address? ReservationFor(DhcpClientId clientId) =>
        _reservations.TryGetValue(clientId, out var address) ? address : null;

    /// <summary>True when <paramref name="address"/> is reserved for a client other than <paramref name="clientId"/>.</summary>
    public bool IsReservedForAnother(IPv4Address address, DhcpClientId clientId) =>
        _reservedAddresses.Contains(address)
        && !(_reservations.TryGetValue(clientId, out var mine) && mine == address);

    /// <summary>
    /// True when <paramref name="address"/> is one the server could give to <paramref name="clientId"/>
    /// on address grounds alone (in range, not infrastructure, not excluded, not reserved for someone
    /// else). It does not consider whether the address is currently leased - that is
    /// <see cref="DhcpLeaseManager"/>'s job.
    /// </summary>
    public bool IsAssignableTo(IPv4Address address, DhcpClientId clientId) =>
        IsInRange(address)
        && !IsReservedInfrastructure(address)
        && !IsExcluded(address)
        && !IsReservedForAnother(address, clientId);

    /// <summary>
    /// Every in-range address the server could dynamically assign to <paramref name="clientId"/>,
    /// lowest first. A large pool is not materialised - callers stop at the first free one.
    /// </summary>
    public IEnumerable<IPv4Address> AssignableAddresses(DhcpClientId clientId)
    {
        if (End.Value < Start.Value)
        {
            yield break;
        }

        for (var value = Start.Value; ; value++)
        {
            var address = new IPv4Address(value);
            if (IsAssignableTo(address, clientId))
            {
                yield return address;
            }

            if (value == End.Value)
            {
                yield break;
            }
        }
    }
}
