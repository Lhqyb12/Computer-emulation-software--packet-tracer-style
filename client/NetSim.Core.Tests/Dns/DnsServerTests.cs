using NetSim.Core.Dns;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Dns;

/// <summary>
/// <see cref="DnsServer.HandleQuery"/> is pure protocol logic - no network I/O - so it is tested
/// directly against hand-built <see cref="DnsMessage"/> queries, exactly like <c>IcmpLayer</c>'s
/// Echo Request/Reply building is tested without a wire (brief section 51, "DNS Server Tests").
/// </summary>
public class DnsServerTests
{
    private static readonly DomainName Example = DomainName.Parse("example.com");
    private static readonly DomainName Www = DomainName.Parse("www.example.com");
    private static readonly DomainName Mail = DomainName.Parse("mail.example.com");
    private static readonly IPv4Address Querier = IPv4Address.Parse("192.168.1.10");

    private static DnsServer BuildExampleZone()
    {
        var server = new DnsServer(Example);
        server.AddRecord(new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300)));
        server.AddRecord(new DnsCnameRecord(Www, Example, TimeSpan.FromSeconds(300)));
        server.AddRecord(new DnsARecord(Mail, IPv4Address.Parse("192.168.1.101"), TimeSpan.FromSeconds(300)));
        server.AddRecord(new DnsMxRecord(Example, 10, Mail, TimeSpan.FromSeconds(300)));
        server.AddRecord(new DnsNsRecord(Example, DomainName.Parse("ns1.example.com"), TimeSpan.FromSeconds(300)));
        return server;
    }

    [Fact]
    public void AddRecord_RaisesRecordAdded()
    {
        var server = new DnsServer(Example);
        DnsRecord? added = null;
        server.RecordAdded += (_, e) => added = e.Record;

        var record = new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300));
        server.AddRecord(record);

        Assert.Equal(record, added);
        Assert.Contains(record, server.Records.AllRecords);
    }

    [Fact]
    public void RemoveRecord_RaisesRecordRemoved()
    {
        var server = new DnsServer(Example);
        var record = new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300));
        server.AddRecord(record);

        var removedEventRaised = false;
        server.RecordRemoved += (_, _) => removedEventRaised = true;

        var removed = server.RemoveRecord(record);

        Assert.True(removed);
        Assert.True(removedEventRaised);
        Assert.DoesNotContain(record, server.Records.AllRecords);
    }

    [Fact]
    public void HandleQuery_ExistingARecord_ReturnsNoErrorWithTheAnswer()
    {
        var server = BuildExampleZone();
        var query = DnsMessage.CreateQuery(1, Example, DnsRecordType.A);

        var response = server.HandleQuery(query, Querier);

        Assert.Equal(DnsResponseCode.NoError, response.Header.ResponseCode);
        Assert.True(response.Header.IsAuthoritativeAnswer);
        Assert.Equal(1, response.Header.TransactionId);
        var answer = Assert.Single(response.Answers);
        Assert.Equal("192.168.1.100", Assert.IsType<DnsARecord>(answer).DataText);
    }

    [Fact]
    public void HandleQuery_ExistingAAAARecord_ReturnsTheIPv6Address()
    {
        var server = new DnsServer(Example);
        var ipv6Name = DomainName.Parse("ipv6.example.com");
        server.AddRecord(new DnsAAAARecord(ipv6Name, IPv6Address.Parse("2001:db8::100"), TimeSpan.FromSeconds(300)));

        var response = server.HandleQuery(DnsMessage.CreateQuery(2, ipv6Name, DnsRecordType.AAAA), Querier);

        Assert.Equal(DnsResponseCode.NoError, response.Header.ResponseCode);
        var answer = Assert.Single(response.Answers);
        Assert.Equal("2001:db8::100", Assert.IsType<DnsAAAARecord>(answer).DataText);
    }

    [Fact]
    public void HandleQuery_Cname_ResolvesToTheFinalARecord()
    {
        var server = BuildExampleZone();

        var response = server.HandleQuery(DnsMessage.CreateQuery(3, Www, DnsRecordType.A), Querier);

        Assert.Equal(DnsResponseCode.NoError, response.Header.ResponseCode);
        Assert.Equal(2, response.Answers.Count);
        Assert.IsType<DnsCnameRecord>(response.Answers[0]);
        Assert.IsType<DnsARecord>(response.Answers[1]);
        Assert.Equal("192.168.1.100", response.Answers[1].DataText);
    }

    [Fact]
    public void HandleQuery_CnameLoop_ReturnsServerFailure_WithoutHanging()
    {
        var server = new DnsServer(Example);
        var a = DomainName.Parse("a.example.com");
        var b = DomainName.Parse("b.example.com");
        server.AddRecord(new DnsCnameRecord(a, b, TimeSpan.FromSeconds(300)));
        server.AddRecord(new DnsCnameRecord(b, a, TimeSpan.FromSeconds(300)));

        var response = server.HandleQuery(DnsMessage.CreateQuery(4, a, DnsRecordType.A), Querier);

        Assert.Equal(DnsResponseCode.ServerFailure, response.Header.ResponseCode);
    }

    [Fact]
    public void HandleQuery_MultipleARecords_ReturnsAllOfThem()
    {
        var server = new DnsServer(Example);
        server.AddRecord(new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300)));
        server.AddRecord(new DnsARecord(Example, IPv4Address.Parse("192.168.1.101"), TimeSpan.FromSeconds(300)));

        var response = server.HandleQuery(DnsMessage.CreateQuery(5, Example, DnsRecordType.A), Querier);

        Assert.Equal(2, response.Answers.Count);
    }

    [Fact]
    public void HandleQuery_UnknownName_ReturnsNxDomain_AndRaisesNxDomainEvent()
    {
        var server = BuildExampleZone();
        var raised = false;
        server.NxDomain += (_, _) => raised = true;

        var response = server.HandleQuery(DnsMessage.CreateQuery(6, DomainName.Parse("doesnotexist.example.com"), DnsRecordType.A), Querier);

        Assert.Equal(DnsResponseCode.NameError, response.Header.ResponseCode);
        Assert.Empty(response.Answers);
        Assert.True(raised);
    }

    [Fact]
    public void HandleQuery_NameExistsButNotForThisType_ReturnsNoErrorWithNoAnswers()
    {
        var server = BuildExampleZone();

        // "example.com" has A/MX/NS records but no AAAA record.
        var response = server.HandleQuery(DnsMessage.CreateQuery(7, Example, DnsRecordType.AAAA), Querier);

        Assert.Equal(DnsResponseCode.NoError, response.Header.ResponseCode);
        Assert.Empty(response.Answers);
    }

    [Fact]
    public void HandleQuery_MalformedQuestionCount_ReturnsFormatError()
    {
        var server = BuildExampleZone();
        var malformed = DnsMessage.CreateResponse(
            8, DnsResponseCode.NoError, [], [], isAuthoritativeAnswer: false, recursionDesired: true, recursionAvailable: false);

        var response = server.HandleQuery(malformed, Querier);

        Assert.Equal(DnsResponseCode.FormatError, response.Header.ResponseCode);
    }

    [Fact]
    public void HandleQuery_RecordRemovedThenQueriedAgain_ReturnsNxDomain()
    {
        var server = new DnsServer(Example);
        var record = new DnsARecord(DomainName.Parse("api.example.com"), IPv4Address.Parse("192.168.1.200"), TimeSpan.FromSeconds(300));
        server.AddRecord(record);
        Assert.Equal(DnsResponseCode.NoError, server.HandleQuery(DnsMessage.CreateQuery(9, record.Name, DnsRecordType.A), Querier).Header.ResponseCode);

        server.RemoveRecord(record);

        var response = server.HandleQuery(DnsMessage.CreateQuery(10, record.Name, DnsRecordType.A), Querier);
        Assert.Equal(DnsResponseCode.NameError, response.Header.ResponseCode);
    }

    [Fact]
    public void HandleQuery_TransactionIdIsEchoedBack()
    {
        var server = BuildExampleZone();

        var response = server.HandleQuery(DnsMessage.CreateQuery(54321, Example, DnsRecordType.A), Querier);

        Assert.Equal(54321, response.Header.TransactionId);
    }
}
