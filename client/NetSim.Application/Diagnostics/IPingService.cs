using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Application.Diagnostics;

/// <summary>
/// The Ping diagnostic tool (brief section 20): drives an Echo Request/Reply exchange entirely
/// inside the simulator - ARP resolution, ICMP, IPv4 and Ethernet all run through the real Core
/// engines exactly as a device would use them, never the operating system's real network. This is
/// the seam a future Traceroute tool (building on <see cref="Core.Icmp.IcmpType.TimeExceeded"/>) and
/// a Ping UI both use.
/// </summary>
public interface IPingService
{
    /// <summary>
    /// Sends <paramref name="count"/> Echo Requests from <paramref name="sourceInterface"/> to
    /// <paramref name="destination"/>, one at a time, each fully resolved (ARP if needed),
    /// transmitted, processed by the destination and replied to before the next one starts.
    /// Never throws for an expected simulation failure (no route, ARP failure, link down, no
    /// reply) - each attempt's outcome is reported as a <see cref="PingReply"/>.
    /// </summary>
    PingSessionResult Ping(NetworkInterface sourceInterface, IPv4Address destination, int count = 4, RawPayload? payload = null);
}
