namespace NetSim.Core.Dns;

/// <summary>
/// Payload for every DNS notification (brief section 29) - the same Core-stays-dispatch-agnostic
/// carrier pattern as <see cref="Udp.UdpEventArgs"/>/<see cref="Icmp.IcmpEventArgs"/>: every field
/// is optional, so one type serves <see cref="IDnsServer"/>'s and the resolver's whole event set.
/// </summary>
public sealed class DnsEventArgs : EventArgs
{
    public DnsEventArgs(
        DnsMessage? message = null,
        DomainName? name = null,
        DnsRecordType? recordType = null,
        IReadOnlyList<DnsRecord>? records = null,
        DnsRecord? record = null,
        DnsResponseCode? responseCode = null,
        ushort? transactionId = null,
        string? detail = null)
    {
        Message = message;
        Name = name;
        RecordType = recordType;
        Records = records;
        Record = record;
        ResponseCode = responseCode;
        TransactionId = transactionId;
        Detail = detail;
    }

    public DnsMessage? Message { get; }

    public DomainName? Name { get; }

    public DnsRecordType? RecordType { get; }

    public IReadOnlyList<DnsRecord>? Records { get; }

    public DnsRecord? Record { get; }

    public DnsResponseCode? ResponseCode { get; }

    public ushort? TransactionId { get; }

    public string? Detail { get; }
}
