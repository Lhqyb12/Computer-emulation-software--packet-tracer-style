namespace NetSim.Core.Dhcp;

/// <summary>
/// The BOOTP <c>op</c> field (brief section 22): every DHCP message is carried in a BOOTP frame
/// that is either a request from a client or a reply from a server. The DHCP <em>message type</em>
/// (DISCOVER/OFFER/...) is a separate option - see <see cref="DhcpMessageType"/>.
/// </summary>
public enum DhcpOpcode : byte
{
    /// <summary>Unset - not a valid message.</summary>
    None = 0,

    /// <summary>A message from a client to a server (DISCOVER, REQUEST, DECLINE, RELEASE).</summary>
    BootRequest = 1,

    /// <summary>A message from a server to a client (OFFER, ACK, NAK).</summary>
    BootReply = 2,
}
