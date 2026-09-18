using NetSim.Core.Common;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Topology;

namespace NetSim.Core.Packets;

/// <summary>
/// The packet engine: the single place packets are created, tracked while in flight, driven
/// through their <see cref="PacketState"/> lifecycle and handed to <see cref="IPacketProcessor"/>s.
///
/// It is deliberately <em>protocol-agnostic</em> - Ethernet / IP / ARP / ... behaviour arrives
/// as processors and payload layers in later phases, never as changes here. It also holds no
/// topology and runs no clock of its own: a future real-time / step simulation phase owns the
/// scheduling and drives this engine. This is the packet-layer counterpart to what
/// <see cref="Topology.Network"/> is for structure.
/// </summary>
public interface IPacketEngine
{
    /// <summary>Every packet created this session, in creation order (including completed ones), until <see cref="Reset"/>.</summary>
    IReadOnlyCollection<Packet> Packets { get; }

    /// <summary>The subset of <see cref="Packets"/> that has not yet reached a terminal state.</summary>
    IReadOnlyCollection<Packet> ActivePackets { get; }

    /// <summary>The subset of <see cref="Packets"/> in a terminal state (delivered or dropped).</summary>
    IReadOnlyCollection<Packet> TerminalPackets { get; }

    /// <summary>Raised after a new packet has been created and registered.</summary>
    event EventHandler<PacketEventArgs>? PacketCreated;

    /// <summary>Raised after a packet's <see cref="PacketState"/> has changed.</summary>
    event EventHandler<PacketEventArgs>? PacketStateChanged;

    /// <summary>Raised after a packet has crossed a connection - <see cref="PacketEventArgs.Hop"/> describes it.</summary>
    event EventHandler<PacketEventArgs>? PacketMoved;

    /// <summary>Raised after a packet has been run through an <see cref="IPacketProcessor"/> (whatever the outcome).</summary>
    event EventHandler<PacketEventArgs>? PacketProcessed;

    /// <summary>
    /// Creates a packet, validates it structurally and registers it. An invalid packet is
    /// rejected with a <see cref="Common.Exceptions.DomainException"/> rather than silently
    /// entering the simulation.
    /// </summary>
    Packet CreatePacket(NetworkDevice source, NetworkDevice destination, string protocol, IPacketPayload? payload = null);

    /// <summary>
    /// Runs <paramref name="packet"/> through <paramref name="processor"/>, applies the resulting
    /// <see cref="PacketState"/> change, raises <see cref="PacketStateChanged"/> (if the state
    /// moved) and <see cref="PacketProcessed"/>, and returns the processor's result.
    /// </summary>
    PacketProcessingResult Process(Packet packet, IPacketProcessor processor);

    /// <summary>Advances the packet to <see cref="PacketState.Transmitted"/> and raises <see cref="PacketStateChanged"/>.</summary>
    void MarkTransmitted(Packet packet, string? note = null);

    /// <summary>Advances the packet to <see cref="PacketState.InTransit"/> and raises <see cref="PacketStateChanged"/>.</summary>
    void MarkInTransit(Packet packet, string? note = null);

    /// <summary>Advances the packet to <see cref="PacketState.Delivered"/> and raises <see cref="PacketStateChanged"/>.</summary>
    void MarkDelivered(Packet packet, string? note = null);

    /// <summary>Drops the packet with the given structured reason and raises <see cref="PacketStateChanged"/>.</summary>
    void Drop(Packet packet, PacketDropReason reason, string? note = null);

    /// <summary>Clears all tracked packets - e.g. when a new simulation run starts or the project closes.</summary>
    void Reset();

    // ----- Registry (efficient id lookup; the internal collection is never exposed mutable) -----

    /// <summary>True when a packet with this id is tracked.</summary>
    bool Contains(EntityId packetId);

    /// <summary>The tracked packet with this id, or null.</summary>
    Packet? Get(EntityId packetId);

    /// <summary>Non-throwing lookup by id.</summary>
    bool TryGet(EntityId packetId, out Packet packet);

    /// <summary>Removes the tracked packet with this id. Returns false when it was not tracked.</summary>
    bool Remove(EntityId packetId);

    // ----- Movement (topology-validated; never routing) -----

    /// <summary>
    /// Moves the packet one hop out of <paramref name="egressInterface"/> to the interface it is
    /// connected to, updating <see cref="Packet.Location"/> and <see cref="Packet.Hops"/> and
    /// raising <see cref="PacketMoved"/>. The move is validated against <paramref name="topology"/>
    /// (the interface must be on the packet's current device and carry a connection); a structural
    /// mismatch is reported via the returned <see cref="PacketMovementResult"/>, not thrown. An
    /// unknown packet id, or a packet already in a terminal state, throws
    /// <see cref="Common.Exceptions.DomainException"/>. This does not decide <em>which</em>
    /// interface to use - that is the caller's (a future routing/simulation engine's) job.
    /// </summary>
    PacketMovementResult MovePacket(ITopologyView topology, EntityId packetId, NetworkInterface egressInterface, long simulationStep = 0);

    /// <summary>
    /// Convenience overload: moves the packet toward <paramref name="nextHopDevice"/> over any one
    /// direct connection between it and the packet's current device. Rejected when there is no such
    /// connection.
    /// </summary>
    PacketMovementResult MovePacket(ITopologyView topology, EntityId packetId, NetworkDevice nextHopDevice, long simulationStep = 0);

    /// <summary>Positions an active packet at a specific interface without recording a hop (e.g. seeding a start point).</summary>
    void PlacePacket(EntityId packetId, NetworkInterface atInterface);
}
