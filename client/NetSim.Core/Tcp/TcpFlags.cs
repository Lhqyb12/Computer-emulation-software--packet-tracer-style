namespace NetSim.Core.Tcp;

/// <summary>
/// The TCP control bits (RFC 793), as a <c>[Flags]</c> enum so a segment's flag set is type-safe and
/// combinable (brief section 10: "SYN", "SYN + ACK", "FIN + ACK", ...) rather than five loose
/// booleans. Bit values match the real TCP header layout (URG=0x20 .. FIN=0x01) purely for
/// familiarity - nothing in this simulation serialises a byte-exact header.
/// </summary>
[Flags]
public enum TcpFlags : byte
{
    None = 0,

    /// <summary>Finish - sender has no more data; begins connection termination.</summary>
    Fin = 0x01,

    /// <summary>Synchronize - requests connection establishment and carries an initial sequence number.</summary>
    Syn = 0x02,

    /// <summary>Reset - aborts the connection immediately.</summary>
    Rst = 0x04,

    /// <summary>Push - asks the receiver to deliver buffered data to the application without delay.</summary>
    Psh = 0x08,

    /// <summary>Acknowledgment - the <see cref="TcpSegment.AcknowledgmentNumber"/> field is valid.</summary>
    Ack = 0x10,

    /// <summary>Urgent - the <see cref="TcpSegment.UrgentPointer"/> field is valid. Modelled for completeness; nothing in this phase acts on it.</summary>
    Urg = 0x20,
}

/// <summary>Convenience helpers over <see cref="TcpFlags"/>.</summary>
public static class TcpFlagsExtensions
{
    /// <summary>True when every bit in <paramref name="flag"/> is set in <paramref name="flags"/>.</summary>
    public static bool Has(this TcpFlags flags, TcpFlags flag) => (flags & flag) == flag;

    /// <summary>Renders a flag set the way the brief writes it, e.g. "SYN+ACK", "FIN+ACK", "-" for none.</summary>
    public static string Describe(this TcpFlags flags)
    {
        if (flags == TcpFlags.None)
        {
            return "-";
        }

        // Primary control flags first, ACK last - matches the brief's own examples ("SYN + ACK", "FIN + ACK").
        var names = new List<string>(6);
        if (flags.Has(TcpFlags.Syn)) names.Add("SYN");
        if (flags.Has(TcpFlags.Fin)) names.Add("FIN");
        if (flags.Has(TcpFlags.Rst)) names.Add("RST");
        if (flags.Has(TcpFlags.Psh)) names.Add("PSH");
        if (flags.Has(TcpFlags.Urg)) names.Add("URG");
        if (flags.Has(TcpFlags.Ack)) names.Add("ACK");
        return string.Join('+', names);
    }
}
