namespace NetSim.Core.Dhcp;

/// <summary>
/// The DHCP option codes this simulator understands (brief section 24). Real RFC 2132 numbers,
/// used for familiarity and as the values a <see cref="DhcpMessageType.Discover"/>/<see cref="DhcpMessageType.Request"/>
/// carries in its Parameter Request List. The strongly-typed values themselves live on
/// <see cref="DhcpOptions"/> rather than being hand-encoded into <see cref="DhcpMessage"/>.
/// </summary>
public enum DhcpOptionCode : byte
{
    /// <summary>Option 1 - the subnet mask for the assigned address.</summary>
    SubnetMask = 1,

    /// <summary>Option 3 - the default gateway(s). This simulator carries a single router address.</summary>
    Router = 3,

    /// <summary>Option 6 - the DNS server(s) the client should use (the Phase 23 integration point).</summary>
    DomainNameServer = 6,

    /// <summary>Option 12 - the client's host name.</summary>
    HostName = 12,

    /// <summary>Option 50 - the address a client is asking to be (re)assigned.</summary>
    RequestedIpAddress = 50,

    /// <summary>Option 51 - the lease duration, in seconds.</summary>
    IpAddressLeaseTime = 51,

    /// <summary>Option 53 - the <see cref="DhcpMessageType"/>.</summary>
    DhcpMessageType = 53,

    /// <summary>Option 54 - the address of the server that owns this transaction.</summary>
    ServerIdentifier = 54,

    /// <summary>Option 55 - the list of option codes a client wants the server to include.</summary>
    ParameterRequestList = 55,

    /// <summary>Option 58 - T1, when the client should begin renewing (seconds).</summary>
    RenewalTimeValue = 58,

    /// <summary>Option 59 - T2, when the client should begin rebinding (seconds).</summary>
    RebindingTimeValue = 59,

    /// <summary>Option 61 - an explicit client identifier (this simulator derives one from the MAC when absent).</summary>
    ClientIdentifier = 61,

    /// <summary>Option 255 - end of options.</summary>
    End = 255,
}
