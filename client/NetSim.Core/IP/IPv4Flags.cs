namespace NetSim.Core.IP;

/// <summary>
/// The 3-bit fragmentation flags of an <see cref="IPv4Header"/>. This phase only <em>represents</em>
/// them - no fragmentation or reassembly is performed - so the data model is ready for a later
/// phase without any behaviour attached. Bit 0 (reserved) is not modelled.
/// </summary>
[Flags]
public enum IPv4Flags
{
    /// <summary>No flags set.</summary>
    None = 0,

    /// <summary>DF - the packet must not be fragmented.</summary>
    DontFragment = 1 << 0,

    /// <summary>MF - more fragments follow this one.</summary>
    MoreFragments = 1 << 1,
}
