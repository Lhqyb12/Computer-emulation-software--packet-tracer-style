using NetSim.Core.Dhcp;

namespace NetSim.Application.Tests.TestSupport;

/// <summary>Deterministic <see cref="IDhcpTransactionIdSource"/> for tests (brief section 68) - hands out <c>0x1000, 0x1001, ...</c>.</summary>
internal sealed class SequentialDhcpTransactionIdSource : IDhcpTransactionIdSource
{
    private uint _next;

    public SequentialDhcpTransactionIdSource(uint start = 0x1000) => _next = start;

    public uint Peek => _next;

    public uint Next() => _next++;
}
