using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Vlans;

/// <summary>
/// Default <see cref="IVlanRegistry"/>: a lock-guarded dictionary keyed by <see cref="VlanId"/>,
/// seeded with the default VLAN (1). Held by <see cref="Devices.Switch.Vlans"/>. A plain
/// configuration container - no forwarding logic, no clock - the switching engine reads it but
/// never mutates it.
/// </summary>
public sealed class VlanRegistry : IVlanRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<VlanId, Vlan> _vlans = [];

    public VlanRegistry()
    {
        var @default = new Vlan(VlanId.Default);
        _vlans[@default.Id] = @default;
    }

    public event EventHandler<VlanRegistryEventArgs>? VlanCreated;

    public event EventHandler<VlanRegistryEventArgs>? VlanRemoved;

    public IReadOnlyList<Vlan> GetAllVlans()
    {
        lock (_gate)
        {
            return _vlans.Values.OrderBy(v => v.Id).ToList();
        }
    }

    public Vlan? GetVlan(VlanId id)
    {
        lock (_gate)
        {
            return _vlans.GetValueOrDefault(id);
        }
    }

    public bool Exists(VlanId id)
    {
        lock (_gate)
        {
            return _vlans.ContainsKey(id);
        }
    }

    public Vlan CreateVlan(VlanId id, string? name = null, string? description = null)
    {
        lock (_gate)
        {
            if (_vlans.ContainsKey(id))
            {
                throw new DomainException($"VLAN {id} already exists on this switch.");
            }

            var vlan = new Vlan(id, name, description);
            _vlans[id] = vlan;
            VlanCreated?.Invoke(this, new VlanRegistryEventArgs(vlan, $"VLAN {id} ({vlan.Name}) created."));
            return vlan;
        }
    }

    public bool TryCreateVlan(VlanId id, out Vlan vlan, string? name = null, string? description = null)
    {
        lock (_gate)
        {
            if (_vlans.TryGetValue(id, out var existing))
            {
                vlan = existing;
                return false;
            }

            vlan = new Vlan(id, name, description);
            _vlans[id] = vlan;
        }

        VlanCreated?.Invoke(this, new VlanRegistryEventArgs(vlan, $"VLAN {id} ({vlan.Name}) created."));
        return true;
    }

    public bool RemoveVlan(VlanId id)
    {
        Vlan removed;
        lock (_gate)
        {
            if (id == VlanId.Default || !_vlans.TryGetValue(id, out removed!))
            {
                return false;
            }

            _vlans.Remove(id);
        }

        VlanRemoved?.Invoke(this, new VlanRegistryEventArgs(removed, $"VLAN {id} ({removed.Name}) removed."));
        return true;
    }
}
