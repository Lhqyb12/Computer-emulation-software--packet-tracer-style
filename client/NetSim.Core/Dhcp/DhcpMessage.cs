using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Dhcp;

/// <summary>
/// A simulated DHCP message (brief section 22) carried in a BOOTP frame. Like
/// <see cref="Dns.DnsMessage"/>/<see cref="Udp.UdpDatagram"/>/<see cref="Icmp.IcmpMessage"/> it is a
/// leaf <see cref="IPacketPayload"/> - it rides as the application payload of a
/// <see cref="Udp.UdpDatagram"/> (client port 68 -&gt; server port 67, or the reverse) and never
/// wraps another protocol layer.
///
/// The fixed BOOTP fields (<c>op</c>, <c>xid</c>, <c>ciaddr</c>/<c>yiaddr</c>/<c>siaddr</c>/<c>giaddr</c>,
/// <c>chaddr</c>) are modelled directly; everything the DHCP workflow negotiates (message type,
/// mask, router, DNS, lease time, ...) lives in the strongly-typed <see cref="DhcpOptions"/>.
/// Obscure BOOTP fields the workflow never reads (<c>secs</c> aside, <c>sname</c>/<c>file</c>) are
/// carried for structural completeness only.
///
/// <see cref="Length"/> is an estimated size (a fixed BOOTP part plus a small per-option estimate),
/// not a real RFC 2131 byte serializer - consistent with every other payload type in this codebase.
/// </summary>
public sealed class DhcpMessage : IPacketPayload
{
    /// <summary>Simulated size of the fixed BOOTP portion (op..chaddr..sname..file plus the magic cookie).</summary>
    private const int FixedSectionSize = 240;

    private DhcpMessage(
        DhcpOpcode opcode,
        byte hardwareType,
        byte hardwareAddressLength,
        byte hops,
        uint transactionId,
        ushort secondsElapsed,
        bool broadcastFlag,
        IPv4Address clientIpAddress,
        IPv4Address yourIpAddress,
        IPv4Address serverIpAddress,
        IPv4Address gatewayIpAddress,
        MacAddress clientHardwareAddress,
        string? serverHostName,
        string? bootFileName,
        DhcpOptions options)
    {
        Opcode = opcode;
        HardwareType = hardwareType;
        HardwareAddressLength = hardwareAddressLength;
        Hops = hops;
        TransactionId = transactionId;
        SecondsElapsed = secondsElapsed;
        BroadcastFlag = broadcastFlag;
        ClientIpAddress = clientIpAddress;
        YourIpAddress = yourIpAddress;
        ServerIpAddress = serverIpAddress;
        GatewayIpAddress = gatewayIpAddress;
        ClientHardwareAddress = clientHardwareAddress;
        ServerHostName = serverHostName;
        BootFileName = bootFileName;
        Options = options;
    }

    // ---- BOOTP fields ----

    public DhcpOpcode Opcode { get; }

    public byte HardwareType { get; }

    public byte HardwareAddressLength { get; }

    public byte Hops { get; }

    /// <summary>The 32-bit transaction id - the client matches OFFER to DISCOVER and ACK/NAK to REQUEST on this (brief section 23).</summary>
    public uint TransactionId { get; }

    public ushort SecondsElapsed { get; }

    /// <summary>The BOOTP broadcast flag - set by a client that cannot yet receive a unicast reply.</summary>
    public bool BroadcastFlag { get; }

    /// <summary><c>ciaddr</c> - the client's current address (0.0.0.0 until BOUND; set when renewing).</summary>
    public IPv4Address ClientIpAddress { get; }

    /// <summary><c>yiaddr</c> - the address the server is offering/assigning to the client.</summary>
    public IPv4Address YourIpAddress { get; }

    /// <summary><c>siaddr</c> - the server's own address.</summary>
    public IPv4Address ServerIpAddress { get; }

    /// <summary><c>giaddr</c> - the relay gateway address (always 0.0.0.0 in this phase - no relay/routing yet).</summary>
    public IPv4Address GatewayIpAddress { get; }

    /// <summary><c>chaddr</c> - the client's hardware address, the fallback client identity (brief section 21).</summary>
    public MacAddress ClientHardwareAddress { get; }

    public string? ServerHostName { get; }

    public string? BootFileName { get; }

    public DhcpOptions Options { get; }

    /// <summary>The DHCP message type from option 53 (brief section 25) - null for a structurally broken message.</summary>
    public DhcpMessageType? MessageType => Options.MessageType;

    /// <summary>The client identifier this message presents: the explicit option 61 if given, otherwise derived from <c>chaddr</c>.</summary>
    public DhcpClientId ClientId => Options.ClientIdentifier ?? DhcpClientId.FromHardwareAddress(ClientHardwareAddress);

    // ---- Factories (brief sections 5-8, 18, 19, 20) ----

    /// <summary>Builds a DISCOVER (client -&gt; broadcast).</summary>
    public static DhcpMessage CreateDiscover(uint transactionId, MacAddress clientHardwareAddress, DhcpOptions? options = null)
    {
        var builder = (options ?? DhcpOptions.Empty).ToBuilder();
        builder.MessageType = DhcpMessageType.Discover;
        return Request(transactionId, clientHardwareAddress, IPv4Address.Any, broadcastFlag: true, builder.Build());
    }

    /// <summary>Builds an OFFER (server -&gt; client).</summary>
    public static DhcpMessage CreateOffer(
        uint transactionId, MacAddress clientHardwareAddress, IPv4Address offeredAddress, IPv4Address serverAddress, DhcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var builder = options.ToBuilder();
        builder.MessageType = DhcpMessageType.Offer;
        return Reply(transactionId, clientHardwareAddress, offeredAddress, serverAddress, builder.Build());
    }

    /// <summary>Builds a REQUEST (client -&gt; broadcast, or unicast when renewing). Pass <paramref name="clientIpAddress"/> for a renewal.</summary>
    public static DhcpMessage CreateRequest(
        uint transactionId, MacAddress clientHardwareAddress, DhcpOptions options, IPv4Address clientIpAddress = default, bool broadcastFlag = true)
    {
        ArgumentNullException.ThrowIfNull(options);
        var builder = options.ToBuilder();
        builder.MessageType = DhcpMessageType.Request;
        return Request(transactionId, clientHardwareAddress, clientIpAddress, broadcastFlag, builder.Build());
    }

    /// <summary>Builds an ACK (server -&gt; client).</summary>
    public static DhcpMessage CreateAck(
        uint transactionId, MacAddress clientHardwareAddress, IPv4Address assignedAddress, IPv4Address serverAddress, DhcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var builder = options.ToBuilder();
        builder.MessageType = DhcpMessageType.Ack;
        return Reply(transactionId, clientHardwareAddress, assignedAddress, serverAddress, builder.Build());
    }

    /// <summary>Builds a NAK (server -&gt; client): no assigned address, just "start over".</summary>
    public static DhcpMessage CreateNak(uint transactionId, MacAddress clientHardwareAddress, IPv4Address serverAddress, string? reason = null)
    {
        var options = new DhcpOptions.Builder
        {
            MessageType = DhcpMessageType.Nak,
            ServerIdentifier = serverAddress,
            HostName = reason,
        }.Build();

        return Reply(transactionId, clientHardwareAddress, IPv4Address.Any, serverAddress, options);
    }

    /// <summary>Builds a RELEASE (client -&gt; server, unicast). <paramref name="clientIpAddress"/> is the address being given up.</summary>
    public static DhcpMessage CreateRelease(
        uint transactionId, MacAddress clientHardwareAddress, IPv4Address clientIpAddress, IPv4Address serverAddress)
    {
        var options = new DhcpOptions.Builder
        {
            MessageType = DhcpMessageType.Release,
            ServerIdentifier = serverAddress,
            ClientIdentifier = DhcpClientId.FromHardwareAddress(clientHardwareAddress),
        }.Build();

        return Request(transactionId, clientHardwareAddress, clientIpAddress, broadcastFlag: false, options);
    }

    /// <summary>Builds a DECLINE (client -&gt; broadcast): the offered/assigned <paramref name="declinedAddress"/> is unusable.</summary>
    public static DhcpMessage CreateDecline(
        uint transactionId, MacAddress clientHardwareAddress, IPv4Address declinedAddress, IPv4Address serverAddress)
    {
        var options = new DhcpOptions.Builder
        {
            MessageType = DhcpMessageType.Decline,
            RequestedIpAddress = declinedAddress,
            ServerIdentifier = serverAddress,
            ClientIdentifier = DhcpClientId.FromHardwareAddress(clientHardwareAddress),
        }.Build();

        return Request(transactionId, clientHardwareAddress, IPv4Address.Any, broadcastFlag: true, options);
    }

    private static DhcpMessage Request(
        uint transactionId, MacAddress chaddr, IPv4Address clientIpAddress, bool broadcastFlag, DhcpOptions options) =>
        new(DhcpOpcode.BootRequest, DhcpProtocol.EthernetHardwareType, DhcpProtocol.EthernetHardwareAddressLength, hops: 0,
            transactionId, secondsElapsed: 0, broadcastFlag,
            clientIpAddress, IPv4Address.Any, IPv4Address.Any, IPv4Address.Any, chaddr, serverHostName: null, bootFileName: null, options);

    private static DhcpMessage Reply(
        uint transactionId, MacAddress chaddr, IPv4Address yourIpAddress, IPv4Address serverAddress, DhcpOptions options) =>
        new(DhcpOpcode.BootReply, DhcpProtocol.EthernetHardwareType, DhcpProtocol.EthernetHardwareAddressLength, hops: 0,
            transactionId, secondsElapsed: 0, broadcastFlag: false,
            IPv4Address.Any, yourIpAddress, serverAddress, IPv4Address.Any, chaddr, serverHostName: null, bootFileName: null, options);

    // ---- IPacketPayload ----

    public string PayloadType => "DHCP";

    public IPacketPayload? EncapsulatedPayload => null;

    public int Length => FixedSectionSize + EstimateOptionsSize();

    /// <summary>Structural self-check (brief section 67): a valid opcode, a unicast client hardware address, and a message type option.</summary>
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

        Check(Opcode != DhcpOpcode.None, "DHCP opcode is not set.");
        Check(ClientHardwareAddress.IsUnicast, "DHCP client hardware address must be a unicast MAC.");
        Check(Options.MessageType is not null, "DHCP message is missing option 53 (message type).");
        Check(HardwareAddressLength == DhcpProtocol.EthernetHardwareAddressLength, "DHCP hardware address length must be 6 for Ethernet.");

        return errors is null ? PacketValidationResult.Valid : PacketValidationResult.Invalid(errors.ToArray());
    }

    private int EstimateOptionsSize()
    {
        var size = 3; // option 53 (message type)
        size += Options.SubnetMask is null ? 0 : 6;
        size += Options.Router is null ? 0 : 6;
        size += Options.DomainNameServers.Count == 0 ? 0 : 2 + (Options.DomainNameServers.Count * 4);
        size += Options.RequestedIpAddress is null ? 0 : 6;
        size += Options.ServerIdentifier is null ? 0 : 6;
        size += Options.IpAddressLeaseTime is null ? 0 : 6;
        size += Options.RenewalTime is null ? 0 : 6;
        size += Options.RebindingTime is null ? 0 : 6;
        size += Options.ParameterRequestList.Count == 0 ? 0 : 2 + Options.ParameterRequestList.Count;
        size += Options.ClientIdentifier is null ? 0 : 9;
        size += string.IsNullOrEmpty(Options.HostName) ? 0 : 2 + Options.HostName!.Length;
        size += 1; // option 255 (end)
        return size;
    }

    public override string ToString() =>
        $"DHCP {Opcode} xid=0x{TransactionId:X8} {MessageType?.ToString() ?? "?"} chaddr={ClientHardwareAddress}" +
        (YourIpAddress.IsUnspecified ? string.Empty : $" yiaddr={YourIpAddress}") + $" {Options}";
}
