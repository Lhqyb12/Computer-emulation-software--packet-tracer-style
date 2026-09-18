namespace NetSim.Core.Packets;

/// <summary>Why a requested <see cref="Packet"/> movement did or did not happen.</summary>
public enum PacketMovementStatus
{
    /// <summary>The packet crossed the connection; <see cref="PacketMovementResult.Hop"/> describes it.</summary>
    Moved,

    /// <summary>The egress interface has no connection to move across.</summary>
    NoConnection,

    /// <summary>The egress interface does not belong to the packet's current device.</summary>
    InterfaceNotOnDevice,

    /// <summary>The packet's current device is not part of the topology the request was evaluated against.</summary>
    DeviceNotInTopology,

    /// <summary>The egress interface is not part of the topology.</summary>
    InterfaceNotInTopology,

    /// <summary>The connection does not actually have the egress interface as one of its endpoints.</summary>
    EndpointMismatch,

    /// <summary>The device on the far side of the connection is no longer in the topology (removed).</summary>
    DeviceRemoved,
}

/// <summary>
/// The outcome of an <see cref="IPacketEngine"/> movement request. Structural / topology
/// mismatches are reported here rather than thrown - the caller (a future routing or simulation
/// engine) inspects <see cref="Status"/> and decides what to do. Precondition violations (unknown
/// packet, a packet that has already reached a terminal state) are a different class of problem
/// and surface as <see cref="Common.Exceptions.DomainException"/> instead.
/// </summary>
public sealed class PacketMovementResult
{
    private PacketMovementResult(
        bool isSuccess,
        PacketMovementStatus status,
        PacketHop? hop,
        PacketLocation? newLocation,
        string? failureReason)
    {
        IsSuccess = isSuccess;
        Status = status;
        Hop = hop;
        NewLocation = newLocation;
        FailureReason = failureReason;
    }

    public bool IsSuccess { get; }

    public PacketMovementStatus Status { get; }

    /// <summary>The hop that was recorded - non-null only when <see cref="IsSuccess"/>.</summary>
    public PacketHop? Hop { get; }

    /// <summary>The packet's location after the move - non-null only when <see cref="IsSuccess"/>.</summary>
    public PacketLocation? NewLocation { get; }

    /// <summary>Human-readable explanation - non-null only when the move was rejected.</summary>
    public string? FailureReason { get; }

    public static PacketMovementResult Moved(PacketHop hop, PacketLocation newLocation) =>
        new(true, PacketMovementStatus.Moved, hop, newLocation, null);

    public static PacketMovementResult Rejected(PacketMovementStatus status, string reason) =>
        new(false, status, null, null, reason);
}
