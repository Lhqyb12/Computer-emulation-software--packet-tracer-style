namespace NetSim.Core.Packets;

/// <summary>
/// Payload for the <see cref="IPacketEngine"/> notifications
/// (<see cref="IPacketEngine.PacketCreated"/> / <see cref="IPacketEngine.PacketStateChanged"/> /
/// <see cref="IPacketEngine.PacketProcessed"/>). A plain data carrier so higher layers - a
/// future packet visualization, event timeline, monitor or AI debugger - can react to exactly
/// what happened without the Core layer knowing how they dispatch.
/// </summary>
public sealed class PacketEventArgs : EventArgs
{
    public PacketEventArgs(Packet packet, PacketStateTransition? transition = null, PacketHop? hop = null)
    {
        Packet = packet;
        Transition = transition;
        Hop = hop;
    }

    public Packet Packet { get; }

    /// <summary>The state change just applied, or null for the initial <see cref="IPacketEngine.PacketCreated"/> notification and for a move.</summary>
    public PacketStateTransition? Transition { get; }

    /// <summary>The hop just recorded, set only on the <see cref="IPacketEngine.PacketMoved"/> notification.</summary>
    public PacketHop? Hop { get; }
}
