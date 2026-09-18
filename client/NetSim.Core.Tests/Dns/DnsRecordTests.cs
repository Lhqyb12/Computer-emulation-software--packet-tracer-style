using NetSim.Core.Common.Exceptions;
using NetSim.Core.Dns;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Dns;

public class DnsRecordTests
{
    private static readonly DomainName Example = DomainName.Parse("example.com");
    private static readonly DomainName Www = DomainName.Parse("www.example.com");

    [Fact]
    public void ARecord_ExposesNameTypeAndAddress()
    {
        var record = new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300));

        Assert.Equal(Example, record.Name);
        Assert.Equal(DnsRecordType.A, record.Type);
        Assert.Equal(DnsClass.IN, record.Class);
        Assert.Equal("192.168.1.100", record.DataText);
    }

    [Fact]
    public void AAAARecord_ExposesIPv6Address()
    {
        var record = new DnsAAAARecord(DomainName.Parse("ipv6.example.com"), IPv6Address.Parse("2001:db8::100"), TimeSpan.FromSeconds(300));

        Assert.Equal(DnsRecordType.AAAA, record.Type);
        Assert.Equal("2001:db8::100", record.DataText);
    }

    [Fact]
    public void CnameRecord_ExposesTarget()
    {
        var record = new DnsCnameRecord(Www, Example, TimeSpan.FromSeconds(300));

        Assert.Equal(DnsRecordType.CNAME, record.Type);
        Assert.Equal(Example, record.Target);
        Assert.Equal("example.com", record.DataText);
    }

    [Fact]
    public void NsRecord_ExposesNameServer()
    {
        var ns = DomainName.Parse("ns1.example.com");
        var record = new DnsNsRecord(Example, ns, TimeSpan.FromSeconds(300));

        Assert.Equal(DnsRecordType.NS, record.Type);
        Assert.Equal(ns, record.NameServer);
    }

    [Fact]
    public void MxRecord_ExposesPreferenceAndExchange()
    {
        var mail = DomainName.Parse("mail.example.com");
        var record = new DnsMxRecord(Example, 10, mail, TimeSpan.FromSeconds(300));

        Assert.Equal(DnsRecordType.MX, record.Type);
        Assert.Equal(10, record.Preference);
        Assert.Equal(mail, record.Exchange);
        Assert.Equal("10 mail.example.com", record.DataText);
    }

    [Fact]
    public void NegativeTtl_Throws()
    {
        Assert.Throws<DomainException>(() => new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void ZeroTtl_IsAllowed()
    {
        var record = new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, record.Ttl);
    }

    [Fact]
    public void Equality_SameNameTypeClassTtlAndData_AreEqual()
    {
        var a = new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300));
        var b = new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300));

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentAddress_AreNotEqual()
    {
        var a = new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300));
        var b = new DnsARecord(Example, IPv4Address.Parse("192.168.1.101"), TimeSpan.FromSeconds(300));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentRecordTypes_AreNotEqual_EvenWithTheSameDataText()
    {
        var cname = new DnsCnameRecord(Example, DomainName.Parse("target.com"), TimeSpan.FromSeconds(300));
        var ns = new DnsNsRecord(Example, DomainName.Parse("target.com"), TimeSpan.FromSeconds(300));

        Assert.NotEqual<DnsRecord>(cname, ns);
    }

    [Fact]
    public void MultipleARecords_ForTheSameName_AreDistinctValues()
    {
        var first = new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300));
        var second = new DnsARecord(Example, IPv4Address.Parse("192.168.1.101"), TimeSpan.FromSeconds(300));

        Assert.NotEqual(first, second);
        Assert.Equal(first.Name, second.Name);
    }
}
