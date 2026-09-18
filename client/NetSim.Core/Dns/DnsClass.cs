namespace NetSim.Core.Dns;

/// <summary>
/// The DNS record class (RFC 1035 section 3.2.4). Only <see cref="IN"/> (Internet) is meaningful
/// today - the other historical classes (CH, HS, ...) are never used and are not modelled.
/// </summary>
public enum DnsClass
{
    IN = 1,
}
