namespace NetSim.Core.Tcp;

/// <summary>What <see cref="ITcpConnectionManager.AcceptSegment"/> / <see cref="ITcpLayer.HandleIncoming"/> decided to do with an inbound, already-validated TCP segment.</summary>
public enum TcpProcessingOutcomeKind
{
    /// <summary>The segment was structurally invalid, checksum-invalid, or addressed to an interface that does not own the destination - never reached the connection manager.</summary>
    Dropped,

    /// <summary>No listener and no existing connection matched - a RST was generated (brief section 18/19: "connection refused").</summary>
    ConnectionRefused,

    /// <summary>A SYN matched a listener; a new connection was created in <see cref="TcpConnectionState.SynReceived"/> and a SYN+ACK was generated.</summary>
    SynReceived,

    /// <summary>A SYN+ACK matched a <see cref="TcpConnectionState.SynSent"/> connection; it moved to <see cref="TcpConnectionState.Established"/> and a final ACK was generated.</summary>
    HandshakeCompleted,

    /// <summary>The final ACK matched a <see cref="TcpConnectionState.SynReceived"/> connection; it moved to <see cref="TcpConnectionState.Established"/>. No response segment.</summary>
    ConnectionEstablished,

    /// <summary>A data segment was accepted in sequence; the payload was delivered and an ACK was generated.</summary>
    DataDelivered,

    /// <summary>A pure ACK (or one that only acknowledges previously sent data) was processed. No response segment.</summary>
    AckProcessed,

    /// <summary>A FIN was accepted; an ACK was generated and the connection began closing.</summary>
    FinReceived,

    /// <summary>The connection's final ACK arrived and it reached a fully-closed/lingering state. No response segment.</summary>
    ConnectionClosed,

    /// <summary>An RST was received (or generated locally); the connection was torn down immediately.</summary>
    Reset,

    /// <summary>The segment did not match the connection's current state and was ignored (no response, no state change) - not severe enough to warrant a RST.</summary>
    Ignored,
}
