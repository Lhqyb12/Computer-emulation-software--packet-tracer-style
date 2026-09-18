using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Ethernet;

/// <summary>
/// The outcome of an <see cref="IEthernetTransmissionService.Transmit"/> call. It reuses the
/// Phase 16 <see cref="PacketProcessingOutcome"/> vocabulary so Ethernet does not introduce a
/// second, parallel result model. On success it carries the tracked <see cref="Packet"/> and the
/// resolved source/destination interfaces; on failure it carries a structured
/// <see cref="DropReason"/> (from <see cref="EthernetDropReasons"/>) and whichever interfaces
/// were resolved before the failure. A dropped transmission never creates a tracked
/// <see cref="Packet"/> - nothing entered the simulation.
/// </summary>
public sealed class EthernetTransmissionResult
{
    private EthernetTransmissionResult(
        PacketProcessingOutcome outcome,
        PacketDropReason? dropReason,
        string? detail,
        NetworkInterface? sourceInterface,
        NetworkInterface? destinationInterface,
        Packet? packet)
    {
        Outcome = outcome;
        DropReason = dropReason;
        Detail = detail;
        SourceInterface = sourceInterface;
        DestinationInterface = destinationInterface;
        Packet = packet;
    }

    public PacketProcessingOutcome Outcome { get; }

    public bool IsSuccess => Outcome is PacketProcessingOutcome.Transmitted or PacketProcessingOutcome.Delivered;

    /// <summary>The structured reason the frame was dropped - null when <see cref="IsSuccess"/>.</summary>
    public PacketDropReason? DropReason { get; }

    /// <summary>Optional human-readable context, safe to show in a diagnostics panel.</summary>
    public string? Detail { get; }

    /// <summary>The transmitting interface, when it was resolved.</summary>
    public NetworkInterface? SourceInterface { get; }

    /// <summary>The receiving interface on the far side of the cable, when it was resolved.</summary>
    public NetworkInterface? DestinationInterface { get; }

    /// <summary>The tracked packet carrying the frame - set only on success.</summary>
    public Packet? Packet { get; }

    internal static EthernetTransmissionResult Delivered(
        NetworkInterface source,
        NetworkInterface destination,
        Packet packet,
        string? detail) =>
        new(PacketProcessingOutcome.Delivered, null, detail, source, destination, packet);

    internal static EthernetTransmissionResult Dropped(
        PacketDropReason reason,
        string? detail,
        NetworkInterface? source = null,
        NetworkInterface? destination = null) =>
        new(PacketProcessingOutcome.Dropped, reason, detail, source, destination, null);

    internal static EthernetTransmissionResult Invalid(
        string detail,
        NetworkInterface? source = null) =>
        new(PacketProcessingOutcome.Invalid, EthernetDropReasons.InvalidFrame, detail, source, null, null);
}
