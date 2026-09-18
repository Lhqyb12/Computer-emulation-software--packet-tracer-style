using NetSim.Core.Devices;
using NetSim.Core.Dns;

namespace NetSim.Application.Dns;

/// <summary>
/// Hosts a simulated DNS service on a device (brief sections 16 &amp; 43): binds the device to
/// <see cref="DnsProtocol.Port"/> on UDP (and, optionally, TCP - brief section 5) through the
/// existing transport layers and associates the device with the <see cref="IDnsServer"/> instance
/// that answers its queries. This is the "is a DNS service running on this device" seam
/// <c>IDnsResolver</c> consults when it delivers a query to a device's port 53.
/// </summary>
public interface IDnsServerService
{
    /// <summary>
    /// Starts <paramref name="server"/> listening on <paramref name="device"/>'s UDP (and, if
    /// <paramref name="enableTcp"/>, TCP) port 53. Throws <see cref="Core.Common.Exceptions.DomainException"/>
    /// if the device is already running a DNS service.
    /// </summary>
    void Start(NetworkDevice device, IDnsServer server, bool enableTcp = true);

    /// <summary>Stops the DNS service on <paramref name="device"/> and releases its port bindings. Returns false when none was running.</summary>
    bool Stop(NetworkDevice device);

    bool IsRunning(NetworkDevice device);

    /// <summary>The <see cref="IDnsServer"/> currently running on <paramref name="device"/>, if any.</summary>
    bool TryGetServer(NetworkDevice device, out IDnsServer? server);
}
