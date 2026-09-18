using System;
using System.Linq;
using NetSim.Application.Common;
using NetSim.Application.State;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Topology;

namespace NetSim.Application.Services;

public sealed class DeviceService : IDeviceService
{
    private readonly IApplicationState _applicationState;

    public DeviceService(IApplicationState applicationState)
    {
        _applicationState = applicationState;
    }

    public event EventHandler? InterfacesChanged;

    // Every Add<Type>() below delegates to NetworkDeviceFactory rather than "new Router(...)"
    // etc. directly, so that devices created through this service always get the
    // NetworkDeviceFactory default interfaces (see NetworkDeviceFactory for the per-type
    // defaults). Devices constructed directly in tests (e.g. "new Router(name)") deliberately
    // keep coming out interface-less - that stays a plain, minimal constructor.
    public OperationResult<Router> AddRouter(string name) =>
        AddDevice(name, static n => (Router)NetworkDeviceFactory.Create(DeviceType.Router, n));

    public OperationResult<Switch> AddSwitch(string name) =>
        AddDevice(name, static n => (Switch)NetworkDeviceFactory.Create(DeviceType.Switch, n));

    public OperationResult<Pc> AddPc(string name) =>
        AddDevice(name, static n => (Pc)NetworkDeviceFactory.Create(DeviceType.Pc, n));

    public OperationResult<Server> AddServer(string name) =>
        AddDevice(name, static n => (Server)NetworkDeviceFactory.Create(DeviceType.Server, n));

    public OperationResult<Laptop> AddLaptop(string name) =>
        AddDevice(name, static n => (Laptop)NetworkDeviceFactory.Create(DeviceType.Laptop, n));

    public OperationResult<NetworkDevice> AddDevice(DeviceType deviceType, string? name = null)
    {
        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult<NetworkDevice>.Failure(OperationErrorType.InvalidState, "No active network. Create a network before adding devices.");
        }

        // Blank-name validation is still a domain rule (NetworkDevice constructor -> Guard);
        // an explicitly-blank name is passed through as-is rather than silently replaced by an
        // auto-generated one, so callers still get the expected DomainException. Only a
        // missing name (null) is auto-named - see docs/architecture/device-model.md ("Naming").
        var resolvedName = name ?? GenerateDefaultName(network, deviceType);

        var device = NetworkDeviceFactory.Create(deviceType, resolvedName);
        network.AddDevice(device);
        return OperationResult<NetworkDevice>.Success(device);
    }

    private static string GenerateDefaultName(Network network, DeviceType deviceType)
    {
        var prefix = DeviceTypeRegistry.Get(deviceType).DisplayName.Replace(" ", string.Empty);
        var existingCount = network.Devices.Count(d => d.DeviceType == deviceType);
        return $"{prefix}{existingCount}";
    }

    public OperationResult RemoveDevice(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!TryGetCurrentNetwork(out var network, out var failure))
        {
            return failure;
        }

        // Network.RemoveDevice() already handles unplugging any attached connections;
        // it returns false rather than throwing when the device isn't part of it, which
        // this service translates into an application-level NotFound outcome.
        return network.RemoveDevice(device)
            ? OperationResult.Success()
            : OperationResult.Failure(OperationErrorType.NotFound, $"Device '{device.Name}' was not found in the current network.");
    }

    public OperationResult RenameDevice(NetworkDevice device, string newName)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!TryGetCurrentNetwork(out var network, out var failure))
        {
            return failure;
        }

        if (!network.Devices.Contains(device))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, $"Device '{device.Name}' was not found in the current network.");
        }

        // Blank-name validation is a domain rule (NetworkDevice.Rename -> Guard) and is
        // left to throw DomainException rather than being duplicated here.
        device.Rename(newName);
        return OperationResult.Success();
    }

    public OperationResult<NetworkInterface> AddInterface(NetworkDevice device, string name, InterfaceType interfaceType)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult<NetworkInterface>.Failure(OperationErrorType.InvalidState, "No active network. Create a network before adding interfaces.");
        }

        if (!network.Devices.Contains(device))
        {
            return OperationResult<NetworkInterface>.Failure(OperationErrorType.NotFound, $"Device '{device.Name}' was not found in the current network.");
        }

        // Duplicate-interface-name validation is a domain rule (NetworkDevice.AddInterface)
        // and is left to throw DomainException rather than being duplicated here.
        var networkInterface = device.AddInterface(name, interfaceType);
        return OperationResult<NetworkInterface>.Success(networkInterface);
    }

    public OperationResult SetInterfaceAdministrativeState(NetworkInterface networkInterface, InterfaceAdministrativeState state)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);

        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No active network.");
        }

        if (!network.Devices.Contains(networkInterface.Device))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, $"Interface '{networkInterface.Name}' is not part of the current network.");
        }

        if (networkInterface.AdministrativeState == state)
        {
            return OperationResult.Success();
        }

        if (state == InterfaceAdministrativeState.Enabled)
        {
            networkInterface.Enable();
        }
        else
        {
            networkInterface.Disable();
        }

        _applicationState.MarkCurrentProjectDirty();
        InterfacesChanged?.Invoke(this, EventArgs.Empty);
        return OperationResult.Success();
    }

    public OperationResult SetInterfaceIPv4Configuration(NetworkInterface networkInterface, IPv4Address address, int prefixLength)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);

        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No active network.");
        }

        if (!network.Devices.Contains(networkInterface.Device))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, $"Interface '{networkInterface.Name}' is not part of the current network.");
        }

        Ipv4InterfaceConfiguration configuration;
        try
        {
            // The domain value object is the single source of address/prefix validation; a bad
            // value is surfaced as an expected ValidationError rather than an unhandled throw.
            configuration = Ipv4InterfaceConfiguration.Create(address, prefixLength);
        }
        catch (DomainException ex)
        {
            return OperationResult.Failure(OperationErrorType.ValidationError, ex.Message);
        }

        networkInterface.SetPrimaryIPv4Configuration(configuration);
        SyncConnectedRoutesIfRouter(networkInterface);
        _applicationState.MarkCurrentProjectDirty();
        InterfacesChanged?.Invoke(this, EventArgs.Empty);
        return OperationResult.Success();
    }

    public OperationResult SetInterfaceIPv4DefaultGateway(NetworkInterface networkInterface, IPv4Address gateway)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);

        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No active network.");
        }

        if (!network.Devices.Contains(networkInterface.Device))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, $"Interface '{networkInterface.Name}' is not part of the current network.");
        }

        try
        {
            networkInterface.SetIPv4DefaultGateway(gateway);
        }
        catch (DomainException ex)
        {
            return OperationResult.Failure(OperationErrorType.ValidationError, ex.Message);
        }

        _applicationState.MarkCurrentProjectDirty();
        InterfacesChanged?.Invoke(this, EventArgs.Empty);
        return OperationResult.Success();
    }

    public OperationResult ClearInterfaceIPv4DefaultGateway(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);

        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No active network.");
        }

        if (!network.Devices.Contains(networkInterface.Device))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, $"Interface '{networkInterface.Name}' is not part of the current network.");
        }

        if (networkInterface.IPv4DefaultGateway is null)
        {
            return OperationResult.Success();
        }

        networkInterface.SetIPv4DefaultGateway(null);
        _applicationState.MarkCurrentProjectDirty();
        InterfacesChanged?.Invoke(this, EventArgs.Empty);
        return OperationResult.Success();
    }

    // A router's connected routes are derived from its interface IPv4 configuration; keep them in
    // step whenever an address changes so the routing-table UI updates without a running simulation
    // (the routing engine also re-derives them before every forwarding decision).
    private static void SyncConnectedRoutesIfRouter(NetworkInterface networkInterface)
    {
        if (networkInterface.Device is Router router)
        {
            router.SyncConnectedRoutes();
        }
    }

    public OperationResult ClearInterfaceIPv4Configuration(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);

        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No active network.");
        }

        if (!network.Devices.Contains(networkInterface.Device))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, $"Interface '{networkInterface.Name}' is not part of the current network.");
        }

        if (!networkInterface.HasIPv4Configuration)
        {
            return OperationResult.Success();
        }

        networkInterface.ClearIPv4Configuration();
        SyncConnectedRoutesIfRouter(networkInterface);
        _applicationState.MarkCurrentProjectDirty();
        InterfacesChanged?.Invoke(this, EventArgs.Empty);
        return OperationResult.Success();
    }

    public OperationResult AddInterfaceIPv6Configuration(NetworkInterface networkInterface, IPv6Address address, int prefixLength)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);

        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No active network.");
        }

        if (!network.Devices.Contains(networkInterface.Device))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, $"Interface '{networkInterface.Name}' is not part of the current network.");
        }

        try
        {
            // The domain value object is the single source of address/prefix validation, and
            // NetworkInterface enforces "no duplicate address" - both surface as an expected
            // ValidationError rather than an unhandled throw.
            var configuration = Ipv6InterfaceConfiguration.Create(address, prefixLength);
            networkInterface.AddIPv6Configuration(configuration);
        }
        catch (DomainException ex)
        {
            return OperationResult.Failure(OperationErrorType.ValidationError, ex.Message);
        }

        _applicationState.MarkCurrentProjectDirty();
        InterfacesChanged?.Invoke(this, EventArgs.Empty);
        return OperationResult.Success();
    }

    public OperationResult ClearInterfaceIPv6Configuration(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);

        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No active network.");
        }

        if (!network.Devices.Contains(networkInterface.Device))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, $"Interface '{networkInterface.Name}' is not part of the current network.");
        }

        if (!networkInterface.HasIPv6Configuration)
        {
            return OperationResult.Success();
        }

        networkInterface.ClearIPv6Configuration();
        _applicationState.MarkCurrentProjectDirty();
        InterfacesChanged?.Invoke(this, EventArgs.Empty);
        return OperationResult.Success();
    }

    public OperationResult ClearInterfaceArpCache(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);

        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult.Failure(OperationErrorType.InvalidState, "No active network.");
        }

        if (!network.Devices.Contains(networkInterface.Device))
        {
            return OperationResult.Failure(OperationErrorType.NotFound, $"Interface '{networkInterface.Name}' is not part of the current network.");
        }

        // The ARP cache is transient runtime state - clearing it is not a persisted edit, so the
        // project is intentionally NOT marked dirty. InterfacesChanged still fires so any open
        // diagnostics view rebuilds from the (now empty) cache.
        networkInterface.ArpCache.Clear();
        InterfacesChanged?.Invoke(this, EventArgs.Empty);
        return OperationResult.Success();
    }

    private OperationResult<TDevice> AddDevice<TDevice>(string name, Func<string, TDevice> factory)
        where TDevice : NetworkDevice
    {
        if (_applicationState.CurrentNetwork is not { } network)
        {
            return OperationResult<TDevice>.Failure(OperationErrorType.InvalidState, "No active network. Create a network before adding devices.");
        }

        // Blank-name validation is a domain rule (NetworkDevice constructor -> Guard) and
        // is left to throw DomainException rather than being duplicated here. Duplicate
        // device names are intentionally allowed - Core has no such rule and this service
        // does not invent one.
        var device = factory(name);
        network.AddDevice(device);
        return OperationResult<TDevice>.Success(device);
    }

    private bool TryGetCurrentNetwork(out Network network, out OperationResult failure)
    {
        if (_applicationState.CurrentNetwork is { } current)
        {
            network = current;
            failure = null!;
            return true;
        }

        network = null!;
        failure = OperationResult.Failure(OperationErrorType.InvalidState, "No active network.");
        return false;
    }
}
