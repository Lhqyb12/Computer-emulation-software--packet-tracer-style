namespace NetSim.Core.Tcp;

/// <summary>The result of <see cref="ITcpConnectionManager.Connect"/>: the new connection (now in <see cref="TcpConnectionState.SynSent"/>) plus the SYN segment the caller must transmit.</summary>
public sealed record TcpConnectResult(TcpConnection Connection, TcpSegment Syn);
