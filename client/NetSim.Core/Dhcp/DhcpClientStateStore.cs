using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Common;
using NetSim.Core.Networking;

namespace NetSim.Core.Dhcp;

/// <summary>Default <see cref="IDhcpClientStateStore"/>. A plain per-interface table, no protocol logic.</summary>
public sealed class DhcpClientStateStore : IDhcpClientStateStore
{
    private readonly Dictionary<EntityId, DhcpClientBinding> _bindings = [];

    public DhcpClientBinding GetOrCreate(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);
        if (!_bindings.TryGetValue(networkInterface.Id, out var binding))
        {
            binding = new DhcpClientBinding(networkInterface.Id);
            _bindings[networkInterface.Id] = binding;
        }

        return binding;
    }

    public bool TryGet(NetworkInterface networkInterface, [NotNullWhen(true)] out DhcpClientBinding? binding)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);
        return _bindings.TryGetValue(networkInterface.Id, out binding);
    }

    public bool TryGet(EntityId interfaceId, [NotNullWhen(true)] out DhcpClientBinding? binding) =>
        _bindings.TryGetValue(interfaceId, out binding);

    public bool IsDhcpManaged(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);
        return _bindings.TryGetValue(networkInterface.Id, out var binding) && binding.IsDhcpManaged;
    }

    public bool Remove(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);
        return _bindings.Remove(networkInterface.Id);
    }
}
