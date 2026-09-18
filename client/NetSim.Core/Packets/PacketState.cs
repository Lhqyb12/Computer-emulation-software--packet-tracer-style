namespace NetSim.Core.Packets;

/// <summary>
/// The lifecycle stage of a <see cref="Packet"/>. The legal path is
/// <see cref="Created"/> -&gt; <see cref="Transmitted"/> -&gt; <see cref="InTransit"/> -&gt;
/// <see cref="Delivered"/>, with <see cref="Dropped"/> reachable from any non-terminal state
/// (and <see cref="Transmitted"/> -&gt; <see cref="Delivered"/> allowed directly, for a delivery
/// that does not model an in-transit leg). <see cref="Delivered"/> and <see cref="Dropped"/> are
/// terminal. Transition rules are enforced by <see cref="Packet"/> itself.
/// </summary>
public enum PacketState
{
    Created,
    Transmitted,
    InTransit,
    Delivered,
    Dropped,
}
