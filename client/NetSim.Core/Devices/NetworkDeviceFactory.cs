using NetSim.Core.Common;

namespace NetSim.Core.Devices;

/// <summary>
/// The single, centralized place that knows how to construct every concrete
/// <see cref="NetworkDevice"/> type and give it sensible default interfaces. Callers (the
/// Application layer, and later the Device Library UI) ask for a device by
/// <see cref="DeviceType"/> instead of hard-coding "new Router(...)" / "new Switch(...)"
/// / ... themselves - adding a new device type only means adding one case here (plus the
/// concrete class and the <see cref="DeviceType"/> value), not touching every call site
/// that creates devices.
/// </summary>
public static class NetworkDeviceFactory
{
    /// <summary>
    /// Creates a device of the given <paramref name="deviceType"/> with the name-appropriate
    /// default interfaces already attached (see docs/architecture/device-model.md for the
    /// exact defaults per type).
    /// </summary>
    public static NetworkDevice Create(DeviceType deviceType, string name)
    {
        var device = CreateWithoutDefaults(deviceType, name);
        ApplyDefaultInterfaces(device);
        return device;
    }

    /// <summary>
    /// Creates a device of the given <paramref name="deviceType"/> with no interfaces
    /// attached. This exists for persistence rehydration (see
    /// <c>NetworkSimulator.Infrastructure.Persistence.Mapping.NetworkDeviceMapper</c>), where
    /// interfaces are restored one-by-one from saved data rather than generated fresh -
    /// applying the "new device" defaults on top of a rehydrated device would duplicate or
    /// contradict what was actually saved. Ordinary device-creation call sites should use
    /// <see cref="Create"/> instead.
    /// </summary>
    public static NetworkDevice CreateWithoutDefaults(DeviceType deviceType, string name) => deviceType switch
    {
        DeviceType.Router => new Router(name),
        DeviceType.Switch => new Switch(name),
        DeviceType.Pc => new Pc(name),
        DeviceType.Server => new Server(name),
        DeviceType.Laptop => new Laptop(name),
        _ => throw new ArgumentOutOfRangeException(nameof(deviceType), deviceType, "Unsupported device type."),
    };

    /// <summary>
    /// Reconstructs a device that already has an id from a previous save (persistence
    /// rehydration), with no interfaces attached - the caller (see
    /// <c>NetworkSimulator.Infrastructure.Persistence.Mapping.NetworkDeviceMapper</c>) is
    /// expected to restore the saved interfaces itself via <see cref="NetworkDevice.AddInterface"/>.
    /// Not for ordinary device creation - that always goes through <see cref="Create"/>, which
    /// mints a brand-new <see cref="EntityId"/>.
    /// </summary>
    public static NetworkDevice RehydrateWithoutDefaults(EntityId id, DeviceType deviceType, string name) => deviceType switch
    {
        DeviceType.Router => new Router(id, name),
        DeviceType.Switch => new Switch(id, name),
        DeviceType.Pc => new Pc(id, name),
        DeviceType.Server => new Server(id, name),
        DeviceType.Laptop => new Laptop(id, name),
        _ => throw new ArgumentOutOfRangeException(nameof(deviceType), deviceType, "Unsupported device type."),
    };

    private static void ApplyDefaultInterfaces(NetworkDevice device)
    {
        // The per-type interface set is declared in DeviceInterfaceCatalog; this method only
        // materialises it. Adding a device type therefore never touches this method.
        foreach (var definition in DeviceInterfaceCatalog.For(device.DeviceType))
        {
            device.AddInterface(definition.Name, definition.InterfaceType);
        }
    }
}
