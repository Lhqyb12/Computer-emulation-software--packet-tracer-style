using Microsoft.Extensions.DependencyInjection;
using NetSim.Application.Canvas;
using NetSim.Application.Services;
using NetSim.Application.State;
using NetSim.Application.Diagnostics;
using NetSim.Application.Dhcp;
using NetSim.Application.Dns;
using NetSim.Core.Arp;
using NetSim.Core.Dhcp;
using NetSim.Core.Dns;
using NetSim.Core.Ethernet;
using NetSim.Core.Icmp;
using NetSim.Core.IP;
using NetSim.Core.Packets;
using NetSim.Core.Routing;
using NetSim.Core.Switching;
using NetSim.Core.Tcp;
using NetSim.Core.Topology;
using NetSim.Core.Udp;
using NetSim.Application.Routing;

namespace NetSim.Application.DependencyInjection;

/// <summary>
/// Composition entry point for the application layer. Future use-case and workflow
/// services (project management, simulation orchestration, ...) are registered here as
/// they are introduced.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Singletons: this is a single-window desktop app with no request/scope
        // boundary, so "one shared instance for the process" is the natural lifetime
        // for both the current-network/selection state (which must be shared for it to
        // mean anything) and the stateless service coordinators (which hold no mutable
        // state of their own - all of it lives in IApplicationState/ISelectionState -
        // so there's no correctness reason to create a new instance per consumer).
        services.AddSingleton<IApplicationState, ApplicationState>();
        services.AddSingleton<ISelectionState, SelectionState>();
        services.AddSingleton<ICanvasState, CanvasState>();
        services.AddSingleton<ICanvasItemsState, CanvasItemsState>();
        services.AddSingleton<ICanvasSelectionState, CanvasSelectionState>();
        services.AddSingleton<IDevicePlacementState, DevicePlacementState>();

        // Stateless structural-graph query engine (BFS/DFS: paths, components, isolation).
        // Shared instance - it holds no state of its own.
        services.AddSingleton<ITopologyQueryService, TopologyQueryService>();

        // The packet engine (Phase 16): creates/tracks packets, drives their lifecycle and runs
        // them through processors. Singleton for the same reason as the state holders - the set
        // of in-flight packets must be shared to mean anything. Protocol-agnostic; Ethernet and
        // later protocol layers plug in as IPacketProcessor implementations, not changes here.
        services.AddSingleton<IPacketEngine, PacketEngine>();

        // The Ethernet layer (Phase 17). The processor is a stateless IPacketProcessor; the
        // transmission service bridges the topology and the packet engine to move one frame across
        // one cable. Both are singletons - stateless apart from event subscribers.
        services.AddSingleton<EthernetProcessor>();
        services.AddSingleton<IEthernetTransmissionService, EthernetTransmissionService>();

        // The Layer 2 switching engine (Phase 25). ISimulationClock is the deterministic time
        // source MAC-table aging uses (SystemSimulationClock in production - a monotonic process
        // stopwatch; a ManualSimulationClock in tests). ISwitchingEngine makes the per-switch
        // forwarding decision (learn source MAC, then known-unicast forward / unknown-unicast +
        // broadcast + multicast flood / drop) against each Switch's own MacAddressTable - there is
        // no network-wide table. ISwitchedSegmentService drives that decision through the existing
        // single-hop IEthernetTransmissionService pipeline, so a frame transparently crosses one or
        // more switches with simulation-only loop safety (not STP). All singletons - stateless
        // apart from event subscribers.
        services.AddSingleton<ISimulationClock, SystemSimulationClock>();
        services.AddSingleton<ISwitchingEngine, SwitchingEngine>();
        services.AddSingleton<ISwitchedSegmentService, SwitchedSegmentService>();

        // The IPv4 layer (Phase 18). The processor is a stateless IPacketProcessor that handles a
        // packet at L3 (accepting a bare IPv4 packet or an Ethernet frame with EtherType IPv4);
        // the layer service bridges IPv4 <-> Ethernet encapsulation and raises IPv4 packet events.
        // Both singletons - stateless apart from event subscribers. No routing / ARP / ICMP here.
        services.AddSingleton<IPv4Processor>();
        services.AddSingleton<IIPv4Layer, IPv4Layer>();

        // The IPv6 layer (Phase 19). Same shape as the IPv4 layer: a stateless IPacketProcessor
        // that handles a packet at L3 (a bare IPv6 packet or an Ethernet frame with EtherType
        // IPv6) plus a layer service that bridges IPv6 <-> Ethernet encapsulation and raises IPv6
        // packet events. Both singletons - stateless apart from event subscribers. IPv4 and IPv6
        // are separate Layer 3 protocols and coexist; no routing / NDP / ICMPv6 / SLAAC here.
        services.AddSingleton<IPv6Processor>();
        services.AddSingleton<IIPv6Layer, IPv6Layer>();

        // The ARP engine (Phase 20). The processor is a stateless IPacketProcessor that
        // structurally validates an ARP message (bare, or inside an EtherType.ARP frame); the layer
        // service builds requests/replies, bridges ARP <-> Ethernet (broadcast request, unicast
        // reply), runs the cache-lookup-then-request resolution flow against a NetworkInterface's
        // own ArpCache, and raises ARP events. Both singletons - stateless apart from event
        // subscribers. ARP is IPv4-only; IPv6 uses Neighbor Discovery (a later phase), never this.
        services.AddSingleton<ArpProcessor>();
        services.AddSingleton<IArpLayer, ArpLayer>();

        // The ICMP engine (Phase 21). The processor is a stateless IPacketProcessor that
        // structurally validates an ICMP message (bare, or inside an IPv4 packet with
        // Protocol.Icmp, optionally itself inside an Ethernet frame) and checks its checksum; the
        // layer service builds Echo Request/Reply (plus a Destination Unreachable / Time Exceeded
        // foundation), bridges ICMP <-> IPv4 encapsulation, and decides whether a received Echo
        // Request is answered (the receiving interface must own the IPv4 destination). Both
        // singletons - stateless apart from event subscribers. ICMP never gets its own EtherType;
        // no routing / TCP / UDP / traceroute here.
        services.AddSingleton<IcmpProcessor>();
        services.AddSingleton<IIcmpLayer, IcmpLayer>();

        // The routing engine (Phase 27). IRoutingEngine makes the per-router Layer 3 forwarding
        // decision (own-address local delivery / longest-prefix match / TTL decrement / no-route /
        // TTL-expired) against each Router's own RoutingTable - there is no network-wide table, and
        // the routing table itself is a per-Router domain object (like a Switch's MacAddressTable),
        // not a DI service. IRouterForwardingService drives that decision on the wire: it resolves
        // the next hop through the existing ARP engine, rebuilds the Ethernet frame with the
        // router's outgoing-interface MAC and transmits it through ISwitchedSegmentService, and
        // generates the ICMP error a drop owes the source. Both singletons - stateless apart from
        // event subscribers. Switching decides on destination MAC; routing decides on destination IP.
        services.AddSingleton<IRoutingEngine, RoutingEngine>();
        services.AddSingleton<IRouterForwardingService, RouterForwardingService>();

        // Static routing (Phase 28). IStaticRouteService is the one door the UI uses to add / edit /
        // remove a router's manually-configured static and default routes: it validates the input,
        // turns it into a Core Route and mutates the router's own RoutingTable through its public
        // AddRoute / RemoveRoute API. Recursive next-hop resolution, longest-prefix match and route
        // usability all reuse the Phase 27 engine - this adds no routing logic of its own. Singleton,
        // stateless apart from its change event.
        services.AddSingleton<IStaticRouteService, StaticRouteService>();

        // The UDP layer (Phase 22). The processor is a stateless IPacketProcessor that structurally
        // validates a UDP datagram wrapped in an IPv4 packet (protocol UDP) and checks its
        // pseudo-header checksum; the delivery manager is a plain per-device socket table
        // (bind/unbind/deliver); the layer service bridges UDP <-> IPv4 encapsulation, confirms
        // destination ownership and drives delivery through the manager. All three singletons -
        // stateless apart from event subscribers / the socket table itself. UDP is connectionless:
        // no handshake, no sequencing, no retransmission.
        services.AddSingleton<UdpProcessor>();
        services.AddSingleton<IUdpDeliveryManager, UdpDeliveryManager>();
        services.AddSingleton<IUdpLayer, UdpLayer>();

        // The TCP layer (Phase 22). The processor is a stateless IPacketProcessor that structurally
        // validates a TCP segment wrapped in an IPv4 packet (protocol TCP) and checks its
        // pseudo-header checksum; the connection manager owns every listening socket and active
        // connection and drives the three-way handshake / data transfer / termination / reset state
        // machine; the layer service bridges TCP <-> IPv4 encapsulation, gates an inbound segment
        // (structure, checksum, destination ownership) and delegates the state-machine decision to
        // the connection manager. The initial-sequence-number generator defaults to a random one;
        // it is registered separately so tests can substitute a deterministic generator. All
        // singletons - the connection manager's mutable state (listeners/connections) must be
        // shared for it to mean anything, exactly like IApplicationState.
        services.AddSingleton<IInitialSequenceNumberGenerator, RandomInitialSequenceNumberGenerator>();
        services.AddSingleton<TcpProcessor>();
        services.AddSingleton<ITcpConnectionManager, TcpConnectionManager>();
        services.AddSingleton<ITcpLayer, TcpLayer>();

        services.AddSingleton<INetworkService, NetworkService>();
        services.AddSingleton<IDeviceService, DeviceService>();
        services.AddSingleton<IConnectionService, ConnectionService>();
        services.AddSingleton<IProjectService, ProjectService>();

        // The Ping diagnostic tool (Phase 21): orchestrates ARP resolution + ICMP echo +
        // Ethernet transmission end to end inside the simulator (never the real OS network) and
        // reports strongly-typed results. Stateless - all state is the returned PingSessionResult.
        services.AddSingleton<IPingService, PingService>();

        // DNS (Phase 23): the first application-layer protocol built on top of the Phase 22
        // transport layer. IDnsServer/DnsZone/DnsRecordStore are per-device domain objects created
        // by application code (exactly like NetworkDevice/Connection are not DI singletons either) -
        // only the host-scoped tables and the stateless orchestrators are registered here.
        // IDnsCache and IDnsClientConfigurationStore are device-keyed tables, the same shape as
        // IUdpDeliveryManager; IDnsServerService binds a device's UDP (and optionally TCP) port 53
        // to whichever IDnsServer instance is hosted there; IDnsResolver drives the full
        // cache -> query -> ARP -> Ethernet -> IPv4 -> UDP -> server -> response -> CNAME/cache
        // workflow end to end inside the simulator, mirroring IPingService.
        services.AddSingleton<IDnsCache, DnsCache>();
        services.AddSingleton<IDnsClientConfigurationStore, DnsClientConfigurationStore>();
        services.AddSingleton<IDnsServerService, DnsServerService>();
        services.AddSingleton<IDnsResolver, DnsResolver>();

        // DHCP (Phase 24): the second application-layer protocol built on the Phase 22 transport
        // layer, and the first that writes into another layer's configuration (the Phase 18 IPv4
        // interface model and the Phase 23 DNS client store). As with DNS, IDhcpServer /
        // DhcpServerConfiguration / DhcpLeaseManager are per-device domain objects an application
        // workflow creates directly - only the host-scoped tables and stateless orchestrators are
        // registered here. IDhcpTransactionIdSource is registered separately so tests can substitute
        // a deterministic id source (mirroring IInitialSequenceNumberGenerator for TCP);
        // IDhcpClientStateStore is a per-interface table the same shape as IUdpDeliveryManager;
        // IDhcpServerService binds a device's UDP port 67 to whichever IDhcpServer is hosted there;
        // IDhcpClient drives the full DISCOVER -> OFFER -> REQUEST -> ACK -> BOUND workflow (plus
        // renew / release / expiry) end to end inside the simulator, mirroring IDnsResolver.
        services.AddSingleton<IDhcpTransactionIdSource, RandomDhcpTransactionIdSource>();
        services.AddSingleton<IDhcpClientStateStore, DhcpClientStateStore>();
        services.AddSingleton<IDhcpServerService, DhcpServerService>();
        services.AddSingleton<IDhcpClient, DhcpClient>();

        return services;
    }
}
