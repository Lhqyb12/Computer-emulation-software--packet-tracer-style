namespace NetSim.Core.Dhcp;

/// <summary>
/// Supplies the 32-bit transaction id a DHCP client stamps on a new DISCOVER/REQUEST exchange
/// (brief section 23). Injectable so a test can make the id deterministic rather than depending on
/// randomness - the same reason <c>IInitialSequenceNumberGenerator</c> exists for TCP.
/// </summary>
public interface IDhcpTransactionIdSource
{
    /// <summary>A fresh transaction id for a new exchange.</summary>
    uint Next();
}

/// <summary>Default <see cref="IDhcpTransactionIdSource"/> - a random non-zero id per exchange.</summary>
public sealed class RandomDhcpTransactionIdSource : IDhcpTransactionIdSource
{
    public uint Next() => (uint)Random.Shared.NextInt64(1, uint.MaxValue);
}
