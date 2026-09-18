using NetSim.Core.Networking;

namespace NetSim.Core.Switching;

/// <summary>What a <see cref="MacAddressTable.Learn"/> call did to the table.</summary>
public enum MacLearnOutcome
{
    /// <summary>Nothing changed (e.g. a dynamic learn that would have overwritten a static entry, or a non-unicast source).</summary>
    Unchanged,

    /// <summary>The address was not in the table and a new dynamic entry was created.</summary>
    Added,

    /// <summary>The address was already mapped to the same port; its <see cref="MacAddressTableEntry.LastSeenAt"/> was refreshed.</summary>
    Refreshed,

    /// <summary>The address was mapped to a different port and the entry was moved (see <see cref="MacLearnResult.PreviousPort"/>).</summary>
    Moved,
}

/// <summary>
/// The outcome of learning a source MAC: <see cref="Outcome"/>, the resulting <see cref="Entry"/>
/// (null only for <see cref="MacLearnOutcome.Unchanged"/>), and - for a
/// <see cref="MacLearnOutcome.Moved"/> - the <see cref="PreviousPort"/> the address used to be on.
/// </summary>
public readonly record struct MacLearnResult(
    MacLearnOutcome Outcome,
    MacAddressTableEntry? Entry,
    NetworkInterface? PreviousPort)
{
    public static MacLearnResult Unchanged { get; } = new(MacLearnOutcome.Unchanged, null, null);

    public bool Changed => Outcome is not MacLearnOutcome.Unchanged;

    public bool Moved => Outcome == MacLearnOutcome.Moved;
}
