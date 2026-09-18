namespace NetSim.Core.Tcp;

/// <summary>
/// The TCP connection state machine (RFC 793 section 3.2, brief section 11). This phase does not
/// model every real-world edge case (retransmission timeouts, simultaneous-open SYN-SYN, half-open
/// resets) - only the transitions <see cref="TcpConnectionManager"/> actually drives are legal; see
/// its class documentation for the full transition table.
/// </summary>
public enum TcpConnectionState
{
    /// <summary>No connection exists. The implicit starting/ending state - never itself stored in the connection table.</summary>
    Closed,

    /// <summary>A server is waiting for a connection request on a bound port (passive open).</summary>
    Listen,

    /// <summary>A client has sent a SYN and is waiting for the matching SYN+ACK (active open).</summary>
    SynSent,

    /// <summary>A server received a SYN, sent SYN+ACK, and is waiting for the final ACK.</summary>
    SynReceived,

    /// <summary>The three-way handshake is complete; data may flow in both directions.</summary>
    Established,

    /// <summary>The local side has sent a FIN and is waiting for it to be acknowledged (and/or a FIN from the remote side).</summary>
    FinWait1,

    /// <summary>The local side's FIN was acknowledged; waiting for the remote side's FIN.</summary>
    FinWait2,

    /// <summary>The remote side sent a FIN first; the local side has acknowledged it but has not yet closed its own side.</summary>
    CloseWait,

    /// <summary>The local side closed after receiving a FIN and is waiting for its own FIN to be acknowledged.</summary>
    LastAck,

    /// <summary>Both sides closed at about the same time - each sent a FIN before acknowledging the other's.</summary>
    Closing,

    /// <summary>Both FINs have been exchanged and acknowledged; the connection lingers here until removed (no simulated timer exists yet - see <see cref="TcpConnectionManager"/>).</summary>
    TimeWait,
}
