namespace NetSim.Core.Dns;

/// <summary>
/// The DNS message header (RFC 1035 section 4.1.1, brief section 14): the transaction id used to
/// match a response to its query, the QR/Opcode/AA/TC/RD/RA flags, the response code, and the
/// section counts. Immutable and always built by <see cref="DnsMessage"/> from its own section
/// lengths, so a header's counts can never disagree with the sections that follow it.
/// </summary>
public sealed class DnsHeader
{
    public DnsHeader(
        ushort transactionId,
        bool isResponse,
        DnsOpcode opcode,
        bool isAuthoritativeAnswer,
        bool isTruncated,
        bool isRecursionDesired,
        bool isRecursionAvailable,
        DnsResponseCode responseCode,
        ushort questionCount,
        ushort answerCount,
        ushort authorityCount,
        ushort additionalCount)
    {
        TransactionId = transactionId;
        IsResponse = isResponse;
        Opcode = opcode;
        IsAuthoritativeAnswer = isAuthoritativeAnswer;
        IsTruncated = isTruncated;
        IsRecursionDesired = isRecursionDesired;
        IsRecursionAvailable = isRecursionAvailable;
        ResponseCode = responseCode;
        QuestionCount = questionCount;
        AnswerCount = answerCount;
        AuthorityCount = authorityCount;
        AdditionalCount = additionalCount;
    }

    /// <summary>Identifies a query/response pair - a response must echo the query's transaction id (brief section 32).</summary>
    public ushort TransactionId { get; }

    /// <summary>QR bit: false for a query, true for a response.</summary>
    public bool IsResponse { get; }

    public DnsOpcode Opcode { get; }

    /// <summary>AA bit: set by a server answering from its own authoritative data (brief section 26).</summary>
    public bool IsAuthoritativeAnswer { get; }

    /// <summary>TC bit: the message was too large and got truncated. Never set by this simulator (no UDP size limit is modelled) - carried for structural completeness.</summary>
    public bool IsTruncated { get; }

    /// <summary>RD bit: the querier is asking the server to resolve recursively if it cannot answer locally (brief section 27).</summary>
    public bool IsRecursionDesired { get; }

    /// <summary>RA bit: the server supports recursion. Always false in this phase - no recursive/forwarding resolver exists yet (brief section 27/28).</summary>
    public bool IsRecursionAvailable { get; }

    public DnsResponseCode ResponseCode { get; }

    public ushort QuestionCount { get; }

    public ushort AnswerCount { get; }

    public ushort AuthorityCount { get; }

    public ushort AdditionalCount { get; }

    public override string ToString() =>
        $"[id={TransactionId} {(IsResponse ? "response" : "query")} {Opcode} {ResponseCode} " +
        $"qd={QuestionCount} an={AnswerCount} ns={AuthorityCount} ar={AdditionalCount}]";
}
