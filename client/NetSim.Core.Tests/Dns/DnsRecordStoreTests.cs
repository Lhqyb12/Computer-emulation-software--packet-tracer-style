using NetSim.Core.Common.Exceptions;
using NetSim.Core.Dns;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Dns;

public class DnsRecordStoreTests
{
    private static readonly DomainName Example = DomainName.Parse("example.com");
    private static readonly DomainName Www = DomainName.Parse("www.example.com");

    [Fact]
    public void AddRecord_ThenFindRecords_ReturnsIt()
    {
        var store = new DnsRecordStore();
        var record = new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300));

        store.AddRecord(record);

        Assert.Equal([record], store.FindRecords(Example, DnsRecordType.A));
    }

    [Fact]
    public void AddRecord_ExactDuplicate_Throws()
    {
        var store = new DnsRecordStore();
        var record = new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300));
        store.AddRecord(record);

        Assert.Throws<DomainException>(() => store.AddRecord(
            new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300))));
    }

    [Fact]
    public void AddRecord_DifferentAddress_ForSameName_IsAllowed()
    {
        var store = new DnsRecordStore();
        store.AddRecord(new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300)));
        store.AddRecord(new DnsARecord(Example, IPv4Address.Parse("192.168.1.101"), TimeSpan.FromSeconds(300)));

        Assert.Equal(2, store.FindRecords(Example, DnsRecordType.A).Count);
    }

    [Fact]
    public void FindRecords_UnknownName_ReturnsEmpty()
    {
        var store = new DnsRecordStore();

        Assert.Empty(store.FindRecords(Example, DnsRecordType.A));
    }

    [Fact]
    public void ContainsName_TrueOnlyWhenAtLeastOneRecordExists()
    {
        var store = new DnsRecordStore();
        Assert.False(store.ContainsName(Example));

        store.AddRecord(new DnsCnameRecord(Www, Example, TimeSpan.FromSeconds(300)));

        Assert.True(store.ContainsName(Www));
        Assert.False(store.ContainsName(Example));
    }

    [Fact]
    public void RemoveRecord_ExactMatch_RemovesIt()
    {
        var store = new DnsRecordStore();
        var record = new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300));
        store.AddRecord(record);

        var removed = store.RemoveRecord(record);

        Assert.True(removed);
        Assert.False(store.ContainsName(Example));
    }

    [Fact]
    public void RemoveRecord_NotPresent_ReturnsFalse()
    {
        var store = new DnsRecordStore();

        Assert.False(store.RemoveRecord(new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300))));
    }

    [Fact]
    public void RemoveRecords_ByNameAndType_RemovesOnlyThoseRecords()
    {
        var store = new DnsRecordStore();
        store.AddRecord(new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300)));
        store.AddRecord(new DnsMxRecord(Example, 10, DomainName.Parse("mail.example.com"), TimeSpan.FromSeconds(300)));

        var removed = store.RemoveRecords(Example, DnsRecordType.A);

        Assert.Equal(1, removed);
        Assert.Empty(store.FindRecords(Example, DnsRecordType.A));
        Assert.NotEmpty(store.FindRecords(Example, DnsRecordType.MX));
    }

    [Fact]
    public void RemoveRecords_ByNameOnly_RemovesEveryType()
    {
        var store = new DnsRecordStore();
        store.AddRecord(new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300)));
        store.AddRecord(new DnsMxRecord(Example, 10, DomainName.Parse("mail.example.com"), TimeSpan.FromSeconds(300)));

        var removed = store.RemoveRecords(Example);

        Assert.Equal(2, removed);
        Assert.False(store.ContainsName(Example));
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        var store = new DnsRecordStore();
        store.AddRecord(new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300)));

        store.Clear();

        Assert.Empty(store.AllRecords);
    }

    [Fact]
    public void FindByName_ReturnsRecordsOfEveryType()
    {
        var store = new DnsRecordStore();
        store.AddRecord(new DnsARecord(Example, IPv4Address.Parse("192.168.1.100"), TimeSpan.FromSeconds(300)));
        store.AddRecord(new DnsNsRecord(Example, DomainName.Parse("ns1.example.com"), TimeSpan.FromSeconds(300)));

        Assert.Equal(2, store.FindByName(Example).Count);
    }
}
