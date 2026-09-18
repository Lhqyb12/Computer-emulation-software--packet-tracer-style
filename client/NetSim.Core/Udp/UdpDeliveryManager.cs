using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Transport;

namespace NetSim.Core.Udp;

/// <summary>Default <see cref="IUdpDeliveryManager"/>. A plain socket-table data structure - no protocol logic, no events (the layer raises those).</summary>
public sealed class UdpDeliveryManager : IUdpDeliveryManager
{
    private readonly Dictionary<(EntityId DeviceId, Port Port), UdpBinding> _bindings = [];

    public UdpBinding Bind(NetworkDevice device, Port port, Action<UdpReceivedDatagram>? onReceived = null)
    {
        ArgumentNullException.ThrowIfNull(device);

        var key = (device.Id, port);
        if (_bindings.ContainsKey(key))
        {
            throw new DomainException($"Device '{device.Name}' already has a UDP endpoint bound on port {port}.");
        }

        var binding = new UdpBinding(device, port, onReceived);
        _bindings[key] = binding;
        return binding;
    }

    public bool Unbind(NetworkDevice device, Port port)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _bindings.Remove((device.Id, port));
    }

    public bool IsBound(NetworkDevice device, Port port)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _bindings.ContainsKey((device.Id, port));
    }

    public bool TryFindBinding(NetworkDevice device, Port port, [NotNullWhen(true)] out UdpBinding? binding)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _bindings.TryGetValue((device.Id, port), out binding);
    }

    public IReadOnlyCollection<UdpBinding> GetBindings(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _bindings.Where(pair => pair.Key.DeviceId == device.Id).Select(pair => pair.Value).ToList().AsReadOnly();
    }

    public UdpDeliveryResult Deliver(NetworkDevice device, UdpDatagram datagram, IPv4Address remoteAddress, Port remotePort)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(datagram);

        if (!TryFindBinding(device, datagram.DestinationPort, out var binding))
        {
            return UdpDeliveryResult.PortUnavailable;
        }

        binding.Deliver(new UdpReceivedDatagram(datagram, remoteAddress, remotePort));
        return UdpDeliveryResult.Delivered(binding);
    }
}
