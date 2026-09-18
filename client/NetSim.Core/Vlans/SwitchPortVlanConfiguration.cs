namespace NetSim.Core.Vlans;

/// <summary>
/// The VLAN configuration of one switch port: its <see cref="Mode"/> plus the fields that mode
/// needs - the <see cref="AccessVlan"/> for an access port, or the <see cref="NativeVlan"/> and
/// <see cref="AllowedVlans"/> for a trunk. Every switch port has one of these; a brand-new port is
/// an access port in <see cref="VlanId.Default"/> (VLAN 1), which is why a topology built before
/// VLAN support behaves exactly as it did - one flat broadcast domain.
///
/// It is a plain configuration holder. Validation that a referenced VLAN actually exists, MAC
/// table invalidation when the VLAN changes, and event raising all live on <see cref="Devices"/>'
/// switch, which owns both this and the MAC table. Switching the <see cref="Mode"/> resets the
/// fields that belong to the other mode so the port can never be left in an ambiguous state.
/// </summary>
public sealed class SwitchPortVlanConfiguration
{
    // null => "all VLANs allowed" (the trunk default). A non-null set is the explicit allow-list.
    private HashSet<VlanId>? _allowedVlans;

    public SwitchPortMode Mode { get; private set; } = SwitchPortMode.Access;

    /// <summary>The single VLAN an access port carries. Meaningful only when <see cref="Mode"/> is <see cref="SwitchPortMode.Access"/>.</summary>
    public VlanId AccessVlan { get; private set; } = VlanId.Default;

    /// <summary>The VLAN a trunk associates with untagged frames. Meaningful only when <see cref="Mode"/> is <see cref="SwitchPortMode.Trunk"/>.</summary>
    public VlanId NativeVlan { get; private set; } = VlanId.Default;

    /// <summary>
    /// True when this trunk carries every VLAN (no explicit allow-list has been set). An access
    /// port is never in this state.
    /// </summary>
    public bool AllowsAllVlans => Mode == SwitchPortMode.Trunk && _allowedVlans is null;

    /// <summary>
    /// The explicit set of VLANs this trunk carries, or an empty list when it carries all of them
    /// (<see cref="AllowsAllVlans"/>) or when the port is an access port. Sorted by id.
    /// </summary>
    public IReadOnlyList<VlanId> AllowedVlans =>
        _allowedVlans is null ? [] : _allowedVlans.OrderBy(v => v).ToList();

    public bool IsAccess => Mode == SwitchPortMode.Access;

    public bool IsTrunk => Mode == SwitchPortMode.Trunk;

    /// <summary>Makes this an access port in <paramref name="vlan"/>, discarding any trunk configuration.</summary>
    public void ConfigureAsAccess(VlanId vlan)
    {
        Mode = SwitchPortMode.Access;
        AccessVlan = vlan;
        NativeVlan = VlanId.Default;
        _allowedVlans = null;
    }

    /// <summary>
    /// Makes this a trunk. <paramref name="nativeVlan"/> defaults to VLAN 1; <paramref name="allowedVlans"/>
    /// <c>null</c> means "carry all VLANs". Discards any access configuration.
    /// </summary>
    public void ConfigureAsTrunk(VlanId? nativeVlan = null, IEnumerable<VlanId>? allowedVlans = null)
    {
        Mode = SwitchPortMode.Trunk;
        AccessVlan = VlanId.Default;
        NativeVlan = nativeVlan ?? VlanId.Default;
        SetAllowedVlans(allowedVlans);
    }

    /// <summary>Sets the access VLAN. No-op unless the port is in access mode.</summary>
    public void SetAccessVlan(VlanId vlan)
    {
        if (Mode == SwitchPortMode.Access)
        {
            AccessVlan = vlan;
        }
    }

    /// <summary>Sets the trunk native VLAN. No-op unless the port is in trunk mode.</summary>
    public void SetNativeVlan(VlanId vlan)
    {
        if (Mode == SwitchPortMode.Trunk)
        {
            NativeVlan = vlan;
        }
    }

    /// <summary>
    /// Replaces the trunk allow-list. <c>null</c> restores "all VLANs allowed". No-op unless the
    /// port is in trunk mode.
    /// </summary>
    public void SetAllowedVlans(IEnumerable<VlanId>? vlans)
    {
        if (Mode != SwitchPortMode.Trunk)
        {
            return;
        }

        _allowedVlans = vlans is null ? null : [.. vlans];
    }

    /// <summary>Adds <paramref name="vlan"/> to the trunk allow-list (creating an explicit list if the trunk was carrying all VLANs).</summary>
    public void AllowVlan(VlanId vlan)
    {
        if (Mode != SwitchPortMode.Trunk)
        {
            return;
        }

        _allowedVlans ??= [];
        _allowedVlans.Add(vlan);
    }

    /// <summary>
    /// Removes <paramref name="vlan"/> from the trunk allow-list. If the trunk was carrying all
    /// VLANs this first materialises the "all" set minus <paramref name="vlan"/> is *not* done -
    /// instead the caller is expected to have set an explicit list; removing from an "all" trunk is
    /// a no-op by design (there is no finite "all" to subtract from without a VLAN registry).
    /// </summary>
    public void RemoveAllowedVlan(VlanId vlan) => _allowedVlans?.Remove(vlan);

    /// <summary>
    /// Whether a frame in <paramref name="vlan"/> may cross this port. An access port carries only
    /// its access VLAN; a trunk carries every VLAN in its allow-list (or all VLANs when no list is
    /// set).
    /// </summary>
    public bool CarriesVlan(VlanId vlan) => Mode switch
    {
        SwitchPortMode.Access => AccessVlan == vlan,
        SwitchPortMode.Trunk => _allowedVlans is null || _allowedVlans.Contains(vlan),
        _ => false,
    };

    /// <summary>Convenience alias of <see cref="CarriesVlan"/> reading naturally for trunk configuration code.</summary>
    public bool IsVlanAllowed(VlanId vlan) => CarriesVlan(vlan);
}
