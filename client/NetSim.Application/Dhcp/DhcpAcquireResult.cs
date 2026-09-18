using NetSim.Core.Dhcp;
using NetSim.Core.Networking;

namespace NetSim.Application.Dhcp;

/// <summary>How an <see cref="IDhcpClient.Acquire"/> / <see cref="IDhcpClient.Renew"/> call ended (brief sections 41-44).</summary>
public enum DhcpAcquireStatus
{
    /// <summary>A lease was obtained and its configuration applied - the client is BOUND.</summary>
    Bound,

    /// <summary>An existing lease was successfully extended - the client stays BOUND.</summary>
    Renewed,

    /// <summary>No network is currently open.</summary>
    NoNetwork,

    /// <summary>The interface cannot run DHCP (not Ethernet, no MAC, or the link is down).</summary>
    InterfaceUnusable,

    /// <summary>A DISCOVER was broadcast but no server offered anything (brief section 42).</summary>
    NoOffer,

    /// <summary>A server received the DISCOVER but had no address left to offer (brief section 43).</summary>
    PoolExhausted,

    /// <summary>The selected server rejected the REQUEST with a NAK (brief section 20).</summary>
    Nak,

    /// <summary>The ACK did not carry a usable configuration (e.g. no subnet mask) - nothing was applied (brief section 44).</summary>
    InvalidConfiguration,

    /// <summary>Renew/Release was called for an interface that is not currently bound.</summary>
    NotBound,

    /// <summary>An unexpected simulation-level problem.</summary>
    Error,
}

/// <summary>
/// The outcome of one DHCP acquisition or renewal (brief section 41): success/failure plus every
/// piece of the negotiated configuration, so diagnostics / a future packet inspector / labs can
/// explain what happened. Never thrown - an expected failure is reported here, exactly like
/// <see cref="Diagnostics.PingSessionResult"/> / <see cref="Dns.DnsResolutionResult"/>.
/// </summary>
public sealed class DhcpAcquireResult
{
    private DhcpAcquireResult(
        DhcpAcquireStatus status,
        DhcpClientId clientId,
        uint? transactionId,
        DhcpNetworkConfiguration? configuration,
        IPv4Address? server,
        string? failureReason)
    {
        Status = status;
        ClientId = clientId;
        TransactionId = transactionId;
        Configuration = configuration;
        Server = server;
        FailureReason = failureReason;
    }

    public DhcpAcquireStatus Status { get; }

    public bool IsSuccess => Status is DhcpAcquireStatus.Bound or DhcpAcquireStatus.Renewed;

    public DhcpClientId ClientId { get; }

    public uint? TransactionId { get; }

    /// <summary>The applied configuration - non-null only on success.</summary>
    public DhcpNetworkConfiguration? Configuration { get; }

    /// <summary>The server that answered (its option-54 identifier), if the exchange got that far.</summary>
    public IPv4Address? Server { get; }

    public string? FailureReason { get; }

    public IPv4Address? Address => Configuration?.Address;

    public SubnetMask? SubnetMask => Configuration?.SubnetMask;

    public IPv4Address? Gateway => Configuration?.Gateway;

    public IPv4Address? DnsServer => Configuration?.DnsServer;

    public TimeSpan? LeaseDuration => Configuration?.LeaseDuration;

    public static DhcpAcquireResult Bound(DhcpClientId clientId, uint transactionId, DhcpNetworkConfiguration configuration) =>
        new(DhcpAcquireStatus.Bound, clientId, transactionId, configuration, configuration.ServerIdentifier, null);

    public static DhcpAcquireResult Renewed(DhcpClientId clientId, uint transactionId, DhcpNetworkConfiguration configuration) =>
        new(DhcpAcquireStatus.Renewed, clientId, transactionId, configuration, configuration.ServerIdentifier, null);

    public static DhcpAcquireResult Failure(
        DhcpAcquireStatus status, DhcpClientId clientId, string reason, uint? transactionId = null, IPv4Address? server = null) =>
        new(status, clientId, transactionId, configuration: null, server, reason);

    public override string ToString() =>
        IsSuccess ? $"{Status}: {Configuration}" : $"{Status}: {FailureReason}";
}
