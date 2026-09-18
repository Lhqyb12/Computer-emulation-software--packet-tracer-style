using NetSim.Core.Devices;
using NetSim.Core.Dhcp;

namespace NetSim.Application.Dhcp;

/// <summary>
/// Hosts a simulated DHCP service on a device (brief section 31): binds the device to
/// <see cref="DhcpProtocol.ServerPort"/> on UDP through the existing delivery manager and
/// associates the device with the <see cref="IDhcpServer"/> instance that answers there. This is
/// the "is a DHCP service running on this device" seam <see cref="IDhcpClient"/> consults after a
/// DISCOVER/REQUEST frame reaches a device. Directly mirrors <c>IDnsServerService</c> (minus the
/// TCP half - DHCP is UDP only).
/// </summary>
public interface IDhcpServerService
{
    /// <summary>
    /// Starts <paramref name="server"/> listening on <paramref name="device"/>'s UDP port 67. Throws
    /// <see cref="Core.Common.Exceptions.DomainException"/> if the device already runs a DHCP service.
    /// </summary>
    void Start(NetworkDevice device, IDhcpServer server);

    /// <summary>Stops the DHCP service on <paramref name="device"/> and releases its port binding. False when none was running.</summary>
    bool Stop(NetworkDevice device);

    bool IsRunning(NetworkDevice device);

    /// <summary>The <see cref="IDhcpServer"/> currently running on <paramref name="device"/>, if any.</summary>
    bool TryGetServer(NetworkDevice device, out IDhcpServer? server);
}
