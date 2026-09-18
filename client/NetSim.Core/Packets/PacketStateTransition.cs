namespace NetSim.Core.Packets;

/// <summary>
/// One entry in a <see cref="Packet"/>'s <see cref="Packet.StateHistory"/>: a single
/// <see cref="PacketState"/> change, when it happened, and an optional human-readable note
/// (e.g. which interface transmitted it, why it was dropped). A plain data carrier that a future
/// event timeline / packet inspector can render without re-deriving anything.
/// </summary>
public readonly record struct PacketStateTransition(
    PacketState From,
    PacketState To,
    DateTimeOffset AtUtc,
    string? Note)
{
    public override string ToString() => Note is null ? $"{From} -> {To}" : $"{From} -> {To} ({Note})";
}
