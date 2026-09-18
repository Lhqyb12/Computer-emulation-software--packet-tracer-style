using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Dns;
using NetSim.Core.Tcp;
using NetSim.Core.Udp;

namespace NetSim.Application.Dns;

/// <summary>Default <see cref="IDnsServerService"/>. See the interface for the overall design.</summary>
public sealed class DnsServerService : IDnsServerService
{
    private readonly IUdpDeliveryManager _udpDelivery;
    private readonly ITcpConnectionManager _tcpConnections;
    private readonly Dictionary<EntityId, IDnsServer> _servers = [];
    private readonly Dictionary<EntityId, bool> _tcpEnabled = [];

    public DnsServerService(IUdpDeliveryManager udpDelivery, ITcpConnectionManager tcpConnections)
    {
        ArgumentNullException.ThrowIfNull(udpDelivery);
        ArgumentNullException.ThrowIfNull(tcpConnections);
        _udpDelivery = udpDelivery;
        _tcpConnections = tcpConnections;
    }

    public void Start(NetworkDevice device, IDnsServer server, bool enableTcp = true)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(server);

        if (_servers.ContainsKey(device.Id))
        {
            throw new DomainException($"Device '{device.Name}' is already running a DNS service.");
        }

        _udpDelivery.Bind(device, DnsProtocol.Port);
        if (enableTcp)
        {
            _tcpConnections.Listen(device, DnsProtocol.Port);
        }

        _servers[device.Id] = server;
        _tcpEnabled[device.Id] = enableTcp;
    }

    public bool Stop(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!_servers.Remove(device.Id))
        {
            return false;
        }

        _udpDelivery.Unbind(device, DnsProtocol.Port);
        if (_tcpEnabled.Remove(device.Id, out var tcpWasEnabled) && tcpWasEnabled)
        {
            _tcpConnections.StopListening(device, DnsProtocol.Port);
        }

        return true;
    }

    public bool IsRunning(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _servers.ContainsKey(device.Id);
    }

    public bool TryGetServer(NetworkDevice device, out IDnsServer? server)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _servers.TryGetValue(device.Id, out server);
    }
}
