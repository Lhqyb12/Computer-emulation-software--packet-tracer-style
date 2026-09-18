namespace NetSim.Core.Vlans;

/// <summary>
/// A simulated IEEE 802.1Q VLAN tag, as it rides on the wire between two trunk ports. Like the
/// rest of the Ethernet model this is a behavioural representation, not a byte-for-byte codec:
/// the <see cref="Tpid"/> is fixed at 0x8100 and only the fields the switching engine actually
/// uses are modelled - the <see cref="VlanId"/>, plus the <see cref="PriorityCodePoint"/> and
/// <see cref="DropEligible"/> bits (carried for completeness; no QoS behaviour depends on them
/// in this phase).
///
/// A frame with no tag is "untagged" - see <see cref="Ethernet.EthernetFrame.VlanTag"/>, which is
/// <c>null</c> for an untagged frame.
/// </summary>
public readonly record struct VlanTag
{
    /// <summary>The 802.1Q Tag Protocol Identifier - always 0x8100 for a C-VLAN tag.</summary>
    public const ushort Tpid = 0x8100;

    public VlanTag(VlanId vlanId, byte priorityCodePoint = 0, bool dropEligible = false)
    {
        if (priorityCodePoint > 7)
        {
            throw new Common.Exceptions.DomainException(
                $"802.1p priority code point {priorityCodePoint} is out of range (0-7).");
        }

        VlanId = vlanId;
        PriorityCodePoint = priorityCodePoint;
        DropEligible = dropEligible;
    }

    /// <summary>The VLAN this frame belongs to.</summary>
    public VlanId VlanId { get; }

    /// <summary>802.1p class-of-service value (0-7). Not acted on in this phase.</summary>
    public byte PriorityCodePoint { get; }

    /// <summary>Drop Eligible Indicator. Not acted on in this phase.</summary>
    public bool DropEligible { get; }

    /// <summary>A plain tag for <paramref name="vlanId"/> with default priority and DEI clear.</summary>
    public static VlanTag For(VlanId vlanId) => new(vlanId);

    public override string ToString() =>
        $"802.1Q vlan={VlanId} pcp={PriorityCodePoint}{(DropEligible ? " dei" : string.Empty)}";
}
