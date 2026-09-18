using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Vlans;

/// <summary>
/// A validated IEEE 802.1Q VLAN identifier - a 12-bit value in the range 1-4094, as an immutable
/// value object. VLAN 0 (priority-tagged frames) and VLAN 4095 (reserved) are not usable as
/// user VLANs and are rejected here, exactly as a real switch rejects them.
///
/// The switching code passes <see cref="VlanId"/> around rather than a bare <see cref="int"/> so a
/// VLAN can never be confused with a port number, a prefix length or any other integer, and so the
/// 1-4094 rule lives in one place.
/// </summary>
public readonly record struct VlanId : IComparable<VlanId>
{
    /// <summary>The lowest assignable VLAN id (also the default VLAN).</summary>
    public const int MinValue = 1;

    /// <summary>The highest assignable VLAN id. 4095 is reserved by 802.1Q and is not usable.</summary>
    public const int MaxValue = 4094;

    /// <summary>The conventional default / native VLAN id every access port starts in - VLAN 1.</summary>
    public const int DefaultValue = 1;

    public VlanId(int value)
    {
        if (!IsValid(value))
        {
            throw new DomainException(
                $"VLAN id {value} is out of range - a VLAN id must be between {MinValue} and {MaxValue}.");
        }

        Value = value;
    }

    /// <summary>The numeric VLAN id (1-4094).</summary>
    public int Value { get; }

    /// <summary>The default VLAN - VLAN 1. New access ports belong to this VLAN.</summary>
    public static VlanId Default { get; } = new(DefaultValue);

    /// <summary>True when <paramref name="value"/> is a usable 802.1Q VLAN id (1-4094).</summary>
    public static bool IsValid(int value) => value is >= MinValue and <= MaxValue;

    /// <summary>Non-throwing constructor. Returns false (and <c>default</c>) for an out-of-range value.</summary>
    public static bool TryCreate(int value, out VlanId vlanId)
    {
        if (IsValid(value))
        {
            vlanId = new VlanId(value);
            return true;
        }

        vlanId = default;
        return false;
    }

    public int CompareTo(VlanId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();
}
