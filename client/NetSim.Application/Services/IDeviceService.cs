using System;
using NetSim.Application.Common;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Application.Services;

/// <summary>
/// Coordinates device-related operations against the application's current network.
/// All operations require a current network to be active (see
/// <see cref="Services.INetworkService"/>); when none is active they fail with
/// <see cref="OperationErrorType.InvalidState"/> rather than throwing.
/// </summary>
public interface IDeviceService
{
    OperationResult<Router> AddRouter(string name);

    OperationResult<Switch> AddSwitch(string name);

    OperationResult<Pc> AddPc(string name);

    OperationResult<Server> AddServer(string name);

    OperationResult<Laptop> AddLaptop(string name);

    /// <summary>
    /// Creates a device of the given <paramref name="deviceType"/> via
    /// <see cref="NetworkDeviceFactory"/>, generating a default name (e.g. "Router0") when
    /// <paramref name="name"/> is null or blank. This is the entry point a future Device
    /// Library UI can use without a switch statement over every concrete Add*() method.
    /// </summary>
    OperationResult<NetworkDevice> AddDevice(DeviceType deviceType, string? name = null);

    OperationResult RemoveDevice(NetworkDevice device);

    OperationResult RenameDevice(NetworkDevice device, string newName);

    OperationResult<NetworkInterface> AddInterface(NetworkDevice device, string name, InterfaceType interfaceType);

    /// <summary>
    /// Sets an interface's <see cref="InterfaceAdministrativeState"/> (Enable/Disable). Marks the
    /// current project dirty and raises <see cref="InterfacesChanged"/> so open UI (canvas anchors,
    /// the Device Properties panel) can refresh. Enforced domain rule: disabling forces the
    /// interface operationally down (see <see cref="NetworkInterface.Disable"/>).
    /// </summary>
    OperationResult SetInterfaceAdministrativeState(NetworkInterface networkInterface, InterfaceAdministrativeState state);

    /// <summary>
    /// Sets the interface's primary IPv4 address (replacing any existing IPv4 configuration) from an
    /// <paramref name="address"/> and a <paramref name="prefixLength"/>. Returns a
    /// <see cref="OperationErrorType.ValidationError"/> failure for an address/prefix the domain
    /// rejects (e.g. 0.0.0.0, a multicast address, a prefix outside 0-32) rather than throwing.
    /// Marks the current project dirty and raises <see cref="InterfacesChanged"/> on success.
    /// </summary>
    OperationResult SetInterfaceIPv4Configuration(NetworkInterface networkInterface, IPv4Address address, int prefixLength);

    /// <summary>
    /// Removes every IPv4 address from the interface. Marks the current project dirty and raises
    /// <see cref="InterfacesChanged"/>. A no-op (still a success) when the interface has no IPv4 configuration.
    /// </summary>
    OperationResult ClearInterfaceIPv4Configuration(NetworkInterface networkInterface);

    /// <summary>
    /// Sets the interface's IPv4 default gateway (Phase 27) - the next-hop router a host sends
    /// off-link traffic to. Returns a <see cref="OperationErrorType.ValidationError"/> failure for
    /// an address the domain rejects (0.0.0.0, broadcast, multicast) rather than throwing. Marks the
    /// current project dirty and raises <see cref="InterfacesChanged"/> on success.
    /// </summary>
    OperationResult SetInterfaceIPv4DefaultGateway(NetworkInterface networkInterface, IPv4Address gateway);

    /// <summary>
    /// Removes the interface's IPv4 default gateway. Marks the current project dirty and raises
    /// <see cref="InterfacesChanged"/>. A no-op (still a success) when no gateway is configured.
    /// </summary>
    OperationResult ClearInterfaceIPv4DefaultGateway(NetworkInterface networkInterface);

    /// <summary>
    /// Adds an IPv6 address (with its <paramref name="prefixLength"/>) to the interface, keeping any
    /// addresses already configured - IPv6 interfaces routinely carry several (a link-local plus one
    /// or more global / unique-local addresses). The first address added becomes the primary.
    /// Returns a <see cref="OperationErrorType.ValidationError"/> failure for an address/prefix the
    /// domain rejects (e.g. <c>::</c>, <c>::1</c>, a multicast address, a prefix outside 0-128) or a
    /// duplicate, rather than throwing. Marks the current project dirty and raises
    /// <see cref="InterfacesChanged"/> on success.
    /// </summary>
    OperationResult AddInterfaceIPv6Configuration(NetworkInterface networkInterface, IPv6Address address, int prefixLength);

    /// <summary>
    /// Removes every IPv6 address from the interface. Marks the current project dirty and raises
    /// <see cref="InterfacesChanged"/>. A no-op (still a success) when the interface has no IPv6 configuration.
    /// </summary>
    OperationResult ClearInterfaceIPv6Configuration(NetworkInterface networkInterface);

    /// <summary>
    /// Clears an interface's ARP cache (Phase 20) - the runtime IPv4 -&gt; MAC mappings it has
    /// learned. Raises <see cref="InterfacesChanged"/> so open diagnostics refresh. Does
    /// <em>not</em> mark the project dirty: the ARP cache is transient runtime state and is never
    /// persisted. A no-op (still a success) when the cache is already empty.
    /// </summary>
    OperationResult ClearInterfaceArpCache(NetworkInterface networkInterface);

    /// <summary>
    /// Raised after an interface's administrative/operational state changes through this service
    /// (outside of a connect/disconnect, which <see cref="IConnectionService.ConnectionsChanged"/>
    /// already covers), so views reflecting interface state can refresh without a selection change.
    /// </summary>
    event EventHandler? InterfacesChanged;
}
