using NetSim.Application.Services;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Dhcp;
using NetSim.Core.Dns;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Switching;
using NetSim.Core.Topology;
using NetSim.Core.Udp;

namespace NetSim.Application.Dhcp;

/// <summary>
/// Default <see cref="IDhcpClient"/>. Orchestrates the workflow the brief lays out (sections 5-8):
/// build the DISCOVER, broadcast it across the local segment through the real UDP/IPv4/Ethernet
/// engines, collect OFFERs, pick one deterministically (the first received - see the class remarks
/// in docs), broadcast a REQUEST naming the chosen server, and on ACK validate and atomically apply
/// the configuration to the interface (Phase 18 model) and the device's DNS list (Phase 23 store).
/// Every hop calls the real Core engines directly - no shortcuts - mirroring <c>DnsResolver</c>.
///
/// <para><b>Bootstrap / broadcast (brief sections 4, 34, 35).</b> During acquisition the client has
/// no address: the DISCOVER/REQUEST go out with IPv4 source <c>0.0.0.0</c>, destination
/// <c>255.255.255.255</c>, UDP 68 -&gt; 67, and an Ethernet broadcast destination MAC. The existing
/// Ethernet layer only does a single physical hop, so this client sends the broadcast frame out of
/// each of the client device's operational interfaces through <see cref="ISwitchedSegmentService"/>
/// (Phase 25), which floods it across any switches on the segment to every attached device -
/// including a DHCP server reachable only through a switch. On a segment with no switch this is
/// identical to a single direct Ethernet hop.</para>
/// </summary>
public sealed class DhcpClient : IDhcpClient
{
    private readonly IUdpLayer _udp;
    private readonly IIPv4Layer _ipv4;
    private readonly IEthernetTransmissionService _ethernet;
    private readonly ISwitchedSegmentService _segment;
    private readonly INetworkService _networkService;
    private readonly IDhcpServerService _dhcpServerService;
    private readonly IDnsClientConfigurationStore _dnsConfiguration;
    private readonly IDhcpClientStateStore _stateStore;
    private readonly IDhcpTransactionIdSource _transactionIds;
    private readonly TimeProvider _timeProvider;

    public DhcpClient(
        IUdpLayer udp,
        IIPv4Layer ipv4,
        IEthernetTransmissionService ethernet,
        INetworkService networkService,
        IDhcpServerService dhcpServerService,
        IDnsClientConfigurationStore dnsConfiguration,
        IDhcpClientStateStore stateStore,
        IDhcpTransactionIdSource transactionIds,
        TimeProvider? timeProvider = null,
        ISwitchedSegmentService? segment = null)
    {
        ArgumentNullException.ThrowIfNull(udp);
        ArgumentNullException.ThrowIfNull(ipv4);
        ArgumentNullException.ThrowIfNull(ethernet);
        ArgumentNullException.ThrowIfNull(networkService);
        ArgumentNullException.ThrowIfNull(dhcpServerService);
        ArgumentNullException.ThrowIfNull(dnsConfiguration);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(transactionIds);

        _udp = udp;
        _ipv4 = ipv4;
        _ethernet = ethernet;
        _segment = segment ?? new SwitchedSegmentService(_ethernet, new SwitchingEngine());
        _networkService = networkService;
        _dhcpServerService = dhcpServerService;
        _dnsConfiguration = dnsConfiguration;
        _stateStore = stateStore;
        _transactionIds = transactionIds;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public event EventHandler<DhcpEventArgs>? ClientStarted;
    public event EventHandler<DhcpEventArgs>? DiscoverCreated;
    public event EventHandler<DhcpEventArgs>? DiscoverSent;
    public event EventHandler<DhcpEventArgs>? OfferReceived;
    public event EventHandler<DhcpEventArgs>? RequestCreated;
    public event EventHandler<DhcpEventArgs>? RequestSent;
    public event EventHandler<DhcpEventArgs>? AckReceived;
    public event EventHandler<DhcpEventArgs>? NakReceived;
    public event EventHandler<DhcpEventArgs>? LeaseBound;
    public event EventHandler<DhcpEventArgs>? LeaseRenewed;
    public event EventHandler<DhcpEventArgs>? LeaseReleased;
    public event EventHandler<DhcpEventArgs>? LeaseExpired;
    public event EventHandler<DhcpEventArgs>? ConfigurationApplied;
    public event EventHandler<DhcpEventArgs>? ServerUnavailable;
    public event EventHandler<DhcpEventArgs>? PoolExhausted;

    private DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public DhcpAcquireResult Acquire(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);

        var clientId = DhcpClientId.FromHardwareAddress(networkInterface.MacAddress ?? MacAddress.Zero);

        if (_networkService.CurrentNetwork is not { } topology)
        {
            return DhcpAcquireResult.Failure(DhcpAcquireStatus.NoNetwork, clientId, "No network is currently open.");
        }

        if (!networkInterface.SupportsEthernet || networkInterface.MacAddress is null)
        {
            return DhcpAcquireResult.Failure(DhcpAcquireStatus.InterfaceUnusable, clientId,
                $"Interface '{networkInterface.Name}' is not an Ethernet interface with a MAC address.");
        }

        if (!networkInterface.IsOperational)
        {
            return DhcpAcquireResult.Failure(DhcpAcquireStatus.InterfaceUnusable, clientId,
                $"Interface '{networkInterface.Name}' is not operational (administratively down or no link).");
        }

        var binding = _stateStore.GetOrCreate(networkInterface);
        binding.IsDhcpManaged = true;

        var transactionId = _transactionIds.Next();
        binding.LastTransactionId = transactionId;
        binding.State = DhcpClientState.Selecting;
        ClientStarted?.Invoke(this, new DhcpEventArgs(
            clientId: clientId, transactionId: transactionId, clientState: DhcpClientState.Selecting,
            detail: $"DHCP started on {networkInterface.Device.Name}/{networkInterface.Name}."));

        // ---- DISCOVER -> OFFER ----
        var discoverOptions = new DhcpOptions.Builder
        {
            ClientIdentifier = clientId,
            ParameterRequestList = [DhcpOptionCode.SubnetMask, DhcpOptionCode.Router, DhcpOptionCode.DomainNameServer],
        }.Build();
        var discover = DhcpMessage.CreateDiscover(transactionId, networkInterface.MacAddress.Value, discoverOptions);
        DiscoverCreated?.Invoke(this, new DhcpEventArgs(discover, clientId: clientId, transactionId: transactionId, detail: discover.ToString()));

        var offers = Broadcast(topology, networkInterface, discover, IPv4Address.Any)
            .Where(r => r.Message.MessageType == DhcpMessageType.Offer && r.Message.TransactionId == transactionId)
            .ToList();
        DiscoverSent?.Invoke(this, new DhcpEventArgs(discover, clientId: clientId, transactionId: transactionId, detail: $"DISCOVER broadcast; {offers.Count} offer(s) received."));

        if (offers.Count == 0)
        {
            binding.State = DhcpClientState.Init;
            ServerUnavailable?.Invoke(this, new DhcpEventArgs(
                clientId: clientId, transactionId: transactionId, detail: "No DHCP server offered a configuration."));
            return DhcpAcquireResult.Failure(DhcpAcquireStatus.NoOffer, clientId, "No DHCP server responded to the DISCOVER.", transactionId);
        }

        // Deterministic selection: the first offer received (iteration follows connection
        // registration order - documented, never nondeterministic - brief sections 36-37).
        var selectedOffer = offers[0].Message;
        var selectedServer = selectedOffer.Options.ServerIdentifier ?? selectedOffer.ServerIpAddress;
        OfferReceived?.Invoke(this, new DhcpEventArgs(
            selectedOffer, clientId: clientId, address: selectedOffer.YourIpAddress, transactionId: transactionId,
            detail: $"OFFER {selectedOffer.YourIpAddress} from {selectedServer}."));

        return SendRequest(
            topology, networkInterface, binding, clientId, transactionId,
            requestedAddress: selectedOffer.YourIpAddress, selectedServer: selectedServer,
            clientIpAddress: IPv4Address.Any, isRenewal: false);
    }

    public DhcpAcquireResult Renew(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);

        var clientId = DhcpClientId.FromHardwareAddress(networkInterface.MacAddress ?? MacAddress.Zero);

        if (!_stateStore.TryGet(networkInterface, out var binding) || binding.Configuration is not { } current || !binding.IsBound)
        {
            return DhcpAcquireResult.Failure(DhcpAcquireStatus.NotBound, clientId, "The interface is not currently bound to a DHCP lease.");
        }

        if (_networkService.CurrentNetwork is not { } topology)
        {
            return DhcpAcquireResult.Failure(DhcpAcquireStatus.NoNetwork, clientId, "No network is currently open.");
        }

        var transactionId = _transactionIds.Next();
        binding.LastTransactionId = transactionId;
        binding.State = DhcpClientState.Renewing;

        return SendRequest(
            topology, networkInterface, binding, clientId, transactionId,
            requestedAddress: current.Address, selectedServer: current.ServerIdentifier,
            clientIpAddress: current.Address, isRenewal: true);
    }

    private DhcpAcquireResult SendRequest(
        Network topology, NetworkInterface networkInterface, DhcpClientBinding binding, DhcpClientId clientId, uint transactionId,
        IPv4Address requestedAddress, IPv4Address selectedServer, IPv4Address clientIpAddress, bool isRenewal)
    {
        if (!isRenewal)
        {
            binding.State = DhcpClientState.Requesting;
        }

        var requestOptions = new DhcpOptions.Builder
        {
            RequestedIpAddress = requestedAddress,
            ServerIdentifier = selectedServer,
            ClientIdentifier = clientId,
            ParameterRequestList = [DhcpOptionCode.SubnetMask, DhcpOptionCode.Router, DhcpOptionCode.DomainNameServer],
        }.Build();
        var request = DhcpMessage.CreateRequest(transactionId, networkInterface.MacAddress!.Value, requestOptions, clientIpAddress, broadcastFlag: !isRenewal);
        RequestCreated?.Invoke(this, new DhcpEventArgs(request, clientId: clientId, transactionId: transactionId, detail: request.ToString()));

        var sourceAddress = isRenewal ? clientIpAddress : IPv4Address.Any;
        var replies = Broadcast(topology, networkInterface, request, sourceAddress)
            .Where(r => r.Message.TransactionId == transactionId)
            .ToList();
        RequestSent?.Invoke(this, new DhcpEventArgs(request, clientId: clientId, transactionId: transactionId, detail: $"REQUEST broadcast; {replies.Count} reply(ies)."));

        var nak = replies.FirstOrDefault(r => r.Message.MessageType == DhcpMessageType.Nak);
        if (nak is not null)
        {
            binding.State = DhcpClientState.Init;
            if (isRenewal)
            {
                WithdrawConfiguration(networkInterface, binding);
            }

            NakReceived?.Invoke(this, new DhcpEventArgs(nak.Message, clientId: clientId, transactionId: transactionId, detail: "Server rejected the REQUEST (NAK)."));
            return DhcpAcquireResult.Failure(DhcpAcquireStatus.Nak, clientId, "The DHCP server rejected the request with a NAK.", transactionId, nak.ServerAddress);
        }

        var ack = replies.FirstOrDefault(r => r.Message.MessageType == DhcpMessageType.Ack);
        if (ack is null)
        {
            // Renewal: the lease is still valid until it expires; stay bound and report the miss.
            binding.State = isRenewal ? DhcpClientState.Bound : DhcpClientState.Init;
            ServerUnavailable?.Invoke(this, new DhcpEventArgs(clientId: clientId, transactionId: transactionId, detail: "No ACK received for the REQUEST."));
            return DhcpAcquireResult.Failure(DhcpAcquireStatus.NoOffer, clientId, "The DHCP server did not acknowledge the request.", transactionId, selectedServer);
        }

        AckReceived?.Invoke(this, new DhcpEventArgs(ack.Message, clientId: clientId, address: ack.Message.YourIpAddress, transactionId: transactionId, detail: ack.Message.ToString()));

        var configuration = BuildConfiguration(ack.Message);
        if (configuration is null)
        {
            binding.State = isRenewal ? DhcpClientState.Bound : DhcpClientState.Init;
            return DhcpAcquireResult.Failure(
                DhcpAcquireStatus.InvalidConfiguration, clientId,
                "The ACK did not carry a usable configuration (missing subnet mask or invalid address).", transactionId, ack.ServerAddress);
        }

        if (!ApplyConfiguration(networkInterface, configuration))
        {
            binding.State = isRenewal ? DhcpClientState.Bound : DhcpClientState.Init;
            return DhcpAcquireResult.Failure(
                DhcpAcquireStatus.InvalidConfiguration, clientId, "The assigned address could not be applied to the interface.", transactionId, ack.ServerAddress);
        }

        binding.State = DhcpClientState.Bound;
        binding.Configuration = configuration;
        binding.Lease = new DhcpLease(clientId, configuration.Address, configuration.LeaseObtained, configuration.LeaseDuration, configuration.ServerIdentifier, DhcpLeaseState.Active);

        ConfigurationApplied?.Invoke(this, new DhcpEventArgs(
            ack.Message, clientId: clientId, address: configuration.Address, configuration: configuration, transactionId: transactionId,
            detail: $"Applied {configuration}."));

        if (isRenewal)
        {
            LeaseRenewed?.Invoke(this, new DhcpEventArgs(
                clientId: clientId, address: configuration.Address, configuration: configuration, lease: binding.Lease, transactionId: transactionId,
                detail: $"Lease {configuration.Address} renewed until {configuration.LeaseExpiration:u}."));
            return DhcpAcquireResult.Renewed(clientId, transactionId, configuration);
        }

        LeaseBound?.Invoke(this, new DhcpEventArgs(
            clientId: clientId, address: configuration.Address, configuration: configuration, lease: binding.Lease, transactionId: transactionId,
            clientState: DhcpClientState.Bound, detail: $"Bound to {configuration.Address}."));
        return DhcpAcquireResult.Bound(clientId, transactionId, configuration);
    }

    public DhcpReleaseResult Release(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);

        if (!_stateStore.TryGet(networkInterface, out var binding) || binding.Configuration is not { } configuration)
        {
            return DhcpReleaseResult.NothingToRelease;
        }

        var clientId = DhcpClientId.FromHardwareAddress(networkInterface.MacAddress ?? MacAddress.Zero);

        if (_networkService.CurrentNetwork is { } topology && networkInterface.MacAddress is not null)
        {
            var transactionId = _transactionIds.Next();
            var release = DhcpMessage.CreateRelease(transactionId, networkInterface.MacAddress.Value, configuration.Address, configuration.ServerIdentifier);
            // Best effort - RELEASE has no reply. Failures to reach the server still clear local state.
            _ = Broadcast(topology, networkInterface, release, configuration.Address);
        }

        var releasedAddress = configuration.Address;
        WithdrawConfiguration(networkInterface, binding);
        binding.State = DhcpClientState.Init;

        LeaseReleased?.Invoke(this, new DhcpEventArgs(
            clientId: clientId, address: releasedAddress, detail: $"Released {releasedAddress}."));
        return DhcpReleaseResult.Released(releasedAddress);
    }

    public bool IsLeaseExpired(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);
        return _stateStore.TryGet(networkInterface, out var binding)
            && binding.Configuration is { } configuration
            && configuration.IsExpired(UtcNow);
    }

    public bool AbandonExpiredLease(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);

        if (!_stateStore.TryGet(networkInterface, out var binding) || binding.Configuration is not { } configuration || !configuration.IsExpired(UtcNow))
        {
            return false;
        }

        var clientId = DhcpClientId.FromHardwareAddress(networkInterface.MacAddress ?? MacAddress.Zero);
        var address = configuration.Address;
        WithdrawConfiguration(networkInterface, binding);
        binding.State = DhcpClientState.Expired;

        LeaseExpired?.Invoke(this, new DhcpEventArgs(
            clientId: clientId, address: address, clientState: DhcpClientState.Expired, detail: $"Lease {address} expired; configuration withdrawn."));
        return true;
    }

    // ---- configuration application (brief sections 28-30, 44, 45) ----

    private DhcpNetworkConfiguration? BuildConfiguration(DhcpMessage ack)
    {
        var address = ack.YourIpAddress;
        if (address.IsUnspecified || address.IsMulticast || address.IsLimitedBroadcast)
        {
            return null;
        }

        if (ack.Options.SubnetMask is not { } mask)
        {
            return null;
        }

        var leaseDuration = ack.Options.IpAddressLeaseTime is { } lease && lease > TimeSpan.Zero ? lease : DhcpProtocol.DefaultLeaseDuration;
        var serverIdentifier = ack.Options.ServerIdentifier ?? ack.ServerIpAddress;

        try
        {
            var configuration = new DhcpNetworkConfiguration(
                address, mask, ack.Options.Router, ack.Options.PrimaryDnsServer, leaseDuration, serverIdentifier, UtcNow,
                ack.Options.RenewalTime, ack.Options.RebindingTime);

            // Validate the address is acceptable to the Phase 18 model before committing to anything.
            _ = configuration.ToInterfaceConfiguration();
            return configuration;
        }
        catch (DomainException)
        {
            return null;
        }
    }

    private bool ApplyConfiguration(NetworkInterface networkInterface, DhcpNetworkConfiguration configuration)
    {
        try
        {
            networkInterface.SetPrimaryIPv4Configuration(configuration.ToInterfaceConfiguration());
        }
        catch (DomainException)
        {
            return false;
        }

        if (configuration.DnsServer is { } dns)
        {
            _dnsConfiguration.SetServers(networkInterface.Device, [dns]);
        }

        return true;
    }

    private void WithdrawConfiguration(NetworkInterface networkInterface, DhcpClientBinding binding)
    {
        if (binding.Configuration is { } configuration)
        {
            networkInterface.RemoveIPv4Configuration(configuration.Address);

            // Only clear the DNS list if it is exactly what DHCP put there - never stomp a value
            // the operator set manually (brief section 45).
            if (configuration.DnsServer is { } dns)
            {
                var current = _dnsConfiguration.GetServers(networkInterface.Device);
                if (current.Count == 1 && current[0] == dns)
                {
                    _dnsConfiguration.ClearServers(networkInterface.Device);
                }
            }
        }

        binding.Configuration = null;
        binding.Lease = null;
    }

    // ---- the broadcast mechanism (brief sections 34-35), now switch-aware via ISwitchedSegmentService ----

    private sealed record DhcpServerReply(IPv4Address ServerAddress, DhcpMessage Message);

    private List<DhcpServerReply> Broadcast(Network topology, NetworkInterface primaryInterface, DhcpMessage message, IPv4Address sourceAddress)
    {
        var replies = new List<DhcpServerReply>();

        var datagram = _udp.CreateDatagram(sourceAddress, IPv4Address.Broadcast, DhcpProtocol.ClientPort, DhcpProtocol.ServerPort, message);
        var ipPacket = _udp.Encapsulate(datagram, sourceAddress, IPv4Address.Broadcast);
        var frame = _ipv4.Encapsulate(ipPacket, primaryInterface.MacAddress!.Value, MacAddress.Broadcast);

        var device = primaryInterface.Device;
        foreach (var connection in topology.GetConnections(device))
        {
            var nearInterface = connection.EndpointA.Device.Id == device.Id ? connection.EndpointA : connection.EndpointB;
            if (nearInterface.Device.Id != device.Id || !nearInterface.IsOperational)
            {
                continue;
            }

            // Flood the broadcast frame across the whole Layer 2 segment - through any switches -
            // to every attached device. With no switch this is a single direct hop.
            var segment = _segment.TransmitAcrossSegment(topology, nearInterface, frame);
            foreach (var delivery in segment.Deliveries)
            {
                var serverInterface = delivery.DestinationInterface;
                var deliveredFrame = delivery.Frame;

                if (!_ipv4.TryDecapsulate(deliveredFrame, out var deliveredIp)
                    || !_udp.TryDecapsulate(deliveredIp!, out var deliveredUdp)
                    || deliveredUdp!.DestinationPort != DhcpProtocol.ServerPort
                    || deliveredUdp.Payload is not DhcpMessage serverRequest
                    || !deliveredUdp.HasValidChecksum(deliveredIp!.SourceAddress, deliveredIp.DestinationAddress))
                {
                    continue;
                }

                if (!_dhcpServerService.TryGetServer(serverInterface.Device, out var server) || server is null)
                {
                    continue;
                }

                var serverReply = server.HandleMessage(serverRequest);
                if (serverReply is null)
                {
                    continue;
                }

                var decoded = TransmitReply(topology, serverInterface, primaryInterface, server.ServerAddress, serverReply);
                replies.Add(new DhcpServerReply(server.ServerAddress, decoded ?? serverReply));
            }
        }

        return replies;
    }

    /// <summary>Sends the server's reply back across the segment (through any switches) and returns the message as the client would decode it.</summary>
    private DhcpMessage? TransmitReply(
        Network topology, NetworkInterface serverInterface, NetworkInterface clientInterface, IPv4Address serverAddress, DhcpMessage reply)
    {
        var datagram = _udp.CreateDatagram(serverAddress, IPv4Address.Broadcast, DhcpProtocol.ServerPort, DhcpProtocol.ClientPort, reply);
        var ipPacket = _udp.Encapsulate(datagram, serverAddress, IPv4Address.Broadcast);
        var frame = _ipv4.Encapsulate(ipPacket, serverInterface.MacAddress!.Value, clientInterface.MacAddress!.Value);

        var segment = _segment.TransmitAcrossSegment(topology, serverInterface, frame);
        var delivery = segment.DeliveryTo(clientInterface) ?? segment.Deliveries.FirstOrDefault();
        if (delivery is null)
        {
            return null;
        }

        var deliveredFrame = delivery.Frame;

        if (!_ipv4.TryDecapsulate(deliveredFrame, out var deliveredIp)
            || !_udp.TryDecapsulate(deliveredIp!, out var deliveredUdp)
            || deliveredUdp!.Payload is not DhcpMessage deliveredMessage
            || !deliveredUdp.HasValidChecksum(deliveredIp!.SourceAddress, deliveredIp.DestinationAddress))
        {
            return null;
        }

        return deliveredMessage;
    }
}
