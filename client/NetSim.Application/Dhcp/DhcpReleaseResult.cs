using NetSim.Core.Networking;

namespace NetSim.Application.Dhcp;

/// <summary>The outcome of an <see cref="IDhcpClient.Release"/> call (brief section 18).</summary>
public sealed record DhcpReleaseResult(bool IsReleased, IPv4Address? ReleasedAddress, string Detail)
{
    public static DhcpReleaseResult Released(IPv4Address address) =>
        new(true, address, $"Lease {address} released and DHCP-derived configuration cleared.");

    public static DhcpReleaseResult NothingToRelease { get; } =
        new(false, null, "The interface holds no DHCP lease to release.");
}
