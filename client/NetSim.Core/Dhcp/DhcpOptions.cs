using NetSim.Core.Networking;

namespace NetSim.Core.Dhcp;

/// <summary>
/// The options section of a <see cref="DhcpMessage"/> (brief section 24) as an immutable,
/// strongly-typed value object rather than a bag of code/byte-array pairs hand-encoded into the
/// message class. Every option the implemented workflow needs has a dedicated typed property; the
/// <see cref="Builder"/> is the only way to construct a non-empty instance, so a message can never
/// carry a half-built option.
///
/// This is the DHCP counterpart to how <see cref="Dns.DnsRecord"/> models typed record data - the
/// brief's "use strongly typed option representations; do not hard-code options directly into a huge
/// DHCP message class".
/// </summary>
public sealed class DhcpOptions
{
    /// <summary>The empty option set - no message type, no parameters. Only valid as a starting point for a <see cref="Builder"/>.</summary>
    public static DhcpOptions Empty { get; } = new Builder().Build();

    private DhcpOptions(
        DhcpMessageType? messageType,
        SubnetMask? subnetMask,
        IPv4Address? router,
        IReadOnlyList<IPv4Address> domainNameServers,
        IPv4Address? requestedIpAddress,
        IPv4Address? serverIdentifier,
        TimeSpan? ipAddressLeaseTime,
        TimeSpan? renewalTime,
        TimeSpan? rebindingTime,
        IReadOnlyList<DhcpOptionCode> parameterRequestList,
        DhcpClientId? clientIdentifier,
        string? hostName)
    {
        MessageType = messageType;
        SubnetMask = subnetMask;
        Router = router;
        DomainNameServers = domainNameServers;
        RequestedIpAddress = requestedIpAddress;
        ServerIdentifier = serverIdentifier;
        IpAddressLeaseTime = ipAddressLeaseTime;
        RenewalTime = renewalTime;
        RebindingTime = rebindingTime;
        ParameterRequestList = parameterRequestList;
        ClientIdentifier = clientIdentifier;
        HostName = hostName;
    }

    /// <summary>Option 53 - the DHCP message type. Present on every valid DHCP message.</summary>
    public DhcpMessageType? MessageType { get; }

    /// <summary>Option 1 - the subnet mask for the offered/assigned address.</summary>
    public SubnetMask? SubnetMask { get; }

    /// <summary>Option 3 - the default gateway.</summary>
    public IPv4Address? Router { get; }

    /// <summary>Option 6 - the DNS server list (empty, never null).</summary>
    public IReadOnlyList<IPv4Address> DomainNameServers { get; }

    /// <summary>Option 50 - the address the client is asking for.</summary>
    public IPv4Address? RequestedIpAddress { get; }

    /// <summary>Option 54 - which server this message is from / is addressed to.</summary>
    public IPv4Address? ServerIdentifier { get; }

    /// <summary>Option 51 - the lease duration.</summary>
    public TimeSpan? IpAddressLeaseTime { get; }

    /// <summary>Option 58 - T1 renewal time.</summary>
    public TimeSpan? RenewalTime { get; }

    /// <summary>Option 59 - T2 rebinding time.</summary>
    public TimeSpan? RebindingTime { get; }

    /// <summary>Option 55 - the option codes the client wants echoed back (empty, never null).</summary>
    public IReadOnlyList<DhcpOptionCode> ParameterRequestList { get; }

    /// <summary>Option 61 - an explicit client identifier.</summary>
    public DhcpClientId? ClientIdentifier { get; }

    /// <summary>Option 12 - the client's host name.</summary>
    public string? HostName { get; }

    /// <summary>The first DNS server, if any - the single address the Phase 23 resolver configuration takes.</summary>
    public IPv4Address? PrimaryDnsServer => DomainNameServers.Count > 0 ? DomainNameServers[0] : null;

    /// <summary>Returns a <see cref="Builder"/> pre-populated with this instance's values.</summary>
    public Builder ToBuilder() => new()
    {
        MessageType = MessageType,
        SubnetMask = SubnetMask,
        Router = Router,
        DomainNameServers = [.. DomainNameServers],
        RequestedIpAddress = RequestedIpAddress,
        ServerIdentifier = ServerIdentifier,
        IpAddressLeaseTime = IpAddressLeaseTime,
        RenewalTime = RenewalTime,
        RebindingTime = RebindingTime,
        ParameterRequestList = [.. ParameterRequestList],
        ClientIdentifier = ClientIdentifier,
        HostName = HostName,
    };

    public override string ToString()
    {
        var parts = new List<string>();
        if (MessageType is { } mt)
        {
            parts.Add(mt.ToString());
        }

        if (SubnetMask is { } mask)
        {
            parts.Add($"mask={mask}");
        }

        if (Router is { } router)
        {
            parts.Add($"router={router}");
        }

        if (DomainNameServers.Count > 0)
        {
            parts.Add($"dns={string.Join(",", DomainNameServers)}");
        }

        if (RequestedIpAddress is { } req)
        {
            parts.Add($"requested={req}");
        }

        if (ServerIdentifier is { } sid)
        {
            parts.Add($"server={sid}");
        }

        if (IpAddressLeaseTime is { } lease)
        {
            parts.Add($"lease={(int)lease.TotalSeconds}s");
        }

        return $"[{string.Join(" ", parts)}]";
    }

    /// <summary>Mutable builder for a <see cref="DhcpOptions"/>. Set what the message needs, then <see cref="Build"/>.</summary>
    public sealed class Builder
    {
        public DhcpMessageType? MessageType { get; set; }

        public SubnetMask? SubnetMask { get; set; }

        public IPv4Address? Router { get; set; }

        public IReadOnlyList<IPv4Address> DomainNameServers { get; set; } = [];

        public IPv4Address? RequestedIpAddress { get; set; }

        public IPv4Address? ServerIdentifier { get; set; }

        public TimeSpan? IpAddressLeaseTime { get; set; }

        public TimeSpan? RenewalTime { get; set; }

        public TimeSpan? RebindingTime { get; set; }

        public IReadOnlyList<DhcpOptionCode> ParameterRequestList { get; set; } = [];

        public DhcpClientId? ClientIdentifier { get; set; }

        public string? HostName { get; set; }

        public DhcpOptions Build() => new(
            MessageType,
            SubnetMask,
            Router,
            [.. DomainNameServers],
            RequestedIpAddress,
            ServerIdentifier,
            IpAddressLeaseTime,
            RenewalTime,
            RebindingTime,
            [.. ParameterRequestList],
            ClientIdentifier,
            HostName);
    }
}
