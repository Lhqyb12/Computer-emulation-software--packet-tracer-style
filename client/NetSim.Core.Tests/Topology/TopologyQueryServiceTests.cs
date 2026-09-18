using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Topology;

namespace NetSim.Core.Tests.Topology;

/// <summary>
/// Structural graph queries (<see cref="TopologyQueryService"/>) across the topology shapes the
/// Phase 15 brief calls out: two-device, linear, star, multiple components, cycles and isolated
/// devices - plus a sanity performance check that the common operations do not blow up on a few
/// hundred devices.
/// </summary>
public class TopologyQueryServiceTests
{
    private readonly TopologyQueryService _query = new();

    private static NetworkDevice AddNode(Network network, string name, int ports = 8)
    {
        var device = new Switch(name);
        for (var i = 0; i < ports; i++)
        {
            device.AddInterface($"Fa0/{i}", InterfaceType.FastEthernet);
        }

        network.AddDevice(device);
        return device;
    }

    private static void Link(Network network, NetworkDevice a, NetworkDevice b) =>
        network.Connect(a.Interfaces.First(i => !i.IsConnected), b.Interfaces.First(i => !i.IsConnected));

    // ---------------------------------------------------------------------------------------
    // HasPath
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void HasPath_TwoDirectlyConnectedDevices()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        Link(network, a, b);

        Assert.True(_query.HasPath(network, a, b));
    }

    [Fact]
    public void HasPath_LinearChain_EndToEnd()
    {
        var network = new Network("Lab");
        var pc0 = AddNode(network, "PC0");
        var sw = AddNode(network, "Switch0");
        var r0 = AddNode(network, "Router0");
        Link(network, pc0, sw);
        Link(network, sw, r0);

        Assert.True(_query.HasPath(network, pc0, r0));
    }

    [Fact]
    public void HasPath_False_AcrossSeparateComponents()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        var x = AddNode(network, "X");
        var y = AddNode(network, "Y");
        Link(network, a, b);
        Link(network, x, y);

        Assert.False(_query.HasPath(network, a, x));
    }

    [Fact]
    public void HasPath_DeviceToItself_IsTrue()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");

        Assert.True(_query.HasPath(network, a, a));
    }

    [Fact]
    public void HasPath_ThroughACycle()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        var c = AddNode(network, "C");
        var d = AddNode(network, "D");
        Link(network, a, b);
        Link(network, b, c);
        Link(network, c, a); // cycle a-b-c
        Link(network, c, d);

        Assert.True(_query.HasPath(network, a, d));
    }

    [Fact]
    public void HasPath_Throws_ForDeviceNotInTopology()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");

        Assert.Throws<DomainException>(() => _query.HasPath(network, a, new Switch("outsider")));
    }

    // ---------------------------------------------------------------------------------------
    // FindPath
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void FindPath_ReturnsHopsInOrder_ForALinearTopology()
    {
        var network = new Network("Lab");
        var pc0 = AddNode(network, "PC0");
        var sw = AddNode(network, "Switch0");
        var r0 = AddNode(network, "Router0");
        Link(network, pc0, sw);
        Link(network, sw, r0);

        var path = _query.FindPath(network, pc0, r0);

        Assert.Equal(new[] { "PC0", "Switch0", "Router0" }, path.Select(d => d.Name));
    }

    [Fact]
    public void FindPath_PicksAShortestPath_WhenACycleOffersTwoRoutes()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        var c = AddNode(network, "C");
        var d = AddNode(network, "D");
        // Square: a-b-c-d-a. Shortest a->c is 2 hops either way round.
        Link(network, a, b);
        Link(network, b, c);
        Link(network, c, d);
        Link(network, d, a);

        var path = _query.FindPath(network, a, c);

        Assert.Equal(3, path.Count);
        Assert.Equal("A", path[0].Name);
        Assert.Equal("C", path[^1].Name);
        Assert.True(path[1].Name is "B" or "D");
    }

    [Fact]
    public void FindPath_EmptyWhenNoRouteExists()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        var lonely = AddNode(network, "Lonely");
        Link(network, a, b);

        Assert.Empty(_query.FindPath(network, a, lonely));
    }

    [Fact]
    public void FindPath_DeviceToItself_IsSingleElement()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");

        var path = _query.FindPath(network, a, a);

        Assert.Single(path);
        Assert.Same(a, path[0]);
    }

    // ---------------------------------------------------------------------------------------
    // Connected components / isolated devices
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void GetConnectedComponents_StarTopology_IsOneComponent()
    {
        var network = new Network("Lab");
        var hub = AddNode(network, "Switch0");
        var leaves = Enumerable.Range(0, 5).Select(i => AddNode(network, $"PC{i}")).ToList();
        foreach (var leaf in leaves)
        {
            Link(network, hub, leaf);
        }

        var components = _query.GetConnectedComponents(network);

        Assert.Single(components);
        Assert.Equal(6, components[0].Count);
    }

    [Fact]
    public void GetConnectedComponents_SeparatesDisjointSubgraphs_AndCountsIsolatedDevicesAsTheirOwn()
    {
        var network = new Network("Lab");
        var r0 = AddNode(network, "Router0");
        var sw = AddNode(network, "Switch0");
        var pc0 = AddNode(network, "PC0");
        var r1 = AddNode(network, "Router1");
        var pc1 = AddNode(network, "PC1");
        var floating = AddNode(network, "Floating");

        Link(network, r0, sw);
        Link(network, sw, pc0);
        Link(network, r1, pc1);

        var components = _query.GetConnectedComponents(network);

        Assert.Equal(3, components.Count);
        Assert.Contains(components, c => c.Count == 3 && c.Any(d => d.Name == "Router0"));
        Assert.Contains(components, c => c.Count == 2 && c.Any(d => d.Name == "Router1"));
        Assert.Contains(components, c => c.Count == 1 && c[0].Name == "Floating");
        Assert.NotNull(floating);
    }

    [Fact]
    public void GetConnectedComponents_EmptyTopology_IsEmpty()
    {
        Assert.Empty(_query.GetConnectedComponents(new Network("Lab")));
    }

    [Fact]
    public void GetIsolatedDevices_ReturnsOnlyDevicesWithNoCables()
    {
        var network = new Network("Lab");
        var r0 = AddNode(network, "Router0");
        var sw = AddNode(network, "Switch0");
        var pc0 = AddNode(network, "PC0");
        Link(network, r0, sw);

        var isolated = _query.GetIsolatedDevices(network);

        Assert.Single(isolated);
        Assert.Same(pc0, isolated[0]);
    }

    [Fact]
    public void GetIsolatedDevices_AfterRemovingItsOnlyConnection()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        network.Connect(a.Interfaces.First(), b.Interfaces.First());
        var connection = network.Connections.Single();

        Assert.Empty(_query.GetIsolatedDevices(network));

        network.RemoveConnection(connection);

        Assert.Equal(2, _query.GetIsolatedDevices(network).Count);
    }

    // ---------------------------------------------------------------------------------------
    // Snapshot parity + performance
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void QueriesRunIdenticallyAgainstALiveNetworkAndItsSnapshot()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        var c = AddNode(network, "C");
        Link(network, a, b);
        Link(network, b, c);

        var snapshot = network.CreateSnapshot();

        Assert.Equal(_query.HasPath(network, a, c), _query.HasPath(snapshot, a, c));
        Assert.Equal(
            _query.FindPath(network, a, c).Select(d => d.Id),
            _query.FindPath(snapshot, a, c).Select(d => d.Id));
        Assert.Equal(
            _query.GetConnectedComponents(network).Count,
            _query.GetConnectedComponents(snapshot).Count);
    }

    [Fact]
    public void CommonOperations_StayFast_OnAFewHundredDevices()
    {
        // Not a benchmark - just a guard against an accidental O(n^2)/O(n^3) implementation.
        const int deviceCount = 400;
        var network = new Network("Big");
        var devices = new List<NetworkDevice>(deviceCount);
        for (var i = 0; i < deviceCount; i++)
        {
            devices.Add(AddNode(network, $"D{i}", ports: 4));
        }

        // A long spine plus some cross links (cycles).
        for (var i = 0; i < deviceCount - 1; i++)
        {
            Link(network, devices[i], devices[i + 1]);
        }

        for (var i = 0; i < deviceCount - 50; i += 50)
        {
            Link(network, devices[i], devices[i + 50]);
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        Assert.True(_query.HasPath(network, devices[0], devices[^1]));
        var path = _query.FindPath(network, devices[0], devices[^1]);
        var components = _query.GetConnectedComponents(network);
        var isolated = _query.GetIsolatedDevices(network);
        var stats = network.GetStatistics();
        var validation = network.Validate();

        stopwatch.Stop();

        Assert.NotEmpty(path);
        Assert.Single(components);
        Assert.Empty(isolated);
        Assert.Equal(deviceCount, stats.DeviceCount);
        Assert.True(validation.IsValid);
        Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"Topology queries took {stopwatch.ElapsedMilliseconds} ms");
    }
}
