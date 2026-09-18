using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;
using NetSim.Core.Switching;
using NetSim.Core.Vlans;

namespace NetSim.Core.Devices;

/// <summary>
/// A Layer 2 Ethernet switch. Phase 25 gave it a <see cref="MacAddressTable"/> - the per-switch
/// MAC-address-to-port forwarding table the <see cref="ISwitchingEngine"/> learns into and reads
/// from. Phase 26 makes it VLAN-aware:
///
/// <list type="bullet">
/// <item>every switch owns a <see cref="Vlans"/> database (seeded with the default VLAN, 1);</item>
/// <item>every Ethernet port has a <see cref="SwitchPortVlanConfiguration"/> - access (one VLAN,
/// untagged) or trunk (many VLANs, 802.1Q-tagged) - reachable through
/// <see cref="GetPortVlanConfiguration"/>. A brand-new port is an access port in VLAN 1, so a
/// topology built before VLAN support keeps behaving as one flat broadcast domain;</item>
/// <item>the MAC address table is keyed by <c>(VLAN, MAC)</c>, so the same address can live in
/// two VLANs at once and traffic never crosses a VLAN boundary.</item>
/// </list>
///
/// STP is still intentionally not implemented (loop safety in
/// <see cref="SwitchedSegmentService"/> is simulator stability only), and there is deliberately
/// <b>no inter-VLAN routing</b> - a switch never forwards between two VLANs. VLAN configuration is
/// persisted; the dynamic MAC table is runtime state and is never saved.
/// </summary>
public class Switch : NetworkDevice
{
    private readonly Dictionary<EntityId, SwitchPortVlanConfiguration> _portConfigurations = [];

    public Switch(string name)
        : base(name, DeviceType.Switch)
    {
    }

    // Persistence rehydration only - see NetworkDeviceFactory.RehydrateWithoutDefaults.
    internal Switch(EntityId id, string name)
        : base(id, name, DeviceType.Switch)
    {
    }

    /// <summary>
    /// This switch's Layer 2 forwarding table, keyed by <c>(VLAN, MAC) -&gt; port</c>.
    /// Runtime state - reset on load, never saved. See <see cref="ISwitchingEngine"/>.
    /// </summary>
    public MacAddressTable MacAddressTable { get; } = new();

    /// <summary>This switch's VLAN database. Always contains the default VLAN (1). Persisted with the switch.</summary>
    public VlanRegistry Vlans { get; } = new();

    /// <summary>Raised after a port's VLAN configuration is changed through this switch.</summary>
    public event EventHandler<SwitchPortConfigurationChangedEventArgs>? PortConfigurationChanged;

    /// <summary>
    /// The VLAN configuration of every Ethernet port on this switch, in port order. Ports that have
    /// never been configured report their default (access, VLAN 1) configuration.
    /// </summary>
    public IReadOnlyList<(NetworkInterface Port, SwitchPortVlanConfiguration Configuration)> PortVlanConfigurations =>
        Interfaces.Where(p => p.SupportsEthernet)
            .Select(p => (p, GetPortVlanConfiguration(p)))
            .ToList();

    /// <summary>
    /// The live VLAN configuration for <paramref name="port"/>, creating the default (access,
    /// VLAN 1) configuration on first access. Throws when the port does not belong to this switch
    /// or is not an Ethernet port.
    /// </summary>
    public SwitchPortVlanConfiguration GetPortVlanConfiguration(NetworkInterface port)
    {
        RequireOwnEthernetPort(port);
        if (!_portConfigurations.TryGetValue(port.Id, out var configuration))
        {
            configuration = new SwitchPortVlanConfiguration();
            _portConfigurations[port.Id] = configuration;
        }

        return configuration;
    }

    /// <summary>Convenience wrapper around <see cref="VlanRegistry.CreateVlan"/>.</summary>
    public Vlan CreateVlan(VlanId id, string? name = null, string? description = null) =>
        Vlans.CreateVlan(id, name, description);

    /// <summary>
    /// Removes a VLAN. The default VLAN cannot be removed, and a VLAN that is still referenced by a
    /// port (as an access VLAN, a trunk native VLAN or a trunk allowed VLAN) cannot be removed -
    /// move those ports to another VLAN first. This is the safe operation the UI calls; it never
    /// leaves a dangling reference.
    /// </summary>
    public bool RemoveVlan(VlanId id)
    {
        if (id == VlanId.Default)
        {
            throw new DomainException("The default VLAN (1) cannot be removed.");
        }

        var referencingPort = FindPortReferencing(id);
        if (referencingPort is not null)
        {
            throw new DomainException(
                $"VLAN {id} is still in use by port '{referencingPort.Name}'. Reassign that port before removing the VLAN.");
        }

        return Vlans.RemoveVlan(id);
    }

    /// <summary>True when any port on this switch references <paramref name="id"/> in its configuration.</summary>
    public bool IsVlanInUse(VlanId id) => FindPortReferencing(id) is not null;

    /// <summary>
    /// Switches a port between access and trunk mode. Changing mode resets the fields that belong
    /// to the other mode (an access port loses its trunk allow-list; a trunk gets native VLAN 1
    /// and "all VLANs allowed") and flushes every MAC address learned on the port so stale entries
    /// cannot bridge traffic after the change.
    /// </summary>
    public void SetPortMode(NetworkInterface port, SwitchPortMode mode)
    {
        var config = GetPortVlanConfiguration(port);
        if (config.Mode == mode)
        {
            return;
        }

        if (mode == SwitchPortMode.Access)
        {
            config.ConfigureAsAccess(VlanId.Default);
        }
        else
        {
            config.ConfigureAsTrunk();
        }

        FlushPort(port);
        RaisePortChanged(port, SwitchPortConfigurationChange.Mode, config,
            $"Port '{port.Name}' changed to {mode} mode.");
    }

    /// <summary>Makes <paramref name="port"/> an access port in <paramref name="vlan"/> (which must exist).</summary>
    public void ConfigureAccessPort(NetworkInterface port, VlanId vlan)
    {
        RequireVlanExists(vlan);
        var config = GetPortVlanConfiguration(port);
        var wasTrunk = config.IsTrunk;
        config.ConfigureAsAccess(vlan);
        FlushPort(port);
        RaisePortChanged(port, wasTrunk ? SwitchPortConfigurationChange.Mode : SwitchPortConfigurationChange.AccessVlan,
            config, $"Port '{port.Name}' is now an access port in VLAN {vlan}.");
    }

    /// <summary>Changes an access port's VLAN. The port must already be in access mode and the VLAN must exist.</summary>
    public void SetAccessVlan(NetworkInterface port, VlanId vlan)
    {
        RequireVlanExists(vlan);
        var config = GetPortVlanConfiguration(port);
        if (!config.IsAccess)
        {
            throw new DomainException($"Port '{port.Name}' is a trunk; set it to access mode before assigning an access VLAN.");
        }

        if (config.AccessVlan == vlan)
        {
            return;
        }

        var previous = config.AccessVlan;
        config.SetAccessVlan(vlan);
        FlushPort(port);
        RaisePortChanged(port, SwitchPortConfigurationChange.AccessVlan, config,
            $"Port '{port.Name}' changed from VLAN {previous} to VLAN {vlan}.");
    }

    /// <summary>
    /// Makes <paramref name="port"/> a trunk. <paramref name="nativeVlan"/> defaults to VLAN 1;
    /// <paramref name="allowedVlans"/> <c>null</c> means "carry all VLANs". Every referenced VLAN
    /// must exist.
    /// </summary>
    public void ConfigureTrunkPort(NetworkInterface port, VlanId? nativeVlan = null, IEnumerable<VlanId>? allowedVlans = null)
    {
        var native = nativeVlan ?? VlanId.Default;
        RequireVlanExists(native);
        var allowedList = allowedVlans?.ToList();
        if (allowedList is not null)
        {
            foreach (var v in allowedList)
            {
                RequireVlanExists(v);
            }
        }

        var config = GetPortVlanConfiguration(port);
        config.ConfigureAsTrunk(native, allowedList);
        FlushPort(port);
        RaisePortChanged(port, SwitchPortConfigurationChange.Mode, config,
            $"Port '{port.Name}' is now a trunk (native VLAN {native}).");
    }

    /// <summary>Sets a trunk's native VLAN. The port must already be a trunk and the VLAN must exist.</summary>
    public void SetTrunkNativeVlan(NetworkInterface port, VlanId vlan)
    {
        RequireVlanExists(vlan);
        var config = GetPortVlanConfiguration(port);
        if (!config.IsTrunk)
        {
            throw new DomainException($"Port '{port.Name}' is not a trunk.");
        }

        if (config.NativeVlan == vlan)
        {
            return;
        }

        config.SetNativeVlan(vlan);
        FlushPort(port);
        RaisePortChanged(port, SwitchPortConfigurationChange.NativeVlan, config,
            $"Trunk '{port.Name}' native VLAN is now {vlan}.");
    }

    /// <summary>
    /// Replaces a trunk's allowed VLAN list. <c>null</c> restores "all VLANs allowed". The port
    /// must already be a trunk and every listed VLAN must exist.
    /// </summary>
    public void SetTrunkAllowedVlans(NetworkInterface port, IEnumerable<VlanId>? vlans)
    {
        var config = GetPortVlanConfiguration(port);
        if (!config.IsTrunk)
        {
            throw new DomainException($"Port '{port.Name}' is not a trunk.");
        }

        var list = vlans?.ToList();
        if (list is not null)
        {
            foreach (var v in list)
            {
                RequireVlanExists(v);
            }
        }

        config.SetAllowedVlans(list);
        FlushPort(port);
        RaisePortChanged(port, SwitchPortConfigurationChange.AllowedVlans, config,
            $"Trunk '{port.Name}' allowed VLANs updated.");
    }

    /// <summary>Adds one VLAN to a trunk's allowed list. The port must be a trunk and the VLAN must exist.</summary>
    public void AllowVlanOnTrunk(NetworkInterface port, VlanId vlan)
    {
        RequireVlanExists(vlan);
        var config = GetPortVlanConfiguration(port);
        if (!config.IsTrunk)
        {
            throw new DomainException($"Port '{port.Name}' is not a trunk.");
        }

        config.AllowVlan(vlan);
        RaisePortChanged(port, SwitchPortConfigurationChange.AllowedVlans, config,
            $"Trunk '{port.Name}' now allows VLAN {vlan}.");
    }

    /// <summary>Removes one VLAN from a trunk's allowed list and flushes that VLAN's entries learned on the port.</summary>
    public void RemoveAllowedVlanFromTrunk(NetworkInterface port, VlanId vlan)
    {
        var config = GetPortVlanConfiguration(port);
        if (!config.IsTrunk)
        {
            throw new DomainException($"Port '{port.Name}' is not a trunk.");
        }

        config.RemoveAllowedVlan(vlan);
        MacAddressTable.RemoveEntriesForVlanOnPort(vlan, port.Id);
        RaisePortChanged(port, SwitchPortConfigurationChange.AllowedVlans, config,
            $"Trunk '{port.Name}' no longer allows VLAN {vlan}.");
    }

    /// <summary>
    /// Persistence rehydration only - returns a fresh, default <see cref="SwitchPortVlanConfiguration"/>
    /// for <paramref name="port"/> for the caller to populate from saved data, without flushing the
    /// MAC table or raising events (there is no running simulation during a load). Ordinary code
    /// configures ports through <see cref="ConfigureAccessPort"/> / <see cref="ConfigureTrunkPort"/>.
    /// </summary>
    public SwitchPortVlanConfiguration RestorePortConfiguration(NetworkInterface port)
    {
        RequireOwnEthernetPort(port);
        var configuration = new SwitchPortVlanConfiguration();
        _portConfigurations[port.Id] = configuration;
        return configuration;
    }

    private void FlushPort(NetworkInterface port) => MacAddressTable.RemoveEntriesForPort(port.Id);

    private void RaisePortChanged(
        NetworkInterface port, SwitchPortConfigurationChange change, SwitchPortVlanConfiguration config, string detail) =>
        PortConfigurationChanged?.Invoke(this, new SwitchPortConfigurationChangedEventArgs(port, change, config, detail));

    private NetworkInterface? FindPortReferencing(VlanId id)
    {
        foreach (var (port, config) in PortVlanConfigurations)
        {
            var referenced = config.IsAccess
                ? config.AccessVlan == id
                : config.NativeVlan == id || (!config.AllowsAllVlans && config.AllowedVlans.Contains(id));

            if (referenced)
            {
                return port;
            }
        }

        return null;
    }

    private void RequireOwnEthernetPort(NetworkInterface port)
    {
        ArgumentNullException.ThrowIfNull(port);
        if (port.Device.Id != Id)
        {
            throw new DomainException($"Port '{port.Name}' does not belong to switch '{Name}'.");
        }

        if (!port.SupportsEthernet)
        {
            throw new DomainException($"Port '{port.Name}' on switch '{Name}' is not an Ethernet port.");
        }
    }

    private void RequireVlanExists(VlanId vlan)
    {
        if (!Vlans.Exists(vlan))
        {
            throw new DomainException($"VLAN {vlan} is not configured on switch '{Name}'. Create it before assigning it to a port.");
        }
    }
}
