namespace NetSim.Core.Networking;

/// <summary>
/// A coarse classification of an <see cref="IPv6Address"/>, derived purely from the address bits.
/// This is address <em>categorisation</em> only - it drives no behaviour in this phase (no routing,
/// no Neighbor Discovery, no filtering). The values are mutually exclusive;
/// <see cref="IPv6Address.Category"/> resolves them in priority order (unspecified / loopback first,
/// then multicast, then link-local, then unique-local, then global unicast, otherwise other).
/// </summary>
public enum IPv6AddressCategory
{
    /// <summary><c>::</c> - the unspecified address.</summary>
    Unspecified,

    /// <summary><c>::1</c> - the loopback address.</summary>
    Loopback,

    /// <summary><c>fe80::/10</c> - link-local unicast.</summary>
    LinkLocal,

    /// <summary><c>fc00::/7</c> - unique local addresses (RFC 4193).</summary>
    UniqueLocal,

    /// <summary><c>ff00::/8</c> - multicast.</summary>
    Multicast,

    /// <summary><c>2000::/3</c> - the currently-allocated global unicast space.</summary>
    GlobalUnicast,

    /// <summary>Any address that is none of the above (other reserved / unassigned ranges).</summary>
    Other,
}
