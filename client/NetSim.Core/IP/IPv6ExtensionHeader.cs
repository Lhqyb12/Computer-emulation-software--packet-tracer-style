using NetSim.Core.Packets;

namespace NetSim.Core.IP;

/// <summary>
/// The architectural foundation for IPv6 extension headers - the chain that can sit between the
/// fixed <see cref="IPv6Header"/> and the upper-layer payload:
///
/// <code>
/// IPv6 Header -> [Hop-by-Hop Options] -> [Routing] -> [Fragment] -> [Destination Options] -> TCP / UDP / ICMPv6
/// </code>
///
/// This phase implements <em>no</em> concrete extension header (no hop-by-hop processing, no
/// routing header, and explicitly no fragmentation / reassembly). It defines only this abstract
/// shape so a later phase can add real extension-header types as subclasses without touching
/// <see cref="IPv6Packet"/> or <see cref="IPv6Header"/>: a packet already carries an ordered
/// <see cref="IPv6Packet.ExtensionHeaders"/> list, and each header already declares
/// <see cref="HeaderType"/> (what it is) and <see cref="NextHeader"/> (what follows it).
/// </summary>
public abstract class IPv6ExtensionHeader
{
    protected IPv6ExtensionHeader(NextHeader headerType, NextHeader nextHeader, int lengthBytes)
    {
        HeaderType = headerType;
        NextHeader = nextHeader;
        LengthBytes = lengthBytes;
    }

    /// <summary>Which extension header this is (the <see cref="NextHeader"/> value that selected it).</summary>
    public NextHeader HeaderType { get; }

    /// <summary>What comes after this header - the next extension header, or the upper-layer protocol.</summary>
    public NextHeader NextHeader { get; }

    /// <summary>The simulated size of this header in bytes.</summary>
    public int LengthBytes { get; }

    /// <summary>
    /// Structural self-check. The base implementation only verifies the declared length is not
    /// negative; a concrete extension header overrides this to check its own fields.
    /// </summary>
    public virtual PacketValidationResult Validate() =>
        LengthBytes >= 0
            ? PacketValidationResult.Valid
            : PacketValidationResult.Invalid($"IPv6 extension header '{HeaderType.Name}' has a negative length ({LengthBytes}).");
}
