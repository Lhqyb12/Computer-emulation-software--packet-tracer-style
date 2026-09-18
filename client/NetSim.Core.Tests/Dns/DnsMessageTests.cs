using NetSim.Core.Dns;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Dns;

public class DnsMessageTests
{
    private static readonly DomainName Www = DomainName.Parse("www.example.com");
    private static readonly DomainName Example = DomainName.Parse("example.com");

    [Fact]
    public void CreateQuery_HasOneQuestion_AndNoOtherSections()
    {
        var query = DnsMessage.CreateQuery(1234, Www, DnsRecordType.A);

        Assert.True(query.IsQuery);
        Assert.False(query.IsResponse);
        Assert.Equal(1234, query.Header.TransactionId);
        Assert.False(query.Header.IsResponse);
        Assert.Equal(DnsOpcode.Query, query.Header.Opcode);
        Assert.True(query.Header.IsRecursionDesired);
        Assert.Single(query.Questions);
        Assert.Equal(Www, query.Questions[0].Name);
        Assert.Equal(DnsRecordType.A, query.Questions[0].Type);
        Assert.Equal(DnsClass.IN, query.Questions[0].Class);
        Assert.Empty(query.Answers);
        Assert.Empty(query.Authority);
        Assert.Empty(query.Additional);
        Assert.Equal(1, query.Header.QuestionCount);
        Assert.Equal(0, query.Header.AnswerCount);
    }

    [Fact]
    public void CreateResponse_EchoesTransactionId_AndCarriesAnswers()
    {
        var question = new DnsQuestion(Www, DnsRecordType.A);
        var answer = new DnsARecord(Www, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300));

        var response = DnsMessage.CreateResponse(
            1234, DnsResponseCode.NoError, [question], [answer],
            isAuthoritativeAnswer: true, recursionDesired: true, recursionAvailable: false);

        Assert.True(response.IsResponse);
        Assert.False(response.IsQuery);
        Assert.Equal(1234, response.Header.TransactionId);
        Assert.Equal(DnsResponseCode.NoError, response.Header.ResponseCode);
        Assert.True(response.Header.IsAuthoritativeAnswer);
        Assert.False(response.Header.IsRecursionAvailable);
        Assert.Single(response.Answers);
        Assert.Equal(answer, response.Answers[0]);
        Assert.Equal(1, response.Header.AnswerCount);
    }

    [Fact]
    public void CreateResponse_NxDomain_HasResponseCodeAndNoAnswers()
    {
        var question = new DnsQuestion(DomainName.Parse("doesnotexist.example.com"), DnsRecordType.A);

        var response = DnsMessage.CreateResponse(
            42, DnsResponseCode.NameError, [question], [], isAuthoritativeAnswer: true, recursionDesired: true, recursionAvailable: false);

        Assert.Equal(DnsResponseCode.NameError, response.Header.ResponseCode);
        Assert.Empty(response.Answers);
    }

    [Fact]
    public void Validate_WellFormedMessage_IsValid()
    {
        var query = DnsMessage.CreateQuery(1, Www, DnsRecordType.A);

        Assert.True(query.Validate().IsValid);
    }

    [Fact]
    public void Length_GrowsWithMoreAnswers()
    {
        var question = new DnsQuestion(Example, DnsRecordType.A);
        var oneAnswer = DnsMessage.CreateResponse(
            1, DnsResponseCode.NoError, [question],
            [new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300))],
            isAuthoritativeAnswer: true, recursionDesired: true, recursionAvailable: false);

        var twoAnswers = DnsMessage.CreateResponse(
            1, DnsResponseCode.NoError, [question],
            [
                new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300)),
                new DnsARecord(Example, IPv4Address.Parse("192.168.1.101"), TimeSpan.FromSeconds(300)),
            ],
            isAuthoritativeAnswer: true, recursionDesired: true, recursionAvailable: false);

        Assert.True(twoAnswers.Length > oneAnswer.Length);
    }

    [Fact]
    public void Question_ToString_IsHumanReadable()
    {
        var question = new DnsQuestion(Www, DnsRecordType.A);

        Assert.Equal("www.example.com IN A", question.ToString());
    }
}
