using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Packets;

/// <summary>
/// A protocol-agnostic unit of data travelling through the simulation. Phase 16 gives it three
/// things beyond identity:
/// <list type="bullet">
///   <item>a nestable <see cref="Payload"/> (<see cref="IPacketPayload"/>) so higher protocol
///   layers - Ethernet, IPv4, ... - encapsulate their own data without a second packet system;</item>
///   <item>an explicit <see cref="State"/> lifecycle (Created -&gt; Transmitted -&gt; InTransit
///   -&gt; Delivered, or -&gt; Dropped from any non-terminal state) with the transition rules
///   enforced here;</item>
///   <item>a <see cref="StateHistory"/> a future event timeline / inspector can render;</item>
///   <item>a current <see cref="Location"/> and a <see cref="Hops"/> traversal history, advanced
///   one connection at a time by the <see cref="IPacketEngine"/> (which validates each move
///   against the topology) - never by routing logic, which is a later phase.</item>
/// </list>
/// Protocol headers (source/destination MAC, EtherType, IP addresses, ports) are never modelled
/// on the packet itself - they live in the payload layers a later phase adds.
/// <see cref="Source"/> / <see cref="Destination"/> stay as the conceptual end devices, exactly
/// as in the Phase 2 placeholder.
/// </summary>
public sealed class Packet
{
    private readonly List<PacketStateTransition> _stateHistory = [];
    private readonly List<PacketHop> _hops = [];

    public Packet(NetworkDevice source, NetworkDevice destination, string protocol, IPacketPayload? payload = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        Id = EntityId.New();
        Source = source;
        Destination = destination;
        Protocol = Guard.AgainstNullOrWhiteSpace(protocol, nameof(protocol));
        Payload = payload;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        State = PacketState.Created;

        // A freshly created packet conceptually sits at its source device until something moves it.
        Location = PacketLocation.AtDevice(source);
    }

    public EntityId Id { get; }

    public NetworkDevice Source { get; }

    public NetworkDevice Destination { get; }

    public string Protocol { get; }

    /// <summary>The encapsulated data, or null for a header-only / payload-less packet.</summary>
    public IPacketPayload? Payload { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public PacketState State { get; private set; }

    /// <summary>The reason this packet was dropped, or null unless <see cref="State"/> is <see cref="PacketState.Dropped"/>.</summary>
    public PacketDropReason? DropReason { get; private set; }

    public IReadOnlyList<PacketStateTransition> StateHistory => _stateHistory.AsReadOnly();

    /// <summary>
    /// Where the packet currently sits in the simulated network. Starts at
    /// <see cref="PacketLocation.AtDevice"/> its <see cref="Source"/>, advances by one interface
    /// per <see cref="RecordHop"/>, and becomes <see cref="PacketLocation.Delivered"/> /
    /// <see cref="PacketLocation.Dropped"/> when the packet reaches a terminal state.
    /// </summary>
    public PacketLocation Location { get; private set; }

    /// <summary>The ordered list of connections this packet has crossed - its traversal history.</summary>
    public IReadOnlyList<PacketHop> Hops => _hops.AsReadOnly();

    /// <summary>How many hops the packet has made so far.</summary>
    public int HopCount => _hops.Count;

    /// <summary>The device the packet is currently at, or null once it is delivered/dropped/unpositioned.</summary>
    public NetworkDevice? CurrentDevice => Location.Device;

    /// <summary>The interface the packet is currently at, or null when it is not at a specific interface.</summary>
    public NetworkInterface? CurrentInterface => Location.Interface;

    /// <summary>Transmitted or in transit - it has left the source but is neither delivered nor dropped.</summary>
    public bool IsInFlight => State is PacketState.Transmitted or PacketState.InTransit;

    public bool IsDelivered => State == PacketState.Delivered;

    public bool IsDropped => State == PacketState.Dropped;

    /// <summary>Reached a terminal state - delivered or dropped.</summary>
    public bool HasCompleted => State is PacketState.Delivered or PacketState.Dropped;

    public void MarkTransmitted(string? note = null) => Transition(PacketState.Transmitted, note);

    public void MarkInTransit(string? note = null) => Transition(PacketState.InTransit, note);

    public void MarkDelivered(string? note = null)
    {
        Transition(PacketState.Delivered, note);
        Location = PacketLocation.Delivered;
    }

    public void Drop(PacketDropReason reason, string? note = null)
    {
        ArgumentNullException.ThrowIfNull(reason);
        Transition(PacketState.Dropped, note);
        DropReason = reason;
        Location = PacketLocation.Dropped;
    }

    // ----- Movement (engine-only). The IPacketEngine validates the move against the topology and
    // then calls these; nothing else may reposition a packet, so simulation state stays controlled
    // through domain operations rather than public setters. -----

    /// <summary>
    /// Appends <paramref name="hop"/> to <see cref="Hops"/> and moves <see cref="Location"/> to the
    /// hop's arrival interface. Rejected for a packet that has already reached a terminal state.
    /// </summary>
    internal void RecordHop(PacketHop hop)
    {
        ArgumentNullException.ThrowIfNull(hop);
        if (HasCompleted)
        {
            throw new DomainException(
                $"Packet '{Id}' has already been {(IsDelivered ? "delivered" : "dropped")} and cannot be moved.");
        }

        _hops.Add(hop);
        Location = hop.ToInterface is not null
            ? PacketLocation.AtInterface(hop.ToInterface)
            : PacketLocation.AtDevice(hop.ToDevice);
    }

    /// <summary>Places the packet at an explicit location without recording a hop (e.g. the engine seeding a start point).</summary>
    internal void PlaceAt(PacketLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (HasCompleted)
        {
            throw new DomainException(
                $"Packet '{Id}' has already been {(IsDelivered ? "delivered" : "dropped")}; its location is fixed.");
        }

        Location = location;
    }

    /// <summary>
    /// Structural validation only: a non-blank <see cref="Protocol"/> (guaranteed by construction)
    /// and, if a <see cref="Payload"/> is present, a structurally valid one. No protocol semantics.
    /// </summary>
    public PacketValidationResult Validate()
    {
        if (Payload is null)
        {
            return PacketValidationResult.Valid;
        }

        var payloadResult = Payload.Validate();
        return payloadResult.IsValid
            ? PacketValidationResult.Valid
            : new PacketValidationResult(payloadResult.Errors);
    }

    private void Transition(PacketState to, string? note)
    {
        if (!IsLegalTransition(State, to))
        {
            throw new DomainException($"A packet cannot move from '{State}' to '{to}'.");
        }

        var from = State;
        State = to;
        _stateHistory.Add(new PacketStateTransition(from, to, DateTimeOffset.UtcNow, note));
    }

    private static bool IsLegalTransition(PacketState from, PacketState to) => (from, to) switch
    {
        (PacketState.Created, PacketState.Transmitted) => true,
        (PacketState.Transmitted, PacketState.InTransit) => true,
        (PacketState.Transmitted, PacketState.Delivered) => true,
        (PacketState.InTransit, PacketState.Delivered) => true,
        (PacketState.Created or PacketState.Transmitted or PacketState.InTransit, PacketState.Dropped) => true,
        _ => false,
    };
}
