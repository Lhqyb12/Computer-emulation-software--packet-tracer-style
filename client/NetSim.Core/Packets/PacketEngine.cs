using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Topology;

namespace NetSim.Core.Packets;

/// <summary>
/// Default <see cref="IPacketEngine"/>. Holds the packets for the current session (an ordered
/// list plus an id index for O(1) lookup - the list is the single source of truth, the dictionary
/// is a pure index over the same references), enforces "a packet is validated before it enters
/// the simulation", centralises the state-change + event-raising every <see cref="IPacketProcessor"/>
/// and transmission path relies on, and validates every movement request against a supplied
/// <see cref="ITopologyView"/>. It runs no clock and makes no routing/forwarding decision.
/// </summary>
public sealed class PacketEngine : IPacketEngine
{
    private readonly List<Packet> _packets = [];
    private readonly Dictionary<EntityId, Packet> _packetsById = [];

    public IReadOnlyCollection<Packet> Packets => _packets.AsReadOnly();

    public IReadOnlyCollection<Packet> ActivePackets =>
        _packets.Where(static p => !p.HasCompleted).ToList().AsReadOnly();

    public IReadOnlyCollection<Packet> TerminalPackets =>
        _packets.Where(static p => p.HasCompleted).ToList().AsReadOnly();

    public event EventHandler<PacketEventArgs>? PacketCreated;

    public event EventHandler<PacketEventArgs>? PacketStateChanged;

    public event EventHandler<PacketEventArgs>? PacketMoved;

    public event EventHandler<PacketEventArgs>? PacketProcessed;

    public Packet CreatePacket(NetworkDevice source, NetworkDevice destination, string protocol, IPacketPayload? payload = null)
    {
        var packet = new Packet(source, destination, protocol, payload);

        var validation = packet.Validate();
        if (!validation.IsValid)
        {
            throw new DomainException($"Cannot create packet: {string.Join("; ", validation.Errors)}");
        }

        _packets.Add(packet);
        _packetsById.Add(packet.Id, packet);
        PacketCreated?.Invoke(this, new PacketEventArgs(packet));
        return packet;
    }

    public PacketProcessingResult Process(Packet packet, IPacketProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(processor);

        var stateBefore = packet.State;
        var result = processor.Process(packet);
        ApplyOutcome(packet, result);

        if (packet.State != stateBefore)
        {
            PacketStateChanged?.Invoke(this, new PacketEventArgs(packet, packet.StateHistory[^1]));
        }

        PacketProcessed?.Invoke(this, new PacketEventArgs(
            packet,
            packet.StateHistory.Count > 0 ? packet.StateHistory[^1] : null));

        return result;
    }

    public void MarkTransmitted(Packet packet, string? note = null) => ApplyTransition(packet, p => p.MarkTransmitted(note));

    public void MarkInTransit(Packet packet, string? note = null) => ApplyTransition(packet, p => p.MarkInTransit(note));

    public void MarkDelivered(Packet packet, string? note = null) => ApplyTransition(packet, p => p.MarkDelivered(note));

    public void Drop(Packet packet, PacketDropReason reason, string? note = null)
    {
        ArgumentNullException.ThrowIfNull(reason);
        ApplyTransition(packet, p => p.Drop(reason, note));
    }

    public void Reset()
    {
        _packets.Clear();
        _packetsById.Clear();
    }

    // ----- Registry -----

    public bool Contains(EntityId packetId) => _packetsById.ContainsKey(packetId);

    public Packet? Get(EntityId packetId) => _packetsById.GetValueOrDefault(packetId);

    public bool TryGet(EntityId packetId, out Packet packet)
    {
        var found = _packetsById.TryGetValue(packetId, out var match);
        packet = match!;
        return found;
    }

    public bool Remove(EntityId packetId)
    {
        if (!_packetsById.Remove(packetId, out var packet))
        {
            return false;
        }

        _packets.Remove(packet);
        return true;
    }

    // ----- Movement -----

    public PacketMovementResult MovePacket(ITopologyView topology, EntityId packetId, NetworkInterface egressInterface, long simulationStep = 0)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(egressInterface);

        var packet = RequirePacket(packetId);
        RequireActive(packet);

        var currentDevice = packet.Location.Device
            ?? throw new DomainException($"Packet '{packetId}' has no current device to move from.");

        if (!topology.ContainsDevice(currentDevice.Id))
        {
            return PacketMovementResult.Rejected(
                PacketMovementStatus.DeviceNotInTopology,
                $"The packet's current device '{currentDevice.Name}' is not part of the topology.");
        }

        if (!ReferenceEquals(egressInterface.Device, currentDevice))
        {
            return PacketMovementResult.Rejected(
                PacketMovementStatus.InterfaceNotOnDevice,
                $"Interface '{egressInterface.Name}' is not on the packet's current device '{currentDevice.Name}'.");
        }

        if (topology.GetInterface(egressInterface.Id) is null)
        {
            return PacketMovementResult.Rejected(
                PacketMovementStatus.InterfaceNotInTopology,
                $"Interface '{egressInterface.Name}' is not part of the topology.");
        }

        var connection = topology.GetConnection(egressInterface);
        if (connection is null)
        {
            return PacketMovementResult.Rejected(
                PacketMovementStatus.NoConnection,
                $"Interface '{egressInterface.Name}' on '{currentDevice.Name}' has no connection to move across.");
        }

        NetworkInterface farInterface;
        try
        {
            farInterface = connection.GetOtherEndpoint(egressInterface);
        }
        catch (DomainException)
        {
            return PacketMovementResult.Rejected(
                PacketMovementStatus.EndpointMismatch,
                $"The connection on '{egressInterface.Name}' does not list it as an endpoint.");
        }

        if (topology.GetInterface(farInterface.Id) is null || !topology.ContainsDevice(farInterface.Device.Id))
        {
            return PacketMovementResult.Rejected(
                PacketMovementStatus.DeviceRemoved,
                $"The device on the far side of '{egressInterface.Name}' is no longer in the topology.");
        }

        var hop = new PacketHop(
            Sequence: packet.HopCount + 1,
            FromDevice: currentDevice,
            FromInterface: egressInterface,
            ToDevice: farInterface.Device,
            ToInterface: farInterface,
            Connection: connection,
            SimulationStep: simulationStep);

        packet.RecordHop(hop);
        PacketMoved?.Invoke(this, new PacketEventArgs(packet, hop: hop));
        return PacketMovementResult.Moved(hop, packet.Location);
    }

    public PacketMovementResult MovePacket(ITopologyView topology, EntityId packetId, NetworkDevice nextHopDevice, long simulationStep = 0)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(nextHopDevice);

        var packet = RequirePacket(packetId);
        RequireActive(packet);

        var currentDevice = packet.Location.Device
            ?? throw new DomainException($"Packet '{packetId}' has no current device to move from.");

        var connection = topology.GetConnectionsBetween(currentDevice, nextHopDevice).FirstOrDefault();
        if (connection is null)
        {
            return PacketMovementResult.Rejected(
                PacketMovementStatus.NoConnection,
                $"There is no direct connection between '{currentDevice.Name}' and '{nextHopDevice.Name}'.");
        }

        var egress = connection.EndpointA.Device.Id == currentDevice.Id
            ? connection.EndpointA
            : connection.EndpointB;

        return MovePacket(topology, packetId, egress, simulationStep);
    }

    public void PlacePacket(EntityId packetId, NetworkInterface atInterface)
    {
        ArgumentNullException.ThrowIfNull(atInterface);
        var packet = RequirePacket(packetId);
        RequireActive(packet);
        packet.PlaceAt(PacketLocation.AtInterface(atInterface));
    }

    // Maps a processor's decision onto the packet's lifecycle. Guards each step with
    // HasCompleted so a processor that reports a terminal outcome for an already-finished packet
    // is a no-op rather than an illegal-transition throw.
    private static void ApplyOutcome(Packet packet, PacketProcessingResult result)
    {
        switch (result.Outcome)
        {
            case PacketProcessingOutcome.Transmitted:
                if (packet.State == PacketState.Created)
                {
                    packet.MarkTransmitted(result.Detail);
                }

                break;

            case PacketProcessingOutcome.Delivered:
                if (packet.State == PacketState.Created)
                {
                    packet.MarkTransmitted(result.Detail);
                }

                if (!packet.HasCompleted)
                {
                    packet.MarkDelivered(result.Detail);
                }

                break;

            case PacketProcessingOutcome.Dropped:
                if (!packet.HasCompleted)
                {
                    packet.Drop(result.DropReason ?? PacketDropReason.InvalidPacket, result.Detail);
                }

                break;

            case PacketProcessingOutcome.Unsupported:
                if (!packet.HasCompleted)
                {
                    packet.Drop(PacketDropReason.UnsupportedProtocol, result.Detail);
                }

                break;

            case PacketProcessingOutcome.Invalid:
                if (!packet.HasCompleted)
                {
                    packet.Drop(PacketDropReason.InvalidPacket, result.Detail);
                }

                break;
        }
    }

    private void ApplyTransition(Packet packet, Action<Packet> transition)
    {
        ArgumentNullException.ThrowIfNull(packet);

        transition(packet);
        PacketStateChanged?.Invoke(this, new PacketEventArgs(packet, packet.StateHistory[^1]));
    }

    private Packet RequirePacket(EntityId packetId) =>
        _packetsById.TryGetValue(packetId, out var packet)
            ? packet
            : throw new DomainException($"No packet with id '{packetId}' is tracked by the packet engine.");

    private static void RequireActive(Packet packet)
    {
        if (packet.HasCompleted)
        {
            throw new DomainException(
                $"Packet '{packet.Id}' has already been {(packet.IsDelivered ? "delivered" : "dropped")} and cannot be moved.");
        }
    }
}
