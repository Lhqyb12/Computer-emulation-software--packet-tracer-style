using NetSim.Core.Dhcp;

namespace NetSim.Core.Tests.Dhcp;

/// <summary>
/// Deterministic <see cref="IDhcpTransactionIdSource"/> for tests (brief section 68): hands out
/// <c>0x1000, 0x1001, ...</c> so a test can assert exact transaction-id matching without depending
/// on randomness.
/// </summary>
public sealed class SequentialDhcpTransactionIdSource : IDhcpTransactionIdSource
{
    private uint _next;

    public SequentialDhcpTransactionIdSource(uint start = 0x1000) => _next = start;

    /// <summary>The id the next <see cref="Next"/> call will return.</summary>
    public uint Peek => _next;

    public uint Next() => _next++;
}
