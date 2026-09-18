using NetSim.Core.Dns;

namespace NetSim.Application.Dns;

/// <summary>How a <see cref="IDnsResolver.Resolve"/> call ended (brief section 44 - "Query Result").</summary>
public enum DnsResolutionStatus
{
    Success,

    /// <summary>The device has no configured DNS server (brief section 20).</summary>
    NoConfiguredServer,

    /// <summary>The configured DNS server is outside the source interface's local subnet - no routing engine exists yet (Phase 27+).</summary>
    NoRoute,

    /// <summary>Nothing answered at the configured DNS server's address/port (ARP failure, or no DNS service bound there) - brief section 31.</summary>
    ServerUnavailable,

    /// <summary>The server authoritatively reported the name does not exist.</summary>
    NxDomain,

    /// <summary>The name exists but has no records of the requested type.</summary>
    NoData,

    /// <summary>A CNAME chain revisited a name already seen - it would loop forever.</summary>
    CnameLoop,

    /// <summary>A CNAME chain exceeded <see cref="DnsProtocol.MaxCnameChainDepth"/> hops.</summary>
    MaxCnameDepthExceeded,

    /// <summary>The received message was not a usable DNS response (wrong shape, or failed the transaction-id check).</summary>
    InvalidResponse,

    /// <summary>The server reported <see cref="DnsResponseCode.ServerFailure"/>.</summary>
    ServerFailure,

    /// <summary>The server reported <see cref="DnsResponseCode.Refused"/> or <see cref="DnsResponseCode.FormatError"/>.</summary>
    Refused,

    /// <summary>An unexpected simulation-level problem (no open network, no source address, ...).</summary>
    Error,
}

/// <summary>
/// The outcome of one <see cref="IDnsResolver.Resolve"/> call (brief section 44): success/failure,
/// what was asked, what came back, and enough context (cache hit, transaction id, response code) for
/// diagnostics or a future packet inspector to explain what happened.
/// </summary>
public sealed class DnsResolutionResult
{
    private DnsResolutionResult(
        DnsResolutionStatus status, DomainName queriedName, DnsRecordType recordType, IReadOnlyList<DnsRecord> records,
        bool fromCache, DnsResponseCode? responseCode, ushort? transactionId, string? errorMessage)
    {
        Status = status;
        QueriedName = queriedName;
        RecordType = recordType;
        Records = records;
        FromCache = fromCache;
        ResponseCode = responseCode;
        TransactionId = transactionId;
        ErrorMessage = errorMessage;
    }

    public DnsResolutionStatus Status { get; }

    public bool IsSuccess => Status == DnsResolutionStatus.Success;

    public DomainName QueriedName { get; }

    public DnsRecordType RecordType { get; }

    /// <summary>The resolved records - empty unless <see cref="IsSuccess"/>.</summary>
    public IReadOnlyList<DnsRecord> Records { get; }

    /// <summary>True when <see cref="Records"/> came from the resolver's cache rather than a fresh query.</summary>
    public bool FromCache { get; }

    public DnsResponseCode? ResponseCode { get; }

    public ushort? TransactionId { get; }

    public string? ErrorMessage { get; }

    public static DnsResolutionResult Success(
        DomainName name, DnsRecordType type, IReadOnlyList<DnsRecord> records, bool fromCache, ushort? transactionId = null) =>
        new(DnsResolutionStatus.Success, name, type, records, fromCache, DnsResponseCode.NoError, transactionId, errorMessage: null);

    public static DnsResolutionResult Failure(
        DnsResolutionStatus status, DomainName name, DnsRecordType type, string message,
        DnsResponseCode? responseCode = null, ushort? transactionId = null) =>
        new(status, name, type, [], fromCache: false, responseCode, transactionId, message);

    public override string ToString() =>
        IsSuccess
            ? $"{QueriedName} {RecordType} -> [{string.Join(", ", Records.Select(r => r.DataText))}]{(FromCache ? " (cache)" : string.Empty)}"
            : $"{QueriedName} {RecordType} -> {Status}: {ErrorMessage}";
}
