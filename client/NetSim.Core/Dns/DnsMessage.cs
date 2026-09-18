using System.Linq;
using NetSim.Core.Packets;

namespace NetSim.Core.Dns;

/// <summary>
/// A complete DNS message (brief section 13): a <see cref="Header"/> plus the Question, Answer,
/// Authority and Additional sections. Used for both queries (<see cref="CreateQuery"/> - one
/// question, no answers) and responses (<see cref="CreateResponse"/>). Like <see cref="Icmp.IcmpMessage"/>
/// and <see cref="Udp.UdpDatagram"/> it is a leaf <see cref="IPacketPayload"/> - it rides as the
/// application payload of a <see cref="Udp.UdpDatagram"/> or <see cref="Tcp.TcpSegment"/> (brief
/// section 36), never wrapping another protocol layer itself.
///
/// <see cref="Length"/> is an estimated wire size (header + a compact name/record encoding), not an
/// actual byte serialization - the brief explicitly does not require a full RFC 1035 wire-format
/// codec for this phase ("do not build unnecessary complete wire-format serializer"), and none of
/// the existing transport payloads (<see cref="Udp.UdpDatagram"/>'s own checksum bytes aside) encode
/// a generic <see cref="IPacketPayload"/> to real bytes either.
/// </summary>
public sealed class DnsMessage : IPacketPayload
{
    private const int WireHeaderSize = 12;

    private DnsMessage(
        DnsHeader header, IReadOnlyList<DnsQuestion> questions, IReadOnlyList<DnsRecord> answers,
        IReadOnlyList<DnsRecord> authority, IReadOnlyList<DnsRecord> additional)
    {
        Header = header;
        Questions = questions;
        Answers = answers;
        Authority = authority;
        Additional = additional;
    }

    /// <summary>Builds a query message: one question, no answers, QR=0.</summary>
    public static DnsMessage CreateQuery(
        ushort transactionId, DomainName name, DnsRecordType type, DnsClass @class = DnsClass.IN, bool recursionDesired = true)
    {
        IReadOnlyList<DnsQuestion> questions = [new DnsQuestion(name, type, @class)];
        var header = new DnsHeader(
            transactionId, isResponse: false, DnsOpcode.Query, isAuthoritativeAnswer: false, isTruncated: false,
            recursionDesired, isRecursionAvailable: false, DnsResponseCode.NoError,
            questionCount: 1, answerCount: 0, authorityCount: 0, additionalCount: 0);

        return new DnsMessage(header, questions, [], [], []);
    }

    /// <summary>Builds a response message that echoes <paramref name="transactionId"/>, QR=1.</summary>
    public static DnsMessage CreateResponse(
        ushort transactionId,
        DnsResponseCode responseCode,
        IReadOnlyList<DnsQuestion> questions,
        IReadOnlyList<DnsRecord> answers,
        bool isAuthoritativeAnswer,
        bool recursionDesired,
        bool recursionAvailable,
        IReadOnlyList<DnsRecord>? authority = null,
        IReadOnlyList<DnsRecord>? additional = null)
    {
        authority ??= [];
        additional ??= [];

        var header = new DnsHeader(
            transactionId, isResponse: true, DnsOpcode.Query, isAuthoritativeAnswer, isTruncated: false,
            recursionDesired, recursionAvailable, responseCode,
            (ushort)questions.Count, (ushort)answers.Count, (ushort)authority.Count, (ushort)additional.Count);

        return new DnsMessage(header, questions, answers, authority, additional);
    }

    public DnsHeader Header { get; }

    public IReadOnlyList<DnsQuestion> Questions { get; }

    public IReadOnlyList<DnsRecord> Answers { get; }

    public IReadOnlyList<DnsRecord> Authority { get; }

    public IReadOnlyList<DnsRecord> Additional { get; }

    public bool IsQuery => !Header.IsResponse;

    public bool IsResponse => Header.IsResponse;

    // ---- IPacketPayload ----

    public string PayloadType => "DNS";

    public IPacketPayload? EncapsulatedPayload => null;

    public int Length => EstimateWireSize();

    /// <summary>Structural self-check: the header's section counts must match the actual sections.</summary>
    public PacketValidationResult Validate()
    {
        List<string>? errors = null;

        void Check(bool ok, string message)
        {
            if (!ok)
            {
                (errors ??= []).Add(message);
            }
        }

        Check(Header.QuestionCount == Questions.Count, "Header question count does not match the question section.");
        Check(Header.AnswerCount == Answers.Count, "Header answer count does not match the answer section.");
        Check(Header.AuthorityCount == Authority.Count, "Header authority count does not match the authority section.");
        Check(Header.AdditionalCount == Additional.Count, "Header additional count does not match the additional section.");

        return errors is null ? PacketValidationResult.Valid : PacketValidationResult.Invalid(errors.ToArray());
    }

    private int EstimateWireSize()
    {
        var size = WireHeaderSize;
        foreach (var question in Questions)
        {
            size += WireNameLength(question.Name) + 4; // QTYPE(2) + QCLASS(2)
        }

        foreach (var record in Answers.Concat(Authority).Concat(Additional))
        {
            size += WireNameLength(record.Name) + 10 + EstimateRDataLength(record); // TYPE(2)+CLASS(2)+TTL(4)+RDLENGTH(2)
        }

        return size;
    }

    private static int WireNameLength(DomainName name) => name.Labels.Sum(label => label.Length + 1) + 1;

    private static int EstimateRDataLength(DnsRecord record) => record switch
    {
        DnsARecord => 4,
        DnsAAAARecord => 16,
        DnsCnameRecord cname => WireNameLength(cname.Target),
        DnsNsRecord ns => WireNameLength(ns.NameServer),
        DnsMxRecord mx => 2 + WireNameLength(mx.Exchange),
        _ => 0,
    };

    public override string ToString() =>
        $"DNS {Header} q=[{string.Join(", ", Questions)}]" + (Answers.Count > 0 ? $" a=[{string.Join(", ", Answers)}]" : string.Empty);
}
