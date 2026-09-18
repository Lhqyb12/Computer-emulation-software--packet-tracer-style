using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Dhcp;
using NetSim.Core.Udp;

namespace NetSim.Application.Dhcp;

/// <summary>Default <see cref="IDhcpServerService"/>. See the interface for the design; mirrors <c>DnsServerService</c>.</summary>
public sealed class DhcpServerService : IDhcpServerService
{
    private readonly IUdpDeliveryManager _udpDelivery;
    private readonly Dictionary<EntityId, IDhcpServer> _servers = [];

    public DhcpServerService(IUdpDeliveryManager udpDelivery)
    {
        ArgumentNullException.ThrowIfNull(udpDelivery);
        _udpDelivery = udpDelivery;
    }

    public void Start(NetworkDevice device, IDhcpServer server)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(server);

        if (_servers.ContainsKey(device.Id))
        {
            throw new DomainException($"Device '{device.Name}' is already running a DHCP service.");
        }

        _udpDelivery.Bind(device, DhcpProtocol.ServerPort);
        _servers[device.Id] = server;
    }

    public bool Stop(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!_servers.Remove(device.Id))
        {
            return false;
        }

        _udpDelivery.Unbind(device, DhcpProtocol.ServerPort);
        return true;
    }

    public bool IsRunning(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _servers.ContainsKey(device.Id);
    }

    public bool TryGetServer(NetworkDevice device, out IDhcpServer? server)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _servers.TryGetValue(device.Id, out server);
    }
}
