namespace NetSim.Core.Dhcp;

/// <summary>
/// The outcome of validating a <see cref="DhcpServerConfiguration"/> (brief section 27). Mirrors
/// <see cref="Packets.PacketValidationResult"/> / <see cref="Topology.TopologyValidationResult"/>:
/// a healthy configuration is <see cref="IsValid"/>; otherwise every problem is listed so the UI
/// can report them all at once rather than one per attempt.
/// </summary>
public sealed class DhcpConfigurationValidationResult
{
    public static DhcpConfigurationValidationResult Valid { get; } = new([]);

    public DhcpConfigurationValidationResult(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public static DhcpConfigurationValidationResult Invalid(params string[] errors) => new(errors);

    public override string ToString() => IsValid ? "valid" : string.Join("; ", Errors);
}
