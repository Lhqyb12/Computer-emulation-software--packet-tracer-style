using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Networking;

/// <summary>
/// One IPv6 address assignment on a <see cref="NetworkInterface"/>: an <see cref="Address"/>, its
/// <see cref="PrefixLength"/>, and whether it is the interface's <see cref="IsPrimary"/> address.
/// Immutable value object with value-based equality.
///
/// The containing <see cref="Network"/> is <em>derived</em> from the address and prefix rather than
/// stored, so the parts can never disagree. An interface commonly holds several of these - a
/// link-local address plus one or more global / unique-local addresses; see
/// <see cref="NetworkInterface.AddIPv6Configuration"/>. Nothing here auto-generates an address
/// (no SLAAC, no EUI-64) - the addresses are simulated static configuration.
/// </summary>
public sealed class Ipv6InterfaceConfiguration : IEquatable<Ipv6InterfaceConfiguration>
{
    private Ipv6InterfaceConfiguration(IPv6Address address, int prefixLength, bool isPrimary)
    {
        Address = address;
        PrefixLength = prefixLength;
        IsPrimary = isPrimary;
    }

    /// <summary>The address configured on the interface.</summary>
    public IPv6Address Address { get; }

    /// <summary>The prefix length, 0-128.</summary>
    public int PrefixLength { get; }

    /// <summary>True when this is the interface's primary address (the one used as a packet source by default).</summary>
    public bool IsPrimary { get; }

    /// <summary>The prefix this address belongs to, derived from <see cref="Address"/> and <see cref="PrefixLength"/>.</summary>
    public IPv6Network Network => IPv6Network.Create(Address, PrefixLength);

    /// <summary>Canonical prefix form of the assignment, e.g. <c>2001:db8:1::10/64</c>.</summary>
    public string Cidr => $"{Address}/{PrefixLength}";

    /// <summary>
    /// Builds a configuration. Throws <see cref="DomainException"/> for an address that cannot be an
    /// interface address (the unspecified address <c>::</c>, the loopback address <c>::1</c>, or a
    /// multicast address) or a prefix outside 0-128.
    /// </summary>
    public static Ipv6InterfaceConfiguration Create(IPv6Address address, int prefixLength, bool isPrimary = true)
    {
        if (address.IsUnspecified)
        {
            throw new DomainException("An interface IPv6 address cannot be the unspecified address '::'.");
        }

        if (address.IsLoopback)
        {
            throw new DomainException("An interface IPv6 address cannot be the loopback address '::1'.");
        }

        if (address.IsMulticast)
        {
            throw new DomainException($"An interface IPv6 address cannot be a multicast address ('{address}').");
        }

        if (prefixLength is < 0 or > IPv6Network.MaxPrefixLength)
        {
            throw new DomainException(
                $"An IPv6 prefix length must be between 0 and {IPv6Network.MaxPrefixLength}, but was {prefixLength}.");
        }

        return new Ipv6InterfaceConfiguration(address, prefixLength, isPrimary);
    }

    /// <summary>Returns this configuration marked primary (or itself, if already primary).</summary>
    public Ipv6InterfaceConfiguration AsPrimary() =>
        IsPrimary ? this : new Ipv6InterfaceConfiguration(Address, PrefixLength, isPrimary: true);

    /// <summary>Returns this configuration marked secondary (or itself, if already secondary).</summary>
    public Ipv6InterfaceConfiguration AsSecondary() =>
        !IsPrimary ? this : new Ipv6InterfaceConfiguration(Address, PrefixLength, isPrimary: false);

    public bool Equals(Ipv6InterfaceConfiguration? other) =>
        other is not null
        && Address == other.Address
        && PrefixLength == other.PrefixLength
        && IsPrimary == other.IsPrimary;

    public override bool Equals(object? obj) => Equals(obj as Ipv6InterfaceConfiguration);

    public override int GetHashCode() => HashCode.Combine(Address, PrefixLength, IsPrimary);

    public override string ToString() => $"{Cidr}{(IsPrimary ? string.Empty : " (secondary)")}";
}
