namespace NetSim.Core.Vlans;

/// <summary>Payload for <see cref="VlanRegistry"/>'s create / remove / rename notifications.</summary>
public sealed class VlanRegistryEventArgs : EventArgs
{
    public VlanRegistryEventArgs(Vlan vlan, string? detail = null)
    {
        Vlan = vlan;
        Detail = detail;
    }

    public Vlan Vlan { get; }

    public string? Detail { get; }
}
