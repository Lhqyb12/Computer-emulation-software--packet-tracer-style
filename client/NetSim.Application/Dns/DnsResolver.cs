using NetSim.Application.Services;
using NetSim.Core.Arp;
using NetSim.Core.Dns;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Topology;
using NetSim.Core.Transport;
using NetSim.Core.Udp;

namespace NetSim.Application.Dns;

/// <summary>
/// Default <see cref="IDnsResolver"/>. Orchestrates the workflow the brief lays out (section 21):
/// build the query, resolve the DNS server's MAC through ARP, encapsulate through UDP/IPv4/Ethernet,
/// let the destination device's <see cref="IDnsServer"/> (found via <see cref="IDnsServerService"/>)
/// build the response, transmit the response back the same way, verify the transaction id, follow
/// any CNAME hop the response did not already resolve, and cache the final answer. Every step calls
/// the real Phase 17-22 engines directly - no shortcuts - mirroring <c>PingService</c>.
/// </summary>
public sealed class DnsResolver : IDnsResolver
{
    private readonly IUdpLayer _udp;
    private readonly IIPv4Layer _ipv4;
    private readonly IArpLayer _arp;
    private readonly IEthernetTransmissionService _ethernet;
    private readonly INetworkService _networkService;
    private readonly IDnsServerService _dnsServerService;
    private readonly IDnsClientConfigurationStore _configuration;
    private readonly IDnsCache _cache;
    private readonly DnsTransactionTracker _transactions = new();

    public DnsResolver(
        IUdpLayer udp, IIPv4Layer ipv4, IArpLayer arp, IEthernetTransmissionService ethernet, INetworkService networkService,
        IDnsServerService dnsServerService, IDnsClientConfigurationStore configuration, IDnsCache cache)
    {
        ArgumentNullException.ThrowIfNull(udp);
        ArgumentNullException.ThrowIfNull(ipv4);
        ArgumentNullException.ThrowIfNull(arp);
        ArgumentNullException.ThrowIfNull(ethernet);
        ArgumentNullException.ThrowIfNull(networkService);
        ArgumentNullException.ThrowIfNull(dnsServerService);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(cache);

        _udp = udp;
        _ipv4 = ipv4;
        _arp = arp;
        _ethernet = ethernet;
        _networkService = networkService;
        _dnsServerService = dnsServerService;
        _configuration = configuration;
        _cache = cache;
    }

    public event EventHandler<DnsEventArgs>? QueryCreated;

    public event EventHandler<DnsEventArgs>? QuerySent;

    public event EventHandler<DnsEventArgs>? ResponseReceived;

    public event EventHandler<DnsEventArgs>? ResolutionStarted;

    public event EventHandler<DnsEventArgs>? ResolutionSucceeded;

    public event EventHandler<DnsEventArgs>? ResolutionFailed;

    public event EventHandler<DnsEventArgs>? CacheHit;

    public event EventHandler<DnsEventArgs>? CacheMiss;

    public event EventHandler<DnsEventArgs>? CnameFollowed;

    public event EventHandler<DnsEventArgs>? ServerUnavailable;

    public event EventHandler<DnsEventArgs>? InvalidResponse;

    public DnsResolutionResult Resolve(NetworkInterface sourceInterface, DomainName name, DnsRecordType type = DnsRecordType.A, bool useCache = true)
    {
        ArgumentNullException.ThrowIfNull(sourceInterface);

        ResolutionStarted?.Invoke(this, new DnsEventArgs(name: name, recordType: type));

        if (useCache && _cache.TryGet(sourceInterface.Device, name, type, out var cached))
        {
            CacheHit?.Invoke(this, new DnsEventArgs(name: name, recordType: type, records: cached));
            var result = DnsResolutionResult.Success(name, type, cached, fromCache: true);
            ResolutionSucceeded?.Invoke(this, new DnsEventArgs(name: name, recordType: type, records: cached));
            return result;
        }

        CacheMiss?.Invoke(this, new DnsEventArgs(name: name, recordType: type));

        var servers = _configuration.GetServers(sourceInterface.Device);
        if (servers.Count == 0)
        {
            return Fail(DnsResolutionStatus.NoConfiguredServer, name, type, "No DNS server is configured on this device.");
        }

        return ResolveViaServer(sourceInterface, servers[0], originalName: name, currentName: name, type, [], useCache);
    }

    private DnsResolutionResult ResolveViaServer(
        NetworkInterface sourceInterface, IPv4Address serverIp, DomainName originalName, DomainName currentName, DnsRecordType type,
        HashSet<DomainName> visited, bool useCache)
    {
        if (!visited.Add(currentName))
        {
            return Fail(DnsResolutionStatus.CnameLoop, originalName, type, $"CNAME loop detected while resolving '{currentName}'.");
        }

        if (visited.Count > DnsProtocol.MaxCnameChainDepth)
        {
            return Fail(
                DnsResolutionStatus.MaxCnameDepthExceeded, originalName, type,
                $"Exceeded the maximum CNAME chain depth ({DnsProtocol.MaxCnameChainDepth}) while resolving '{originalName}'.");
        }

        if (_networkService.CurrentNetwork is not { } topology)
        {
            return Fail(DnsResolutionStatus.Error, originalName, type, "No network is currently open.");
        }

        if (sourceInterface.PrimaryIPv4Configuration is not { } sourceConfig)
        {
            return Fail(DnsResolutionStatus.Error, originalName, type, $"No IPv4 address is configured on interface '{sourceInterface.Name}'.");
        }

        if (!sourceConfig.Network.Contains(serverIp))
        {
            return Fail(
                DnsResolutionStatus.NoRoute, originalName, type,
                $"DNS server {serverIp} is outside {sourceInterface.Name}'s local subnet ({sourceConfig.Network}) and no routing is available yet.");
        }

        var transactionId = _transactions.Begin();
        var query = DnsMessage.CreateQuery(transactionId, currentName, type);
        QueryCreated?.Invoke(this, new DnsEventArgs(message: query, name: currentName, recordType: type, transactionId: transactionId));

        var sourcePort = Port.Create(Random.Shared.Next(49152, Port.MaxValue + 1));
        var datagram = _udp.CreateDatagram(sourceConfig.Address, serverIp, sourcePort, DnsProtocol.Port, query);
        var requestIp = _udp.Encapsulate(datagram, sourceConfig.Address, serverIp);

        var destinationMac = ResolveMac(topology, sourceInterface, serverIp, out var arpFailureDetail);
        if (destinationMac is null)
        {
            _transactions.TryComplete(transactionId);
            return ServerUnreachable(originalName, type, transactionId, arpFailureDetail ?? $"DNS server {serverIp} is unreachable.");
        }

        var requestFrame = _ipv4.Encapsulate(requestIp, sourceInterface.MacAddress!.Value, destinationMac.Value);
        var requestTx = _ethernet.Transmit(topology, sourceInterface, requestFrame);
        if (!requestTx.IsSuccess)
        {
            _transactions.TryComplete(transactionId);
            return ServerUnreachable(originalName, type, transactionId, requestTx.Detail ?? "The DNS query could not be sent.");
        }

        QuerySent?.Invoke(this, new DnsEventArgs(message: query, name: currentName, recordType: type, transactionId: transactionId));

        _ipv4.Process(requestTx.Packet!);
        var destinationInterface = requestTx.DestinationInterface!;
        var deliveredRequestFrame = (EthernetFrame)requestTx.Packet!.Payload!;
        if (!_ipv4.TryDecapsulate(deliveredRequestFrame, out var deliveredRequestIp))
        {
            _transactions.TryComplete(transactionId);
            return Fail(DnsResolutionStatus.Error, originalName, type, "The delivered frame did not carry an IPv4 packet.");
        }

        var udpReport = _udp.HandleIncoming(destinationInterface, deliveredRequestIp!);
        if (!udpReport.IsSuccess || !udpReport.IsDelivered || udpReport.Datagram!.Payload is not DnsMessage receivedQuery
            || !_dnsServerService.TryGetServer(destinationInterface.Device, out var dnsServer) || dnsServer is null)
        {
            _transactions.TryComplete(transactionId);
            return ServerUnreachable(originalName, type, transactionId, $"No DNS server is listening at {serverIp}:{DnsProtocol.Port}.");
        }

        var response = dnsServer.HandleQuery(receivedQuery, sourceConfig.Address);

        // Return leg: server -> client, exactly mirroring the outbound leg above.
        var responseDatagram = _udp.CreateDatagram(serverIp, sourceConfig.Address, DnsProtocol.Port, sourcePort, response);
        var responseIp = _udp.Encapsulate(responseDatagram, serverIp, sourceConfig.Address);

        var replyMac = ResolveMac(topology, destinationInterface, sourceConfig.Address, out var replyFailureDetail);
        if (replyMac is null)
        {
            _transactions.TryComplete(transactionId);
            return Fail(DnsResolutionStatus.Error, originalName, type, replyFailureDetail ?? "The DNS response could not be routed back to the client.");
        }

        var responseFrame = _ipv4.Encapsulate(responseIp, destinationInterface.MacAddress!.Value, replyMac.Value);
        var responseTx = _ethernet.Transmit(topology, destinationInterface, responseFrame);
        if (!responseTx.IsSuccess)
        {
            _transactions.TryComplete(transactionId);
            return Fail(DnsResolutionStatus.Error, originalName, type, responseTx.Detail ?? "The DNS response could not be sent back.");
        }

        _ipv4.Process(responseTx.Packet!);
        var deliveredResponseFrame = (EthernetFrame)responseTx.Packet!.Payload!;
        if (!_ipv4.TryDecapsulate(deliveredResponseFrame, out var deliveredResponseIp))
        {
            _transactions.TryComplete(transactionId);
            return Fail(DnsResolutionStatus.Error, originalName, type, "The delivered response frame did not carry an IPv4 packet.");
        }

        var responseUdpReport = _udp.HandleIncoming(sourceInterface, deliveredResponseIp!);
        if (!responseUdpReport.IsSuccess || responseUdpReport.Datagram!.Payload is not DnsMessage receivedResponse)
        {
            _transactions.TryComplete(transactionId);
            InvalidResponse?.Invoke(this, new DnsEventArgs(name: originalName, recordType: type, transactionId: transactionId));
            return Fail(DnsResolutionStatus.InvalidResponse, originalName, type, "The DNS response was not a valid DNS message.");
        }

        ResponseReceived?.Invoke(this, new DnsEventArgs(
            message: receivedResponse, name: originalName, recordType: type, transactionId: receivedResponse.Header.TransactionId));

        if (receivedResponse.Header.TransactionId != transactionId || !_transactions.TryComplete(transactionId))
        {
            InvalidResponse?.Invoke(this, new DnsEventArgs(
                name: originalName, recordType: type, transactionId: transactionId, detail: "Transaction ID mismatch."));
            return Fail(DnsResolutionStatus.InvalidResponse, originalName, type, "The DNS response transaction ID did not match the query.");
        }

        var code = receivedResponse.Header.ResponseCode;
        if (code == DnsResponseCode.NameError)
        {
            return Fail(DnsResolutionStatus.NxDomain, originalName, type, $"'{currentName}' does not exist.", code, transactionId);
        }

        if (code != DnsResponseCode.NoError)
        {
            var status = code == DnsResponseCode.ServerFailure ? DnsResolutionStatus.ServerFailure : DnsResolutionStatus.Refused;
            return Fail(status, originalName, type, $"The DNS server returned {code} for '{currentName}'.", code, transactionId);
        }

        var direct = receivedResponse.Answers.Where(a => a.Type == type).ToList();
        if (direct.Count > 0)
        {
            if (useCache)
            {
                _cache.Put(sourceInterface.Device, originalName, type, direct);
            }

            var result = DnsResolutionResult.Success(originalName, type, direct, fromCache: false, transactionId);
            ResolutionSucceeded?.Invoke(this, new DnsEventArgs(name: originalName, recordType: type, records: direct, transactionId: transactionId));
            return result;
        }

        var cnameHop = receivedResponse.Answers.OfType<DnsCnameRecord>().LastOrDefault();
        if (cnameHop is not null)
        {
            CnameFollowed?.Invoke(this, new DnsEventArgs(record: cnameHop, name: currentName, recordType: type, transactionId: transactionId));
            return ResolveViaServer(sourceInterface, serverIp, originalName, cnameHop.Target, type, visited, useCache);
        }

        return Fail(DnsResolutionStatus.NoData, originalName, type, $"'{currentName}' has no {type} records.", code, transactionId);
    }

    private DnsResolutionResult ServerUnreachable(DomainName name, DnsRecordType type, ushort transactionId, string detail)
    {
        ServerUnavailable?.Invoke(this, new DnsEventArgs(name: name, recordType: type, transactionId: transactionId, detail: detail));
        return Fail(DnsResolutionStatus.ServerUnavailable, name, type, detail);
    }

    private DnsResolutionResult Fail(
        DnsResolutionStatus status, DomainName name, DnsRecordType type, string message, DnsResponseCode? code = null, ushort? transactionId = null)
    {
        var result = DnsResolutionResult.Failure(status, name, type, message, code, transactionId);
        ResolutionFailed?.Invoke(this, new DnsEventArgs(name: name, recordType: type, responseCode: code, transactionId: transactionId, detail: message));
        return result;
    }

    /// <summary>Resolves <paramref name="targetIp"/>'s MAC from <paramref name="fromInterface"/>, driving a full ARP request/reply round trip on a cache miss. Mirrors <c>PingService.ResolveMac</c>.</summary>
    private MacAddress? ResolveMac(Network topology, NetworkInterface fromInterface, IPv4Address targetIp, out string? failureDetail)
    {
        var resolution = _arp.Resolve(fromInterface, targetIp);
        if (resolution.IsResolved)
        {
            failureDetail = null;
            return resolution.HardwareAddress;
        }

        if (resolution.HasFailed)
        {
            failureDetail = resolution.FailureReason ?? "ARP resolution failed.";
            return null;
        }

        var requestTx = _ethernet.Transmit(topology, fromInterface, resolution.RequestFrame!);
        if (!requestTx.IsSuccess)
        {
            failureDetail = requestTx.Detail ?? "The ARP request could not be sent.";
            return null;
        }

        var arpRequestFrame = (EthernetFrame)requestTx.Packet!.Payload!;
        var arpReport = _arp.HandleIncoming(requestTx.DestinationInterface!, arpRequestFrame);
        if (!arpReport.ReplyGenerated)
        {
            failureDetail = $"No device on the link answered the ARP request for {targetIp}.";
            return null;
        }

        var replyTx = _ethernet.Transmit(topology, requestTx.DestinationInterface!, arpReport.ReplyFrame!);
        if (!replyTx.IsSuccess)
        {
            failureDetail = replyTx.Detail ?? "The ARP reply could not be sent back.";
            return null;
        }

        var arpReplyFrame = (EthernetFrame)replyTx.Packet!.Payload!;
        _arp.HandleIncoming(replyTx.DestinationInterface!, arpReplyFrame);

        if (!fromInterface.ArpCache.TryGet(targetIp, out var entry))
        {
            failureDetail = $"ARP resolution for {targetIp} did not complete.";
            return null;
        }

        failureDetail = null;
        return entry.HardwareAddress;
    }
}
