using NetSim.Core.Common;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Topology;

namespace NetSim.Core.Tests.Topology;

/// <summary>
/// Covers the Phase 15 topology-engine surface layered on top of <see cref="Network"/>: id
/// lookups, device/interface/connection relationships, neighbour and direct-connectivity
/// queries, change notifications + <see cref="Network.Version"/>, statistics, validation, and
/// that every invariant still holds after each supported mutation.
/// </summary>
public class NetworkTopologyEngineTests
{
    // A device carrying enough Gigabit ports to cable it a few times over.
    private static NetworkDevice AddNode(Network network, string name, int ports = 4)
    {
        var device = new Router(name);
        for (var i = 0; i < ports; i++)
        {
            device.AddInterface($"G0/{i}", InterfaceType.GigabitEthernet);
        }

        network.AddDevice(device);
        return device;
    }

    private static Connection Link(Network network, NetworkDevice a, NetworkDevice b)
    {
        var freeA = a.Interfaces.First(i => !i.IsConnected);
        var freeB = b.Interfaces.First(i => !i.IsConnected);
        return network.Connect(freeA, freeB);
    }

    // ---------------------------------------------------------------------------------------
    // Device management
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ContainsDevice_ByIdAndByReference_TrueOnlyForRegisteredDevices()
    {
        var network = new Network("Lab");
        var router = AddNode(network, "R0");
        var stranger = new Router("R1");

        Assert.True(network.ContainsDevice(router));
        Assert.True(network.ContainsDevice(router.Id));
        Assert.False(network.ContainsDevice(stranger));
        Assert.False(network.ContainsDevice(stranger.Id));
    }

    [Fact]
    public void GetDevice_ReturnsRegisteredDevice_OrNull()
    {
        var network = new Network("Lab");
        var router = AddNode(network, "R0");

        Assert.Same(router, network.GetDevice(router.Id));
        Assert.Null(network.GetDevice(EntityId.New()));
    }

    [Fact]
    public void DeviceCount_TracksAddAndRemove()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        AddNode(network, "B");

        Assert.Equal(2, network.Devices.Count);

        network.RemoveDevice(a);

        Assert.Single(network.Devices);
    }

    [Fact]
    public void RemoveDevice_NotInNetwork_ReturnsFalse()
    {
        var network = new Network("Lab");

        Assert.False(network.RemoveDevice(new Router("ghost")));
    }

    // ---------------------------------------------------------------------------------------
    // Interface lookup / relationships
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void GetInterface_ResolvesInterfacesAddedBothBeforeAndAfterRegistration()
    {
        var network = new Network("Lab");
        var router = new Router("R0");
        var beforeRegistration = router.AddInterface("G0/0", InterfaceType.GigabitEthernet);
        network.AddDevice(router);
        var afterRegistration = router.AddInterface("G0/1", InterfaceType.GigabitEthernet);

        Assert.Same(beforeRegistration, network.GetInterface(beforeRegistration.Id));
        Assert.Same(afterRegistration, network.GetInterface(afterRegistration.Id));
    }

    [Fact]
    public void GetInterface_ReturnsNull_AfterDeviceRemoved()
    {
        var network = new Network("Lab");
        var router = AddNode(network, "R0");
        var ifaceId = router.Interfaces.First().Id;

        network.RemoveDevice(router);

        Assert.Null(network.GetInterface(ifaceId));
    }

    [Fact]
    public void GetInterfaces_ReturnsTheDeviceOwnList()
    {
        var network = new Network("Lab");
        var router = AddNode(network, "R0", ports: 3);

        Assert.Equal(3, network.GetInterfaces(router).Count);
        Assert.Equal(router.Interfaces, network.GetInterfaces(router));
    }

    // ---------------------------------------------------------------------------------------
    // Connection lookup / relationships
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void GetConnection_ById_ResolvesRegisteredConnections()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        var connection = Link(network, a, b);

        Assert.Same(connection, network.GetConnection(connection.Id));
        Assert.True(network.ContainsConnection(connection.Id));
    }

    [Fact]
    public void GetConnections_ForDevice_ReturnsEveryLinkTouchingIt()
    {
        var network = new Network("Lab");
        var hub = AddNode(network, "Hub");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        Link(network, hub, a);
        Link(network, hub, b);

        Assert.Equal(2, network.GetConnections(hub).Count);
        Assert.Single(network.GetConnections(a));
    }

    [Fact]
    public void GetConnection_ForInterface_UsesTheInterfaceBackReference()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        var connection = Link(network, a, b);
        var connectedIface = a.Interfaces.First(i => i.IsConnected);

        Assert.Same(connection, network.GetConnection(connectedIface));
        Assert.Null(network.GetConnection(a.Interfaces.First(i => !i.IsConnected)));
    }

    // ---------------------------------------------------------------------------------------
    // Neighbours / direct connectivity
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void GetNeighbors_ReturnsDirectlyConnectedDevices_Distinct()
    {
        var network = new Network("Lab");
        var r0 = AddNode(network, "R0");
        var sw = AddNode(network, "Switch0");
        var r1 = AddNode(network, "R1");
        Link(network, r0, sw);
        Link(network, r0, r1);
        Link(network, r0, r1); // second cable to the same pair - still one neighbour

        var neighbors = network.GetNeighbors(r0);

        Assert.Equal(2, neighbors.Count);
        Assert.Contains(sw, neighbors);
        Assert.Contains(r1, neighbors);
    }

    [Fact]
    public void GetNeighbor_ForInterface_ReturnsFarDevice_OrNullWhenUncabled()
    {
        var network = new Network("Lab");
        var r0 = AddNode(network, "R0");
        var sw = AddNode(network, "Switch0");
        Link(network, r0, sw);

        Assert.Same(sw, network.GetNeighbor(r0.Interfaces.First(i => i.IsConnected)));
        Assert.Null(network.GetNeighbor(r0.Interfaces.First(i => !i.IsConnected)));
    }

    [Fact]
    public void AreDirectlyConnected_TrueOnlyForAnActualCable()
    {
        var network = new Network("Lab");
        var r0 = AddNode(network, "R0");
        var sw = AddNode(network, "Switch0");
        var pc1 = AddNode(network, "PC1");
        Link(network, r0, sw);

        Assert.True(network.AreDirectlyConnected(r0, sw));
        Assert.True(network.AreDirectlyConnected(sw, r0));
        Assert.False(network.AreDirectlyConnected(r0, pc1));
        Assert.False(network.AreDirectlyConnected(r0, r0));
    }

    [Fact]
    public void GetConnectionsBetween_SupportsMoreThanOneCablePerDevicePair()
    {
        var network = new Network("Lab");
        var r0 = AddNode(network, "R0");
        var r1 = AddNode(network, "R1");
        var first = Link(network, r0, r1);
        var second = Link(network, r0, r1);

        var between = network.GetConnectionsBetween(r0, r1);

        Assert.Equal(2, between.Count);
        Assert.Contains(first, between);
        Assert.Contains(second, between);
    }

    // ---------------------------------------------------------------------------------------
    // Change notifications + Version
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Mutations_RaiseSpecificAndGeneralEvents_AndAdvanceVersion()
    {
        var network = new Network("Lab");
        var changes = new List<TopologyChangeKind>();
        network.TopologyChanged += (_, e) => changes.Add(e.Kind);

        var startVersion = network.Version;

        var a = AddNode(network, "A", ports: 1);
        var b = AddNode(network, "B", ports: 1);
        var connection = network.Connect(a.Interfaces.First(), b.Interfaces.First());
        network.RemoveConnection(connection);
        network.RemoveDevice(a);

        Assert.Equal(
            new[]
            {
                TopologyChangeKind.DeviceAdded,
                TopologyChangeKind.DeviceAdded,
                TopologyChangeKind.ConnectionAdded,
                TopologyChangeKind.ConnectionRemoved,
                TopologyChangeKind.DeviceRemoved,
            },
            changes);
        Assert.Equal(startVersion + 5, network.Version);
    }

    [Fact]
    public void RemoveDevice_WithConnections_SurfacesOneDeviceRemoved_AndCleansConnectionIndex()
    {
        var network = new Network("Lab");
        var deviceEvents = new List<TopologyChangeKind>();
        var connectionEvents = 0;
        network.DeviceRemoved += (_, _) => deviceEvents.Add(TopologyChangeKind.DeviceRemoved);
        network.ConnectionRemoved += (_, _) => connectionEvents++;

        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        Link(network, a, b);

        network.RemoveDevice(a);

        // The cascaded connection teardown is folded into the single DeviceRemoved change.
        Assert.Single(deviceEvents);
        Assert.Equal(0, connectionEvents);
        Assert.Empty(network.Connections);
        Assert.Empty(network.GetConnections(b));
        Assert.DoesNotContain(b.Interfaces, i => i.IsConnected);
    }

    [Fact]
    public void FailedMutation_DoesNotAdvanceVersion()
    {
        var network = new Network("Lab");
        var router = AddNode(network, "R0");
        var version = network.Version;

        Assert.Throws<Core.Common.Exceptions.DomainException>(() => network.AddDevice(router));
        Assert.Equal(version, network.Version);
    }

    // ---------------------------------------------------------------------------------------
    // Statistics
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void GetStatistics_ReportsCountsComponentsAndIsolation()
    {
        var network = new Network("Lab");
        var r0 = AddNode(network, "R0", ports: 2);
        var sw = AddNode(network, "Switch0", ports: 2);
        AddNode(network, "Lonely", ports: 2);
        Link(network, r0, sw);

        var stats = network.GetStatistics();

        Assert.Equal(3, stats.DeviceCount);
        Assert.Equal(6, stats.InterfaceCount);
        Assert.Equal(1, stats.ConnectionCount);
        Assert.Equal(2, stats.ConnectedDeviceCount);
        Assert.Equal(1, stats.IsolatedDeviceCount);
        Assert.Equal(2, stats.ConnectedComponentCount); // {R0,Switch0} and {Lonely}
    }

    // ---------------------------------------------------------------------------------------
    // Validation + consistency
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Validate_HealthyTopology_ReportsNoIssues()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        var c = AddNode(network, "C");
        Link(network, a, b);
        Link(network, b, c);
        Link(network, c, a); // a cycle - still valid structure

        var result = network.Validate();

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_DetectsAnUntrackedConnectionLeftOnAnInterface()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");

        // Build a connection outside the topology and force-attach it: the interfaces now
        // reference a connection Network never registered.
        var rogue = Connection.Create(
            a.Interfaces.First(i => !i.IsConnected),
            b.Interfaces.First(i => !i.IsConnected));

        var result = network.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Kind == TopologyValidationIssueKind.UntrackedConnection);
        Assert.NotNull(rogue);
    }

    [Fact]
    public void Topology_StaysConsistent_AfterAddRemoveCycles()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        var c = AddNode(network, "C");

        for (var i = 0; i < 20; i++)
        {
            var ab = Link(network, a, b);
            var bc = Link(network, b, c);
            Assert.True(network.Validate().IsValid);

            network.RemoveConnection(ab);
            network.RemoveConnection(bc);
            Assert.True(network.Validate().IsValid);
        }

        Assert.Empty(network.Connections);
        Assert.DoesNotContain(a.Interfaces.Concat(b.Interfaces).Concat(c.Interfaces), x => x.IsConnected);
    }

    [Fact]
    public void RemoveConnection_ImmediatelyReflectedInEveryQuery()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        var connection = Link(network, a, b);

        network.RemoveConnection(connection);

        Assert.Null(network.GetConnection(connection.Id));
        Assert.False(network.ContainsConnection(connection.Id));
        Assert.Empty(network.GetConnections(a));
        Assert.Empty(network.GetNeighbors(a));
        Assert.False(network.AreDirectlyConnected(a, b));
        Assert.DoesNotContain(connection, network.Connections);
    }

    // ---------------------------------------------------------------------------------------
    // Snapshot
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void CreateSnapshot_FreezesStructure_EvenAfterFurtherEdits()
    {
        var network = new Network("Lab");
        var a = AddNode(network, "A");
        var b = AddNode(network, "B");
        Link(network, a, b);

        var snapshot = network.CreateSnapshot();

        // Mutate the live network after the snapshot.
        var c = AddNode(network, "C");
        Link(network, b, c);
        network.RemoveDevice(a);

        Assert.Equal(2, snapshot.Devices.Count);
        Assert.Single(snapshot.Connections);
        Assert.True(snapshot.ContainsDevice(a.Id));
        Assert.False(snapshot.ContainsDevice(c.Id));
        Assert.True(snapshot.AreDirectlyConnected(a, b));
        Assert.Equal(network.Version - 3, snapshot.Version);
    }
}
