namespace NetSim.Core.Packets;

/// <summary>
/// A structured reason a <see cref="Packet"/> was dropped: a stable <see cref="Code"/> for
/// programmatic handling and a human-readable <see cref="Description"/> for diagnostics. The
/// engine ships only the protocol-agnostic reasons below; each protocol layer (Ethernet in
/// Phase 17, IPv4 later, ...) contributes its own catalogue of <see cref="PacketDropReason"/>
/// values rather than dropping with a bare string, so drop causes stay enumerable and testable.
/// Value-based equality (record) - two reasons are equal when their code and description match.
/// </summary>
public sealed record PacketDropReason(string Code, string Description)
{
    /// <summary>The packet failed structural validation before it could enter the simulation or a processing step.</summary>
    public static PacketDropReason InvalidPacket { get; } = new("INVALID_PACKET", "The packet failed structural validation.");

    /// <summary>The packet's payload failed its own structural validation.</summary>
    public static PacketDropReason InvalidPayload { get; } = new("INVALID_PAYLOAD", "The packet payload failed structural validation.");

    /// <summary>No processor is available for the packet's protocol / encapsulated type yet.</summary>
    public static PacketDropReason UnsupportedProtocol { get; } = new("UNSUPPORTED_PROTOCOL", "No processor is available for the packet's protocol.");

    public override string ToString() => $"{Code}: {Description}";
}
