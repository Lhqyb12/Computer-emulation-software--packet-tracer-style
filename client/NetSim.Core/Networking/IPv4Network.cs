using System.Globalization;
using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Networking;

/// <summary>
/// An IPv4 network (subnet) - a <see cref="NetworkAddress"/> paired with a
/// <see cref="PrefixLength"/>, e.g. <c>192.168.1.0/24</c>. Immutable; construction always
/// normalises the supplied address down to the network address for the prefix, so
/// <c>Create(192.168.1.130, 26)</c> yields <c>192.168.1.128/26</c>.
///
/// It exposes the derived facts a future routing / addressing phase needs -
/// <see cref="SubnetMask"/>, <see cref="BroadcastAddress"/>, the usable host range and
/// <see cref="Contains(IPv4Address)"/> membership - and handles the special prefixes carefully:
/// <c>/31</c> (RFC 3021 point-to-point: two usable hosts, no network/broadcast split) and
/// <c>/32</c> (a single host) do not get the traditional "subtract network and broadcast"
/// treatment.
/// </summary>
public sealed class IPv4Network : IEquatable<IPv4Network>
{
    private IPv4Network(IPv4Address networkAddress, int prefixLength, SubnetMask subnetMask)
    {
        NetworkAddress = networkAddress;
        PrefixLength = prefixLength;
        SubnetMask = subnetMask;
    }

    /// <summary>The network (base) address - the lowest address in the block.</summary>
    public IPv4Address NetworkAddress { get; }

    /// <summary>The CIDR prefix length, 0-32.</summary>
    public int PrefixLength { get; }

    /// <summary>The subnet mask equivalent to <see cref="PrefixLength"/>.</summary>
    public SubnetMask SubnetMask { get; }

    /// <summary>Total number of addresses in the block, including the network and broadcast addresses.</summary>
    public long TotalAddressCount => 1L << (32 - PrefixLength);

    /// <summary>The highest address in the block (network address OR the wildcard mask).</summary>
    public IPv4Address LastAddress => new(NetworkAddress.Value | SubnetMask.WildcardMask.Value);

    /// <summary>
    /// True when the block has a distinct broadcast address - i.e. the prefix is /30 or shorter.
    /// A /31 and a /32 have none.
    /// </summary>
    public bool HasBroadcastAddress => PrefixLength <= 30;

    /// <summary>
    /// The all-ones (directed broadcast) address of the block, or <c>null</c> for a /31 or /32
    /// where no such address exists.
    /// </summary>
    public IPv4Address? BroadcastAddress => HasBroadcastAddress ? LastAddress : null;

    /// <summary>The first address usable by a host. See the type remarks for the /31 and /32 handling.</summary>
    public IPv4Address FirstUsableHost => PrefixLength <= 30
        ? new IPv4Address(NetworkAddress.Value + 1)
        : NetworkAddress;

    /// <summary>The last address usable by a host. See the type remarks for the /31 and /32 handling.</summary>
    public IPv4Address LastUsableHost => PrefixLength switch
    {
        <= 30 => new IPv4Address(LastAddress.Value - 1),
        31 => LastAddress,
        _ => NetworkAddress, // /32
    };

    /// <summary>
    /// How many addresses in the block a host can be assigned: the usual <c>2^(32-prefix) - 2</c>
    /// for /30 and shorter, <c>2</c> for a /31 (RFC 3021) and <c>1</c> for a /32.
    /// </summary>
    public long UsableHostCount => PrefixLength switch
    {
        <= 30 => TotalAddressCount - 2,
        31 => 2,
        _ => 1, // /32
    };

    /// <summary>
    /// Builds a network from any address in it and a prefix length. The address is normalised to
    /// the network address. Throws <see cref="DomainException"/> if the prefix is outside 0-32.
    /// </summary>
    public static IPv4Network Create(IPv4Address address, int prefixLength)
    {
        var mask = SubnetMask.FromPrefixLength(prefixLength);
        var network = new IPv4Address(address.Value & mask.Value);
        return new IPv4Network(network, prefixLength, mask);
    }

    /// <summary>As <see cref="Create(IPv4Address, int)"/>, taking a <see cref="SubnetMask"/> instead of a prefix length.</summary>
    public static IPv4Network Create(IPv4Address address, SubnetMask subnetMask) =>
        Create(address, subnetMask.PrefixLength);

    /// <summary>
    /// Parses CIDR notation (<c>a.b.c.d/n</c>). Throws <see cref="DomainException"/> for a malformed
    /// string, a bad address or a prefix outside 0-32.
    /// </summary>
    public static IPv4Network Parse(string? cidr) =>
        TryParse(cidr, out var result)
            ? result!
            : throw new DomainException($"'{cidr}' is not valid CIDR notation (expected 'a.b.c.d/prefix').");

    /// <summary>Non-throwing counterpart of <see cref="Parse"/>.</summary>
    public static bool TryParse(string? cidr, out IPv4Network? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(cidr))
        {
            return false;
        }

        var slash = cidr.IndexOf('/');
        if (slash <= 0 || slash == cidr.Length - 1)
        {
            return false;
        }

        if (!IPv4Address.TryParse(cidr[..slash], out var address))
        {
            return false;
        }

        if (!int.TryParse(cidr[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var prefix)
            || prefix is < 0 or > 32)
        {
            return false;
        }

        result = Create(address, prefix);
        return true;
    }

    /// <summary>True when <paramref name="address"/> falls inside this network.</summary>
    public bool Contains(IPv4Address address) =>
        (address.Value & SubnetMask.Value) == NetworkAddress.Value;

    /// <summary>True when <paramref name="other"/> is entirely inside this network (equal or more specific).</summary>
    public bool Contains(IPv4Network other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.PrefixLength >= PrefixLength && Contains(other.NetworkAddress);
    }

    public bool Equals(IPv4Network? other) =>
        other is not null && NetworkAddress == other.NetworkAddress && PrefixLength == other.PrefixLength;

    public override bool Equals(object? obj) => Equals(obj as IPv4Network);

    public override int GetHashCode() => HashCode.Combine(NetworkAddress, PrefixLength);

    public static bool operator ==(IPv4Network? left, IPv4Network? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(IPv4Network? left, IPv4Network? right) => !(left == right);

    /// <summary>CIDR notation, e.g. <c>192.168.1.0/24</c>.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{NetworkAddress}/{PrefixLength}");
}
