using System.Linq;
using NetSim.Core.Dhcp;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Dhcp;

public class DhcpServerConfigurationTests
{
    private static DhcpServerConfiguration Valid() => new(
        serverAddress: IPv4Address.Parse("192.168.1.1"),
        prefixLength: 24,
        poolStart: IPv4Address.Parse("192.168.1.100"),
        poolEnd: IPv4Address.Parse("192.168.1.200"),
        defaultGateway: IPv4Address.Parse("192.168.1.1"),
        dnsServer: IPv4Address.Parse("192.168.1.53"),
        leaseDuration: TimeSpan.FromSeconds(3600));

    [Fact]
    public void Validate_AcceptsAWellFormedConfiguration()
    {
        Assert.True(Valid().Validate().IsValid);
    }

    [Fact]
    public void Validate_RejectsPoolStartAfterPoolEnd()
    {
        var config = new DhcpServerConfiguration(
            IPv4Address.Parse("192.168.1.1"), 24, IPv4Address.Parse("192.168.1.200"), IPv4Address.Parse("192.168.1.100"));

        var result = config.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("after pool end"));
    }

    [Fact]
    public void Validate_RejectsAPoolOutsideTheServerSubnet()
    {
        var config = new DhcpServerConfiguration(
            IPv4Address.Parse("192.168.1.1"), 24, IPv4Address.Parse("10.0.0.100"), IPv4Address.Parse("10.0.0.200"));

        var result = config.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("outside the server subnet"));
    }

    [Fact]
    public void Validate_RejectsAPoolThatIncludesTheNetworkAddress()
    {
        var config = new DhcpServerConfiguration(
            IPv4Address.Parse("192.168.1.1"), 24, IPv4Address.Parse("192.168.1.0"), IPv4Address.Parse("192.168.1.50"));

        Assert.Contains(config.Validate().Errors, e => e.Contains("network address"));
    }

    [Fact]
    public void Validate_RejectsAPoolThatIncludesTheBroadcastAddress()
    {
        var config = new DhcpServerConfiguration(
            IPv4Address.Parse("192.168.1.1"), 24, IPv4Address.Parse("192.168.1.200"), IPv4Address.Parse("192.168.1.255"));

        Assert.Contains(config.Validate().Errors, e => e.Contains("broadcast address"));
    }

    [Fact]
    public void Validate_RejectsAPoolThatIncludesTheServerAddress()
    {
        var config = new DhcpServerConfiguration(
            IPv4Address.Parse("192.168.1.10"), 24, IPv4Address.Parse("192.168.1.1"), IPv4Address.Parse("192.168.1.50"));

        Assert.Contains(config.Validate().Errors, e => e.Contains("server's own address"));
    }

    [Fact]
    public void Validate_RejectsAnInvalidPrefixLength()
    {
        var config = new DhcpServerConfiguration(
            IPv4Address.Parse("192.168.1.1"), 40, IPv4Address.Parse("192.168.1.100"), IPv4Address.Parse("192.168.1.200"));

        Assert.Contains(config.Validate().Errors, e => e.Contains("Prefix length"));
    }

    [Fact]
    public void Validate_RejectsANonPositiveLeaseDuration()
    {
        var config = new DhcpServerConfiguration(
            IPv4Address.Parse("192.168.1.1"), 24, IPv4Address.Parse("192.168.1.100"), IPv4Address.Parse("192.168.1.200"),
            leaseDuration: TimeSpan.Zero);

        Assert.Contains(config.Validate().Errors, e => e.Contains("Lease duration"));
    }

    [Fact]
    public void Validate_RejectsDuplicateExcludedAddresses()
    {
        var config = new DhcpServerConfiguration(
            IPv4Address.Parse("192.168.1.1"), 24, IPv4Address.Parse("192.168.1.100"), IPv4Address.Parse("192.168.1.200"),
            excludedAddresses: [IPv4Address.Parse("192.168.1.150"), IPv4Address.Parse("192.168.1.150")]);

        Assert.Contains(config.Validate().Errors, e => e.Contains("listed more than once"));
    }

    [Fact]
    public void BuildPool_ProducesAPoolMatchingTheConfiguration()
    {
        var pool = Valid().BuildPool();

        Assert.Equal(IPv4Address.Parse("192.168.1.100"), pool.Start);
        Assert.Equal(IPv4Address.Parse("192.168.1.200"), pool.End);
        Assert.Equal(IPv4Address.Parse("192.168.1.1"), pool.ServerAddress);
    }
}
