namespace NetSim.Core.Packets;

/// <summary>
/// The result of running a <see cref="Packet"/> through an <see cref="IPacketProcessor"/>: the
/// <see cref="Outcome"/>, an optional structured <see cref="DropReason"/> (set whenever the
/// outcome is not a success), and an optional free-text <see cref="Detail"/> for diagnostics.
/// Built through the static factories so an outcome and its reason are always chosen together.
/// A protocol layer can carry richer, layer-specific result data by deriving from this type.
/// </summary>
public class PacketProcessingResult
{
    protected PacketProcessingResult(PacketProcessingOutcome outcome, PacketDropReason? dropReason, string? detail)
    {
        Outcome = outcome;
        DropReason = dropReason;
        Detail = detail;
    }

    public PacketProcessingOutcome Outcome { get; }

    /// <summary>The structured drop reason - null only when <see cref="IsSuccess"/> is true.</summary>
    public PacketDropReason? DropReason { get; }

    /// <summary>Optional human-readable context, safe to show in a diagnostics panel.</summary>
    public string? Detail { get; }

    public bool IsSuccess => Outcome is PacketProcessingOutcome.Transmitted or PacketProcessingOutcome.Delivered;

    public static PacketProcessingResult Transmitted(string? detail = null) =>
        new(PacketProcessingOutcome.Transmitted, null, detail);

    public static PacketProcessingResult Delivered(string? detail = null) =>
        new(PacketProcessingOutcome.Delivered, null, detail);

    public static PacketProcessingResult Dropped(PacketDropReason reason, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return new(PacketProcessingOutcome.Dropped, reason, detail);
    }

    public static PacketProcessingResult Unsupported(string detail) =>
        new(PacketProcessingOutcome.Unsupported, PacketDropReason.UnsupportedProtocol, detail);

    public static PacketProcessingResult Invalid(string detail) =>
        new(PacketProcessingOutcome.Invalid, PacketDropReason.InvalidPacket, detail);
}
