using System.Globalization;
using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Networking;

/// <summary>
/// An IPv6 network (prefix) - a <see cref="NetworkAddress"/> paired with a
/// <see cref="PrefixLength"/>, e.g. <c>2001:db8:1234::/48</c>. Immutable; construction always
/// normalises the supplied address down to the prefix, so
/// <c>Create(2001:db8:1234:5678::1, 48)</c> yields <c>2001:db8:1234::/48</c>.
///
/// It exposes the derived facts a future IPv6 routing phase needs - the
/// <see cref="FirstAddress"/> / <see cref="LastAddress"/> of the block and
/// <see cref="Contains(IPv6Address)"/> / <see cref="Contains(IPv6Network)"/> membership. IPv6 has
/// no network/broadcast split and no "usable host" arithmetic, so none is modelled here.
/// </summary>
public sealed class IPv6Network : IEquatable<IPv6Network>
{
    /// <summary>The largest valid IPv6 prefix length.</summary>
    public const int MaxPrefixLength = 128;

    private readonly UInt128 _mask;

    private IPv6Network(IPv6Address networkAddress, int prefixLength, UInt128 mask)
    {
        NetworkAddress = networkAddress;
        PrefixLength = prefixLength;
        _mask = mask;
    }

    /// <summary>The prefix (base) address - the lowest address in the block.</summary>
    public IPv6Address NetworkAddress { get; }

    /// <summary>The prefix length, 0-128.</summary>
    public int PrefixLength { get; }

    /// <summary>The lowest address in the block (same as <see cref="NetworkAddress"/>).</summary>
    public IPv6Address FirstAddress => NetworkAddress;

    /// <summary>The highest address in the block (prefix bits fixed, all host bits set).</summary>
    public IPv6Address LastAddress => new(NetworkAddress.Value | ~_mask);

    /// <summary>
    /// Builds a network from any address in it and a prefix length. The address is normalised to
    /// the prefix. Throws <see cref="DomainException"/> if the prefix is outside 0-128.
    /// </summary>
    public static IPv6Network Create(IPv6Address address, int prefixLength)
    {
        if (prefixLength is < 0 or > MaxPrefixLength)
        {
            throw new DomainException($"An IPv6 prefix length must be between 0 and {MaxPrefixLength}, but was {prefixLength}.");
        }

        var mask = MaskFor(prefixLength);
        return new IPv6Network(new IPv6Address(address.Value & mask), prefixLength, mask);
    }

    /// <summary>
    /// Parses prefix notation (<c>address/prefix</c>). Throws <see cref="DomainException"/> for a
    /// malformed string, a bad address or a prefix outside 0-128.
    /// </summary>
    public static IPv6Network Parse(string? text) =>
        TryParse(text, out var result)
            ? result!
            : throw new DomainException($"'{text}' is not valid IPv6 prefix notation (expected 'address/prefix').");

    /// <summary>Non-throwing counterpart of <see cref="Parse"/>.</summary>
    public static bool TryParse(string? text, out IPv6Network? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var slash = text.IndexOf('/');
        if (slash <= 0 || slash == text.Length - 1)
        {
            return false;
        }

        if (!IPv6Address.TryParse(text[..slash], out var address))
        {
            return false;
        }

        if (!int.TryParse(text[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var prefix)
            || prefix is < 0 or > MaxPrefixLength)
        {
            return false;
        }

        result = Create(address, prefix);
        return true;
    }

    /// <summary>True when <paramref name="address"/> falls inside this prefix.</summary>
    public bool Contains(IPv6Address address) => (address.Value & _mask) == NetworkAddress.Value;

    /// <summary>True when <paramref name="other"/> is entirely inside this prefix (equal or more specific).</summary>
    public bool Contains(IPv6Network other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.PrefixLength >= PrefixLength && Contains(other.NetworkAddress);
    }

    public bool Equals(IPv6Network? other) =>
        other is not null && NetworkAddress == other.NetworkAddress && PrefixLength == other.PrefixLength;

    public override bool Equals(object? obj) => Equals(obj as IPv6Network);

    public override int GetHashCode() => HashCode.Combine(NetworkAddress, PrefixLength);

    /// <summary>Prefix notation, e.g. <c>2001:db8:1234::/48</c>.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{NetworkAddress}/{PrefixLength}");

    // A left shift by 128 is masked to a shift by 0 for UInt128, so /0 has to be special-cased;
    // every other prefix (1-128) is a well-defined "all ones, then shift the zeros in from the right".
    private static UInt128 MaskFor(int prefixLength) =>
        prefixLength == 0 ? UInt128.Zero : UInt128.MaxValue << (MaxPrefixLength - prefixLength);
}
