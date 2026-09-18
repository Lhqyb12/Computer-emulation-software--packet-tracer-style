using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Vlans;

/// <summary>
/// One configured VLAN in a switch's <see cref="VlanRegistry"/>: a logical Layer 2 broadcast
/// domain identified by its <see cref="Id"/>, with an operator-facing <see cref="Name"/>, an
/// optional <see cref="Description"/> and an <see cref="IsActive"/> flag (an inactive VLAN still
/// exists in the database but the switch does not forward its traffic).
///
/// This is <em>configuration</em>, not runtime state - it is persisted with the switch. The
/// dynamic <c>(VLAN, MAC) -&gt; port</c> learning lives in the MAC address table and is never
/// saved.
/// </summary>
public sealed class Vlan
{
    private const int MaxNameLength = 64;

    public Vlan(VlanId id, string? name = null, string? description = null, bool isActive = true)
    {
        Id = id;
        Name = NormaliseName(name) ?? DefaultNameFor(id);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = isActive;
    }

    public VlanId Id { get; }

    /// <summary>Operator-facing name (e.g. "Students", "Servers"). Never null or blank.</summary>
    public string Name { get; private set; }

    /// <summary>Optional free-text description, or null.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Whether the switch forwards this VLAN's traffic. A newly created VLAN is active. An inactive
    /// VLAN is still a defined broadcast domain (ports may reference it) but frames in it are
    /// dropped by the switching engine.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>The default VLAN (VLAN 1) is present in every switch and cannot be removed.</summary>
    public bool IsDefault => Id == VlanId.Default;

    public void Rename(string name)
    {
        Name = NormaliseName(name)
            ?? throw new DomainException("A VLAN name cannot be empty.");
    }

    public void SetDescription(string? description) =>
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>The Cisco-style default name for a VLAN: "default" for VLAN 1, "VLAN0010" otherwise.</summary>
    public static string DefaultNameFor(VlanId id) =>
        id == VlanId.Default ? "default" : $"VLAN{id.Value:D4}";

    private static string? NormaliseName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();
        return trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
    }

    public override string ToString() => $"VLAN {Id} ({Name})";
}
