namespace NetSim.Core.Networking;

/// <summary>
/// A coarse classification of an <see cref="IPv4Address"/>, derived purely from the address bits.
/// This is address <em>categorisation</em> only - it drives no behaviour in this phase (no NAT,
/// no routing, no filtering). The values are mutually exclusive; <see cref="IPv4Address.Category"/>
/// resolves them in priority order (unspecified / limited broadcast first, then loopback, then
/// link-local, then private, then multicast, otherwise public).
/// </summary>
public enum IPv4AddressCategory
{
    /// <summary>0.0.0.0 - "this host" / unset.</summary>
    Unspecified,

    /// <summary>127.0.0.0/8.</summary>
    Loopback,

    /// <summary>169.254.0.0/16 (RFC 3927 auto-configuration).</summary>
    LinkLocal,

    /// <summary>RFC 1918: 10.0.0.0/8, 172.16.0.0/12 or 192.168.0.0/16.</summary>
    Private,

    /// <summary>224.0.0.0/4.</summary>
    Multicast,

    /// <summary>255.255.255.255 - the limited broadcast address.</summary>
    LimitedBroadcast,

    /// <summary>Any globally-routable unicast address that is none of the above.</summary>
    Public,
}
