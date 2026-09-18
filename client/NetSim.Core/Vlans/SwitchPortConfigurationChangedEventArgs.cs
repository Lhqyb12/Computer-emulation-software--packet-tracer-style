using NetSim.Core.Networking;

namespace NetSim.Core.Vlans;

/// <summary>What changed about a switch port's VLAN configuration.</summary>
public enum SwitchPortConfigurationChange
{
    /// <summary>The port's <see cref="SwitchPortMode"/> changed (Access &lt;-&gt; Trunk).</summary>
    Mode,

    /// <summary>An access port's VLAN changed.</summary>
    AccessVlan,

    /// <summary>A trunk's native VLAN changed.</summary>
    NativeVlan,

    /// <summary>A trunk's allowed VLAN list changed.</summary>
    AllowedVlans,
}

/// <summary>
/// Raised by <see cref="Devices.Switch"/> after a port's VLAN configuration is changed through one
/// of its <c>ConfigurePort*</c> / <c>SetPort*</c> methods, so the UI and diagnostics can react
/// without polling. Carries the affected <see cref="Port"/>, what <see cref="Change"/> occurred and
/// a human-readable <see cref="Detail"/>.
/// </summary>
public sealed class SwitchPortConfigurationChangedEventArgs : EventArgs
{
    public SwitchPortConfigurationChangedEventArgs(
        NetworkInterface port,
        SwitchPortConfigurationChange change,
        SwitchPortVlanConfiguration configuration,
        string detail)
    {
        Port = port;
        Change = change;
        Configuration = configuration;
        Detail = detail;
    }

    public NetworkInterface Port { get; }

    public SwitchPortConfigurationChange Change { get; }

    public SwitchPortVlanConfiguration Configuration { get; }

    public string Detail { get; }
}
