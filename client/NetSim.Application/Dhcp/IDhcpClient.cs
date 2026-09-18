using NetSim.Core.Dhcp;
using NetSim.Core.Networking;

namespace NetSim.Application.Dhcp;

/// <summary>
/// The DHCP client (brief section 32): drives the full simulated acquisition workflow
/// (DISCOVER -&gt; OFFER -&gt; REQUEST -&gt; ACK -&gt; BOUND) from a device's interface over the real
/// Ethernet/IPv4/UDP engines - never the operating system's DHCP - and maintains the resulting
/// lease (renew, release, expire). The DHCP counterpart to <c>IDnsResolver</c>/<c>IPingService</c>.
///
/// Every method reports an expected simulation failure through its result type rather than throwing
/// (brief sections 42-44, 67).
/// </summary>
public interface IDhcpClient
{
    /// <summary>
    /// Runs a fresh acquisition on <paramref name="networkInterface"/> (INIT -&gt; SELECTING -&gt;
    /// REQUESTING -&gt; BOUND). On success the interface's IPv4 configuration and the device's DNS
    /// server list are updated from the ACK.
    /// </summary>
    DhcpAcquireResult Acquire(NetworkInterface networkInterface);

    /// <summary>
    /// Renews the lease currently bound to <paramref name="networkInterface"/> (BOUND -&gt; RENEWING
    /// -&gt; BOUND), extending its expiry (brief section 15). Returns
    /// <see cref="DhcpAcquireStatus.NotBound"/> when the interface holds no lease.
    /// </summary>
    DhcpAcquireResult Renew(NetworkInterface networkInterface);

    /// <summary>
    /// Sends a RELEASE for <paramref name="networkInterface"/>'s lease and clears the DHCP-derived
    /// configuration (brief section 18). Manual/static configuration is left untouched.
    /// </summary>
    DhcpReleaseResult Release(NetworkInterface networkInterface);

    /// <summary>True when the interface holds a DHCP lease whose time has run out (brief section 17).</summary>
    bool IsLeaseExpired(NetworkInterface networkInterface);

    /// <summary>
    /// Withdraws an expired lease's configuration and moves the client to
    /// <see cref="DhcpClientState.Expired"/> so it will not keep using the address (brief section
    /// 17). No-op when the lease is still valid.
    /// </summary>
    bool AbandonExpiredLease(NetworkInterface networkInterface);

    event EventHandler<DhcpEventArgs>? ClientStarted;

    event EventHandler<DhcpEventArgs>? DiscoverCreated;

    event EventHandler<DhcpEventArgs>? DiscoverSent;

    event EventHandler<DhcpEventArgs>? OfferReceived;

    event EventHandler<DhcpEventArgs>? RequestCreated;

    event EventHandler<DhcpEventArgs>? RequestSent;

    event EventHandler<DhcpEventArgs>? AckReceived;

    event EventHandler<DhcpEventArgs>? NakReceived;

    event EventHandler<DhcpEventArgs>? LeaseBound;

    event EventHandler<DhcpEventArgs>? LeaseRenewed;

    event EventHandler<DhcpEventArgs>? LeaseReleased;

    event EventHandler<DhcpEventArgs>? LeaseExpired;

    event EventHandler<DhcpEventArgs>? ConfigurationApplied;

    event EventHandler<DhcpEventArgs>? ServerUnavailable;

    event EventHandler<DhcpEventArgs>? PoolExhausted;
}
