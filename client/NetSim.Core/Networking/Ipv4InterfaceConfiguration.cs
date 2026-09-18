using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Networking;

/// <summary>
/// One IPv4 address assignment on a <see cref="NetworkInterface"/>: an <see cref="Address"/>, its
/// <see cref="PrefixLength"/> / <see cref="SubnetMask"/>, and whether it is the interface's
/// <see cref="IsPrimary"/> address. Immutable value object with value-based equality.
///
/// Everything else - the containing <see cref="Network"/>, its broadcast address, the usable host
/// range - is <em>derived</em> from the address and prefix rather than stored, so the parts can
/// never disagree. An interface can hold several of these (a primary plus secondary addresses);
/// see <see cref="NetworkInterface.AddIPv4Configuration"/>.
/// </summary>
public sealed class Ipv4InterfaceConfiguration : IEquatable<Ipv4InterfaceConfiguration>
{
    private Ipv4InterfaceConfiguration(IPv4Address address, int prefixLength, SubnetMask subnetMask, bool isPrimary)
    {
        Address = address;
        PrefixLength = prefixLength;
        SubnetMask = subnetMask;
        IsPrimary = isPrimary;
    }

    /// <summary>The host address configured on the interface.</summary>
    public IPv4Address Address { get; }

    /// <summary>The CIDR prefix length, 0-32.</summary>
    public int PrefixLength { get; }

    /// <summary>The subnet mask equivalent to <see cref="PrefixLength"/>.</summary>
    public SubnetMask SubnetMask { get; }

    /// <summary>True when this is the interface's primary address (the one used as a packet source by default).</summary>
    public bool IsPrimary { get; }

    /// <summary>The subnet this address belongs to, derived from <see cref="Address"/> and <see cref="PrefixLength"/>.</summary>
    public IPv4Network Network => IPv4Network.Create(Address, PrefixLength);

    /// <summary>Canonical CIDR form of the assignment, e.g. <c>192.168.1.10/24</c>.</summary>
    public string Cidr => $"{Address}/{PrefixLength}";

    /// <summary>
    /// Builds a configuration. Throws <see cref="DomainException"/> for an address that cannot be a
    /// host address (0.0.0.0, 255.255.255.255 or a multicast address) or a prefix outside 0-32.
    /// </summary>
    public static Ipv4InterfaceConfiguration Create(IPv4Address address, int prefixLength, bool isPrimary = true)
    {
        if (address.IsUnspecified)
        {
            throw new DomainException("An interface IPv4 address cannot be 0.0.0.0.");
        }

        if (address.IsLimitedBroadcast)
        {
            throw new DomainException("An interface IPv4 address cannot be the limited broadcast address 255.255.255.255.");
        }

        if (address.IsMulticast)
        {
            throw new DomainException($"An interface IPv4 address cannot be a multicast address ('{address}').");
        }

        var subnetMask = SubnetMask.FromPrefixLength(prefixLength);
        return new Ipv4InterfaceConfiguration(address, prefixLength, subnetMask, isPrimary);
    }

    /// <summary>As <see cref="Create(IPv4Address, int, bool)"/>, taking a <see cref="SubnetMask"/> instead of a prefix length.</summary>
    public static Ipv4InterfaceConfiguration Create(IPv4Address address, SubnetMask subnetMask, bool isPrimary = true) =>
        Create(address, subnetMask.PrefixLength, isPrimary);

    /// <summary>Returns this configuration marked primary (or itself, if already primary).</summary>
    public Ipv4InterfaceConfiguration AsPrimary() =>
        IsPrimary ? this : new Ipv4InterfaceConfiguration(Address, PrefixLength, SubnetMask, isPrimary: true);

    /// <summary>Returns this configuration marked secondary (or itself, if already secondary).</summary>
    public Ipv4InterfaceConfiguration AsSecondary() =>
        !IsPrimary ? this : new Ipv4InterfaceConfiguration(Address, PrefixLength, SubnetMask, isPrimary: false);

    public bool Equals(Ipv4InterfaceConfiguration? other) =>
        other is not null
        && Address == other.Address
        && PrefixLength == other.PrefixLength
        && IsPrimary == other.IsPrimary;

    public override bool Equals(object? obj) => Equals(obj as Ipv4InterfaceConfiguration);

    public override int GetHashCode() => HashCode.Combine(Address, PrefixLength, IsPrimary);

    public override string ToString() => $"{Cidr}{(IsPrimary ? string.Empty : " (secondary)")}";
}
