namespace NetSim.Core.Vlans;

/// <summary>
/// One switch's VLAN database - the set of VLANs configured on it. Every switch has its own
/// (there is no network-wide VLAN database; VTP is out of scope). VLAN 1, the default VLAN, is
/// always present and cannot be removed.
///
/// This is the seam the switching engine and the UI use so neither talks to a raw dictionary,
/// mirroring how <c>IMacAddressTable</c> relates to the switching engine.
/// </summary>
public interface IVlanRegistry
{
    /// <summary>Every configured VLAN, ordered by id (VLAN 1 first).</summary>
    IReadOnlyList<Vlan> GetAllVlans();

    /// <summary>The VLAN with <paramref name="id"/>, or null when it is not configured.</summary>
    Vlan? GetVlan(VlanId id);

    /// <summary>True when a VLAN with <paramref name="id"/> is configured.</summary>
    bool Exists(VlanId id);

    /// <summary>
    /// Creates a VLAN. Throws <see cref="Common.Exceptions.DomainException"/> if <paramref name="id"/>
    /// is already configured. Returns the created VLAN.
    /// </summary>
    Vlan CreateVlan(VlanId id, string? name = null, string? description = null);

    /// <summary>Non-throwing <see cref="CreateVlan"/>: returns false when the VLAN already exists.</summary>
    bool TryCreateVlan(VlanId id, out Vlan vlan, string? name = null, string? description = null);

    /// <summary>
    /// Removes the VLAN with <paramref name="id"/>. The default VLAN (1) cannot be removed
    /// (returns false). Returns false when the VLAN is not configured. This is the raw database
    /// operation - it does not check whether any port still references the VLAN; the switch's
    /// <c>RemoveVlan</c> does that.
    /// </summary>
    bool RemoveVlan(VlanId id);

    /// <summary>Raised after a VLAN is created.</summary>
    event EventHandler<VlanRegistryEventArgs>? VlanCreated;

    /// <summary>Raised after a VLAN is removed.</summary>
    event EventHandler<VlanRegistryEventArgs>? VlanRemoved;
}
