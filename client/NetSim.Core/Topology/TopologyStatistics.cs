namespace NetSim.Core.Topology;

/// <summary>
/// A cheap structural summary of a topology, captured at one instant. Deliberately just counts:
/// the full Network Monitoring surface is a later phase - this is the minimum a future monitor,
/// status bar or AI analysis needs to describe "how big / how connected is this network".
/// </summary>
/// <param name="DeviceCount">Number of devices registered in the topology.</param>
/// <param name="InterfaceCount">Total interfaces across every registered device.</param>
/// <param name="ConnectionCount">Number of connections (edges) in the topology.</param>
/// <param name="ConnectedDeviceCount">Devices that have at least one connection.</param>
/// <param name="IsolatedDeviceCount">Devices with no connections at all.</param>
/// <param name="ConnectedComponentCount">
/// Number of connected components, counting each isolated device as its own component. Zero only
/// when the topology has no devices.
/// </param>
public readonly record struct TopologyStatistics(
    int DeviceCount,
    int InterfaceCount,
    int ConnectionCount,
    int ConnectedDeviceCount,
    int IsolatedDeviceCount,
    int ConnectedComponentCount);
