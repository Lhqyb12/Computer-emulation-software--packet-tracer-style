namespace NetSim.Core.Topology;

/// <summary>
/// Classifies a single structural problem found by <see cref="Network.Validate"/>.
/// </summary>
public enum TopologyValidationIssueKind
{
    /// <summary>A connection endpoint belongs to a device that is not registered in the topology.</summary>
    OrphanedConnection,

    /// <summary>An interface's <see cref="Networking.NetworkInterface.Device"/> is not the device that owns it in the topology.</summary>
    InvalidInterfaceOwnership,

    /// <summary>The same connection object appears more than once, or two connections join the exact same interface pair.</summary>
    DuplicateConnection,

    /// <summary>A connection joins an interface to itself.</summary>
    SelfConnection,

    /// <summary>An interface referenced by a registered connection does not report that same connection back.</summary>
    InterfaceConnectionMismatch,

    /// <summary>An interface reports a connection that the topology does not know about.</summary>
    UntrackedConnection,
}

/// <summary>One structural problem, with enough context to locate it.</summary>
/// <param name="Kind">What class of problem this is.</param>
/// <param name="Description">Human-readable explanation, safe to show in a diagnostics panel.</param>
public readonly record struct TopologyValidationIssue(TopologyValidationIssueKind Kind, string Description)
{
    public override string ToString() => $"{Kind}: {Description}";
}

/// <summary>
/// The outcome of <see cref="Network.Validate"/>: the list of structural problems found, or an
/// empty list when the topology is internally consistent. Validation never throws for a
/// recoverable inconsistency - callers decide what to do with the report.
/// </summary>
public sealed class TopologyValidationResult
{
    private static readonly IReadOnlyList<TopologyValidationIssue> NoIssues = [];

    public TopologyValidationResult(IReadOnlyList<TopologyValidationIssue> issues)
    {
        Issues = issues.Count == 0 ? NoIssues : issues;
    }

    public IReadOnlyList<TopologyValidationIssue> Issues { get; }

    /// <summary>True when no structural problems were found.</summary>
    public bool IsValid => Issues.Count == 0;

    public static TopologyValidationResult Valid { get; } = new(NoIssues);
}
