using NetSim.Core.Networking;

namespace NetSim.Core.Devices;

/// <summary>
/// The single source of truth for which interfaces each <see cref="DeviceType"/> starts with.
/// <see cref="NetworkDeviceFactory"/> asks this for a list of <see cref="InterfaceDefinition"/>s
/// and materialises each one through <see cref="NetworkDevice.AddInterface"/> - so interface
/// creation is defined declaratively in one place rather than duplicated as imperative
/// AddInterface calls across the codebase. Realistic, extensible names are used (slot/port style
/// where appropriate) without assuming a fixed number of numeric components.
/// </summary>
public static class DeviceInterfaceCatalog
{
    // Deliberately small: a sensible starting point for a lab topology, not a simulation of a
    // specific real switch model/port count.
    private const int DefaultSwitchPortCount = 8;

    public static IReadOnlyList<InterfaceDefinition> For(DeviceType deviceType) => deviceType switch
    {
        DeviceType.Router =>
        [
            new InterfaceDefinition("GigabitEthernet0/0", InterfaceType.GigabitEthernet),
            new InterfaceDefinition("GigabitEthernet0/1", InterfaceType.GigabitEthernet),
            new InterfaceDefinition("Serial0/0", InterfaceType.Serial),
        ],

        DeviceType.Switch => BuildSwitchPorts(),

        DeviceType.Pc or DeviceType.Laptop or DeviceType.Server =>
        [
            new InterfaceDefinition("Ethernet0", InterfaceType.Ethernet),
        ],

        _ => throw new ArgumentOutOfRangeException(nameof(deviceType), deviceType, "Unsupported device type."),
    };

    private static IReadOnlyList<InterfaceDefinition> BuildSwitchPorts()
    {
        var ports = new List<InterfaceDefinition>(DefaultSwitchPortCount);
        for (var portNumber = 1; portNumber <= DefaultSwitchPortCount; portNumber++)
        {
            ports.Add(new InterfaceDefinition($"FastEthernet0/{portNumber}", InterfaceType.FastEthernet));
        }

        return ports;
    }
}
