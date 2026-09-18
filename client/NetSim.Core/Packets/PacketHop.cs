using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Packets;

/// <summary>
/// One step of a <see cref="Packet"/>'s traversal history: it crossed one
/// <see cref="Connection"/> from one interface to another. A plain, immutable data carrier - the
/// domain foundation a future Event Timeline / Packet Inspector / monitoring layer renders,
/// without any of those systems existing yet and without any UI concern here.
///
/// <see cref="SimulationStep"/> is an abstract, monotonically increasing step counter supplied by
/// the caller (0 when the caller does not track steps yet). It is deliberately not a wall-clock
/// <c>DateTime</c> - simulation time is a later phase and must not be derived from the real clock.
/// </summary>
public sealed record PacketHop(
    int Sequence,
    NetworkDevice FromDevice,
    NetworkInterface? FromInterface,
    NetworkDevice ToDevice,
    NetworkInterface? ToInterface,
    Connection Connection,
    long SimulationStep)
{
    public override string ToString()
    {
        var from = FromInterface is null ? FromDevice.Name : $"{FromDevice.Name}/{FromInterface.Name}";
        var to = ToInterface is null ? ToDevice.Name : $"{ToDevice.Name}/{ToInterface.Name}";
        return $"#{Sequence} {from} -> {to}";
    }
}
