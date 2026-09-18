namespace NetSim.Core.Networking;

/// <summary>
/// How an <see cref="ArpCacheEntry"/> came to exist, and therefore how it behaves over time.
/// Deliberately minimal - a later phase can add richer states (Incomplete, Stale, Probe, ...)
/// without a redesign; this enum is the single extension point.
/// </summary>
public enum ArpCacheEntryState
{
    /// <summary>Learned at runtime from received ARP traffic. Ages out once its lifetime expires.</summary>
    Dynamic,

    /// <summary>Configured by an operator. Never expires; only removed explicitly.</summary>
    Static,
}
