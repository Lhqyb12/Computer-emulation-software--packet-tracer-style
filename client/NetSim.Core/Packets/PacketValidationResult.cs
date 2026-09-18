namespace NetSim.Core.Packets;

/// <summary>
/// The outcome of a structural check on a <see cref="Packet"/> or an <see cref="IPacketPayload"/>:
/// the list of problems found, or an empty list when it is structurally sound. Mirrors the shape
/// of <see cref="Topology.TopologyValidationResult"/> - validation never throws for a recoverable
/// problem, the caller decides what to do with the report. "Structural" only: a missing/blank
/// field or a broken encapsulation chain, never protocol semantics (those arrive with each
/// protocol layer in later phases).
/// </summary>
public sealed class PacketValidationResult
{
    private static readonly IReadOnlyList<string> NoErrors = [];

    public PacketValidationResult(IReadOnlyList<string> errors)
    {
        Errors = errors.Count == 0 ? NoErrors : errors;
    }

    public IReadOnlyList<string> Errors { get; }

    /// <summary>True when no structural problems were found.</summary>
    public bool IsValid => Errors.Count == 0;

    public static PacketValidationResult Valid { get; } = new(NoErrors);

    public static PacketValidationResult Invalid(params string[] errors) => new(errors);
}
