using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;

namespace NetSim.Core.Topology;

/// <summary>
/// Iterative BFS implementation of <see cref="ITopologyQueryService"/>. Stateless - one shared
/// instance is safe to reuse. Traversal is queue-based (never recursive) so a large or deeply
/// chained topology cannot overflow the stack. Neighbour expansion goes through
/// <see cref="ITopologyView.GetNeighbors"/>, which is index-backed on both the live network and a
/// snapshot, so each query is O(V + E).
/// </summary>
public sealed class TopologyQueryService : ITopologyQueryService
{
    public bool HasPath(ITopologyView topology, NetworkDevice source, NetworkDevice destination)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        RequireRegistered(topology, source);
        RequireRegistered(topology, destination);

        if (source.Id == destination.Id)
        {
            return true;
        }

        var visited = new HashSet<EntityId> { source.Id };
        var queue = new Queue<NetworkDevice>();
        queue.Enqueue(source);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in topology.GetNeighbors(current))
            {
                if (neighbor.Id == destination.Id)
                {
                    return true;
                }

                if (visited.Add(neighbor.Id))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return false;
    }

    public IReadOnlyList<NetworkDevice> FindPath(ITopologyView topology, NetworkDevice source, NetworkDevice destination)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        RequireRegistered(topology, source);
        RequireRegistered(topology, destination);

        if (source.Id == destination.Id)
        {
            return new[] { source };
        }

        // BFS carrying a parent map, so the first time we reach the destination we have a
        // shortest (fewest-hop) path to reconstruct.
        var parents = new Dictionary<EntityId, NetworkDevice> { [source.Id] = source };
        var queue = new Queue<NetworkDevice>();
        queue.Enqueue(source);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in topology.GetNeighbors(current))
            {
                if (parents.ContainsKey(neighbor.Id))
                {
                    continue;
                }

                parents[neighbor.Id] = current;
                if (neighbor.Id == destination.Id)
                {
                    return ReconstructPath(parents, source, destination);
                }

                queue.Enqueue(neighbor);
            }
        }

        return [];
    }

    public IReadOnlyList<IReadOnlyList<NetworkDevice>> GetConnectedComponents(ITopologyView topology)
    {
        ArgumentNullException.ThrowIfNull(topology);

        var visited = new HashSet<EntityId>();
        var components = new List<IReadOnlyList<NetworkDevice>>();

        foreach (var start in topology.Devices)
        {
            if (!visited.Add(start.Id))
            {
                continue;
            }

            var component = new List<NetworkDevice> { start };
            var queue = new Queue<NetworkDevice>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in topology.GetNeighbors(current))
                {
                    if (visited.Add(neighbor.Id))
                    {
                        component.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            components.Add(component.AsReadOnly());
        }

        return components.AsReadOnly();
    }

    public IReadOnlyList<NetworkDevice> GetIsolatedDevices(ITopologyView topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        return topology.Devices.Where(d => topology.GetConnections(d).Count == 0).ToList().AsReadOnly();
    }

    private static IReadOnlyList<NetworkDevice> ReconstructPath(
        Dictionary<EntityId, NetworkDevice> parents,
        NetworkDevice source,
        NetworkDevice destination)
    {
        var path = new List<NetworkDevice>();
        var current = destination;
        while (current.Id != source.Id)
        {
            path.Add(current);
            current = parents[current.Id];
        }

        path.Add(source);
        path.Reverse();
        return path.AsReadOnly();
    }

    private static void RequireRegistered(ITopologyView topology, NetworkDevice device)
    {
        if (!topology.ContainsDevice(device.Id))
        {
            throw new DomainException($"Device '{device.Name}' is not part of this topology.");
        }
    }
}
