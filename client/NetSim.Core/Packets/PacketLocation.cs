using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Packets;

/// <summary>Where a <see cref="Packet"/> currently sits in the simulated network.</summary>
public enum PacketLocationKind
{
    /// <summary>No position assigned (a packet that has not entered the topology).</summary>
    None,

    /// <summary>At a device, not tied to a specific interface.</summary>
    AtDevice,

    /// <summary>At a specific interface of a device (e.g. the port a frame just arrived on).</summary>
    AtInterface,

    /// <summary>Terminal: the packet reached its destination.</summary>
    Delivered,

    /// <summary>Terminal: the packet was dropped.</summary>
    Dropped,
}

/// <summary>
/// A clean, immutable description of a <see cref="Packet"/>'s current position. It holds live
/// domain references (never ids to re-resolve, never canvas coordinates) but is a value object -
/// two locations are equal when they point at the same kind/device/interface. Movement produces a
/// new <see cref="PacketLocation"/>; the packet never mutates one in place.
///
/// Continuous "in transit between two interfaces" position is intentionally not modelled here -
/// a hop is atomic in this foundation (you are <see cref="AtInterface"/> the egress port, then
/// <see cref="AtInterface"/> the ingress port). Interpolating a position along a cable for an
/// animation is a Packet Visualization concern (Phase 35), not a domain one.
/// </summary>
public sealed record PacketLocation
{
    private PacketLocation(PacketLocationKind kind, NetworkDevice? device, NetworkInterface? networkInterface)
    {
        Kind = kind;
        Device = device;
        Interface = networkInterface;
    }

    public PacketLocationKind Kind { get; }

    /// <summary>The device the packet is at, or null for <see cref="PacketLocationKind.None"/> / terminal kinds.</summary>
    public NetworkDevice? Device { get; }

    /// <summary>The interface the packet is at, or null unless <see cref="Kind"/> is <see cref="PacketLocationKind.AtInterface"/>.</summary>
    public NetworkInterface? Interface { get; }

    /// <summary>The unpositioned location.</summary>
    public static PacketLocation None { get; } = new(PacketLocationKind.None, null, null);

    /// <summary>The terminal "delivered" location.</summary>
    public static PacketLocation Delivered { get; } = new(PacketLocationKind.Delivered, null, null);

    /// <summary>The terminal "dropped" location.</summary>
    public static PacketLocation Dropped { get; } = new(PacketLocationKind.Dropped, null, null);

    public static PacketLocation AtDevice(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return new PacketLocation(PacketLocationKind.AtDevice, device, null);
    }

    public static PacketLocation AtInterface(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);
        return new PacketLocation(PacketLocationKind.AtInterface, networkInterface.Device, networkInterface);
    }

    /// <summary>True when the packet has a device position (<see cref="PacketLocationKind.AtDevice"/> or <see cref="PacketLocationKind.AtInterface"/>).</summary>
    public bool IsPositioned => Kind is PacketLocationKind.AtDevice or PacketLocationKind.AtInterface;

    public override string ToString() => Kind switch
    {
        PacketLocationKind.AtInterface => $"{Device!.Name}/{Interface!.Name}",
        PacketLocationKind.AtDevice => Device!.Name,
        _ => Kind.ToString(),
    };
}
