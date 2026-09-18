using NetSim.Core.Networking;

namespace NetSim.Core.Dns;

/// <summary>Default <see cref="IDnsServer"/>. See the interface for the overall design.</summary>
public sealed class DnsServer : IDnsServer
{
    private readonly int _maxCnameChainDepth;

    public DnsServer(DomainName origin, IDnsRecordStore? records = null, int maxCnameChainDepth = DnsProtocol.MaxCnameChainDepth)
    {
        Zone = new DnsZone(origin, records);
        _maxCnameChainDepth = maxCnameChainDepth;
    }

    public DnsZone Zone { get; }

    public IDnsRecordStore Records => Zone.Records;

    public event EventHandler<DnsEventArgs>? QueryReceived;

    public event EventHandler<DnsEventArgs>? ResponseCreated;

    public event EventHandler<DnsEventArgs>? RecordAdded;

    public event EventHandler<DnsEventArgs>? RecordRemoved;

    public event EventHandler<DnsEventArgs>? NxDomain;

    public void AddRecord(DnsRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Records.AddRecord(record);
        RecordAdded?.Invoke(this, new DnsEventArgs(name: record.Name, recordType: record.Type, record: record, detail: record.ToString()));
    }

    public bool RemoveRecord(DnsRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var removed = Records.RemoveRecord(record);
        if (removed)
        {
            RecordRemoved?.Invoke(this, new DnsEventArgs(name: record.Name, recordType: record.Type, record: record, detail: record.ToString()));
        }

        return removed;
    }

    public DnsMessage HandleQuery(DnsMessage query, IPv4Address querierAddress)
    {
        ArgumentNullException.ThrowIfNull(query);

        QueryReceived?.Invoke(this, new DnsEventArgs(
            message: query, transactionId: query.Header.TransactionId, detail: $"Query from {querierAddress}: {query}"));

        if (query.Questions.Count != 1 || query.Questions[0].Class != DnsClass.IN)
        {
            return CreateAndAnnounce(query, DnsResponseCode.FormatError, query.Questions, [], authoritative: false);
        }

        var question = query.Questions[0];
        var (responseCode, answers) = Resolve(question.Name, question.Type, depth: 0, visited: []);

        if (responseCode == DnsResponseCode.NameError)
        {
            NxDomain?.Invoke(this, new DnsEventArgs(name: question.Name, recordType: question.Type, detail: $"'{question.Name}' does not exist."));
        }

        return CreateAndAnnounce(query, responseCode, [question], answers, authoritative: true);
    }

    private DnsMessage CreateAndAnnounce(
        DnsMessage query, DnsResponseCode responseCode, IReadOnlyList<DnsQuestion> questions, IReadOnlyList<DnsRecord> answers, bool authoritative)
    {
        var response = DnsMessage.CreateResponse(
            query.Header.TransactionId, responseCode, questions, answers,
            isAuthoritativeAnswer: authoritative, recursionDesired: query.Header.IsRecursionDesired, recursionAvailable: false);

        ResponseCreated?.Invoke(this, new DnsEventArgs(
            message: response, responseCode: responseCode, transactionId: response.Header.TransactionId, detail: response.ToString()));

        return response;
    }

    /// <summary>
    /// Resolves <paramref name="name"/>/<paramref name="type"/> against <see cref="Zone"/>, chasing a
    /// CNAME chain (accumulating each hop's record ahead of the final answer) up to
    /// <see cref="_maxCnameChainDepth"/> - beyond that, or on a cycle, treated as a server failure
    /// rather than looping forever (brief section 24).
    /// </summary>
    private (DnsResponseCode Code, List<DnsRecord> Answers) Resolve(DomainName name, DnsRecordType type, int depth, HashSet<DomainName> visited)
    {
        if (depth > _maxCnameChainDepth || !visited.Add(name))
        {
            return (DnsResponseCode.ServerFailure, []);
        }

        var direct = Records.FindRecords(name, type);
        if (direct.Count > 0)
        {
            return (DnsResponseCode.NoError, [.. direct]);
        }

        var cnames = Records.FindRecords(name, DnsRecordType.CNAME);
        if (cnames.Count > 0 && type != DnsRecordType.CNAME)
        {
            var cname = (DnsCnameRecord)cnames[0];
            var (innerCode, innerAnswers) = Resolve(cname.Target, type, depth + 1, visited);

            var answers = new List<DnsRecord> { cname };
            answers.AddRange(innerAnswers);
            return (innerCode, answers);
        }

        return (Records.ContainsName(name) ? DnsResponseCode.NoError : DnsResponseCode.NameError, []);
    }
}
