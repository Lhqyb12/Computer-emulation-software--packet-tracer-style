using System.Linq;
using NetSim.Core.Common;
using NetSim.Core.Networking;
using NetSim.Core.Routing;

namespace NetSim.Core.Devices;

/// <summary>
/// A router - the Layer 3 forwarding device. Phase 27 gives it a <see cref="RoutingTable"/>: the
/// IPv4 routing table the <see cref="IRoutingEngine"/> consults to pick an outgoing interface and
/// next hop for a destination address.
///
/// <list type="bullet">
/// <item>The table's <see cref="RouteType.Connected"/> routes are <em>derived</em> from the
/// router's own interface IPv4 configuration - one connected route per configured address's
/// network - and are kept in sync by <see cref="SyncConnectedRoutes"/> (called automatically when
/// an interface is added/removed, by the routing engine before every decision, and by the
/// application layer after an address change). A connected route becomes inactive, without being
/// deleted, when its interface goes down.</item>
/// <item>Static and dynamic routes are Phase 28/29 - the model and the table support them, nothing
/// here creates them.</item>
/// </list>
///
/// There is deliberately <b>no</b> switch-style MAC address table on a router (brief section 56):
/// it uses its interface MACs, the per-interface ARP cache and this routing table. The routing
/// table is runtime state, rebuilt from the persisted interface configuration on load - it is
/// never itself saved.
/// </summary>
public class Router : NetworkDevice
{
    public Router(string name)
        : base(name, DeviceType.Router)
    {
        WireConnectedRouteSync();
    }

    // Persistence rehydration only - see NetworkDeviceFactory.RehydrateWithoutDefaults.
    internal Router(EntityId id, string name)
        : base(id, name, DeviceType.Router)
    {
        WireConnectedRouteSync();
    }

    /// <summary>
    /// This router's IPv4 routing table. Its connected routes mirror the router's interface
    /// configuration (see the type remarks and <see cref="SyncConnectedRoutes"/>). Runtime state -
    /// reset on load, never saved.
    /// </summary>
    public RoutingTable RoutingTable { get; } = new();

    /// <summary>
    /// Rebuilds the table's <see cref="RouteType.Connected"/> routes from the current interface
    /// configuration: one connected route per configured IPv4 address, for that address's network,
    /// out that interface. Idempotent, and it never touches non-connected routes. A stale connected
    /// route (from an address that was changed or removed) is dropped; a route whose interface is
    /// down stays in the table but reports <see cref="Route.IsActive"/> == false.
    /// </summary>
    public void SyncConnectedRoutes()
    {
        var connected = new List<Route>();
        var seenNetworks = new HashSet<IPv4Network>();

        foreach (var networkInterface in Interfaces)
        {
            foreach (var configuration in networkInterface.IPv4Configurations)
            {
                var network = configuration.Network;

                // /32 host addresses and, more importantly, an overlapping address on a second
                // interface would produce two connected routes for one prefix. Keep the first
                // deterministically (interface order) - ValidateAddressing surfaces the conflict.
                if (network.PrefixLength == 32 || !seenNetworks.Add(network))
                {
                    continue;
                }

                connected.Add(Route.Connected(networkInterface, network));
            }
        }

        RoutingTable.ReplaceConnectedRoutes(connected);
    }

    /// <summary>
    /// Checks the router's interface addressing for the ambiguities that would make routing
    /// non-deterministic (brief section 28) and returns a human-readable message for each. An empty
    /// list means the addressing is unambiguous. Today it flags two interfaces configured into the
    /// same directly-connected network (each physical interface is meant to be a separate network).
    /// </summary>
    public IReadOnlyList<string> ValidateAddressing()
    {
        var issues = new List<string>();
        var networkToInterface = new Dictionary<IPv4Network, string>();

        foreach (var networkInterface in Interfaces)
        {
            foreach (var configuration in networkInterface.IPv4Configurations)
            {
                var network = configuration.Network;
                if (networkToInterface.TryGetValue(network, out var existing))
                {
                    if (existing != networkInterface.Name)
                    {
                        issues.Add(
                            $"Interfaces '{existing}' and '{networkInterface.Name}' are both in network {network} - " +
                            "each router interface should connect a separate network.");
                    }
                }
                else
                {
                    networkToInterface[network] = networkInterface.Name;
                }
            }
        }

        return issues;
    }

    private void WireConnectedRouteSync()
    {
        InterfaceAdded += (_, _) => SyncConnectedRoutes();
        InterfaceRemoved += (_, _) => SyncConnectedRoutes();
    }
}
