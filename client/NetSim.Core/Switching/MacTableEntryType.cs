namespace NetSim.Core.Switching;

/// <summary>
/// How a <see cref="MacAddressTableEntry"/> came to be in the table. Phase 25 only ever creates
/// <see cref="Dynamic"/> entries (learned from received frames, subject to aging); <see cref="Static"/>
/// is defined now so a later "configure a permanent MAC entry" feature is an additive change to the
/// table rather than a redesign - the switching engine already branches on this value.
/// </summary>
public enum MacTableEntryType
{
    /// <summary>Learned from a received frame's source address. Ages out when unused (see <see cref="MacAddressTable.AgingTime"/>).</summary>
    Dynamic,

    /// <summary>Operator-configured. Never ages, always wins over a dynamic learn. Not created by Phase 25.</summary>
    Static,
}
