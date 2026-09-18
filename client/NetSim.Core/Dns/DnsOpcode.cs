namespace NetSim.Core.Dns;

/// <summary>
/// The DNS header's Opcode field (RFC 1035 section 4.1.1). This simulator only ever issues and
/// answers standard queries - <see cref="Query"/> is the only value produced, but the field is
/// still modelled (rather than assumed) so <see cref="DnsHeader"/> stays a faithful, extensible
/// representation of the real header.
/// </summary>
public enum DnsOpcode
{
    Query = 0,
}
