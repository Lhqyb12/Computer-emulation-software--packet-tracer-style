namespace NetSim.Core.Dns;

/// <summary>
/// Tracks a resolver's outstanding DNS transaction ids (brief section 32): create one for a new
/// query, and complete (remove) it when a response claiming that id arrives - so a response can
/// never be matched to a query it does not belong to, and a stray/duplicate response is rejected
/// rather than accepted. A plain data structure, like <see cref="Udp.IUdpDeliveryManager"/> - no
/// protocol logic, no events.
/// </summary>
public sealed class DnsTransactionTracker
{
    private readonly HashSet<ushort> _outstanding = [];

    /// <summary>Generates a fresh transaction id not already outstanding and records it as in-flight.</summary>
    public ushort Begin()
    {
        ushort id;
        do
        {
            id = (ushort)Random.Shared.Next(1, ushort.MaxValue + 1);
        }
        while (!_outstanding.Add(id));

        return id;
    }

    public bool IsOutstanding(ushort transactionId) => _outstanding.Contains(transactionId);

    /// <summary>Completes (removes) a transaction. Returns false if it was not outstanding - a response with an unknown/already-completed id.</summary>
    public bool TryComplete(ushort transactionId) => _outstanding.Remove(transactionId);
}
