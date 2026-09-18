using NetSim.Core.Networking;

namespace NetSim.Core.Dhcp;

/// <summary>
/// Payload for every DHCP notification (brief section 40) - the same Core-stays-dispatch-agnostic
/// carrier pattern as <see cref="Dns.DnsEventArgs"/>/<see cref="Udp.UdpEventArgs"/>: every field is
/// optional, so one type serves the whole server-side and client-side event set rather than a new
/// event framework.
/// </summary>
public sealed class DhcpEventArgs : EventArgs
{
    public DhcpEventArgs(
        DhcpMessage? message = null,
        DhcpMessageType? messageType = null,
        DhcpClientId? clientId = null,
        IPv4Address? address = null,
        DhcpLease? lease = null,
        DhcpNetworkConfiguration? configuration = null,
        uint? transactionId = null,
        DhcpClientState? clientState = null,
        string? detail = null)
    {
        Message = message;
        MessageType = messageType ?? message?.MessageType;
        ClientId = clientId;
        Address = address;
        Lease = lease;
        Configuration = configuration;
        TransactionId = transactionId ?? message?.TransactionId;
        ClientState = clientState;
        Detail = detail;
    }

    public DhcpMessage? Message { get; }

    public DhcpMessageType? MessageType { get; }

    public DhcpClientId? ClientId { get; }

    public IPv4Address? Address { get; }

    public DhcpLease? Lease { get; }

    public DhcpNetworkConfiguration? Configuration { get; }

    public uint? TransactionId { get; }

    public DhcpClientState? ClientState { get; }

    public string? Detail { get; }
}
