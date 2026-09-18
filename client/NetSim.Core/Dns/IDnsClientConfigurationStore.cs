using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Dns;

/// <summary>
/// A device's configured DNS server addresses (brief section 20), host-scoped like
/// <see cref="Udp.IUdpDeliveryManager"/> rather than a field on <see cref="NetworkDevice"/>. Purely
/// manual/programmatic configuration in this phase - Phase 24 (DHCP) will later be able to populate
/// this same store automatically without any change here (brief section 59).
/// </summary>
public interface IDnsClientConfigurationStore
{
    /// <summary>The device's configured DNS servers, in preference order. Empty (never null) when none are configured.</summary>
    IReadOnlyList<IPv4Address> GetServers(NetworkDevice device);

    /// <summary>Replaces the device's configured DNS servers.</summary>
    void SetServers(NetworkDevice device, IReadOnlyList<IPv4Address> servers);

    /// <summary>Appends one server if it is not already configured.</summary>
    void AddServer(NetworkDevice device, IPv4Address server);

    bool RemoveServer(NetworkDevice device, IPv4Address server);

    void ClearServers(NetworkDevice device);
}
