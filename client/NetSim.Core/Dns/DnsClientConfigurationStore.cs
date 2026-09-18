using NetSim.Core.Common;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Dns;

/// <summary>Default <see cref="IDnsClientConfigurationStore"/>. A plain per-device table.</summary>
public sealed class DnsClientConfigurationStore : IDnsClientConfigurationStore
{
    private readonly Dictionary<EntityId, List<IPv4Address>> _servers = [];

    public IReadOnlyList<IPv4Address> GetServers(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _servers.TryGetValue(device.Id, out var servers) ? servers.AsReadOnly() : Array.Empty<IPv4Address>();
    }

    public void SetServers(NetworkDevice device, IReadOnlyList<IPv4Address> servers)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(servers);
        _servers[device.Id] = servers.Distinct().ToList();
    }

    public void AddServer(NetworkDevice device, IPv4Address server)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (!_servers.TryGetValue(device.Id, out var servers))
        {
            servers = [];
            _servers[device.Id] = servers;
        }

        if (!servers.Contains(server))
        {
            servers.Add(server);
        }
    }

    public bool RemoveServer(NetworkDevice device, IPv4Address server)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _servers.TryGetValue(device.Id, out var servers) && servers.Remove(server);
    }

    public void ClearServers(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _servers.Remove(device.Id);
    }
}
