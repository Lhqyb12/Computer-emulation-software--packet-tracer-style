using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Packets;

namespace NetSim.Core.Tcp;

/// <summary>
/// One simulated TCP connection: its identity (<see cref="Key"/>), current
/// <see cref="TcpConnectionState"/>, and the sequence/acknowledgment bookkeeping the brief's
/// sections 13-15 ask for. A plain, mostly-passive data holder - <see cref="TransitionTo"/> is the
/// only self-enforced rule (a legal-transitions table, exactly like <see cref="Packet"/>'s own state
/// machine); every other mutation is driven by <see cref="TcpConnectionManager"/>, which alone
/// decides <em>when</em> a transition or sequence-number advance happens. Nothing outside
/// <see cref="TcpConnectionManager"/> constructs or mutates one.
/// </summary>
public sealed class TcpConnection
{
    private readonly List<IPacketPayload> _receivedData = [];

    internal TcpConnection(TcpConnectionKey key, NetworkDevice device, bool isServerSide, Action<TcpConnection, IPacketPayload>? onDataReceived)
    {
        Key = key;
        Device = device;
        IsServerSide = isServerSide;
        OnDataReceived = onDataReceived;
        State = TcpConnectionState.Closed;
    }

    public TcpConnectionKey Key { get; }

    /// <summary>The local device that owns this connection.</summary>
    public NetworkDevice Device { get; }

    /// <summary>True for the side that answered a SYN (passive open); false for the side that initiated with <see cref="ITcpConnectionManager.Connect"/>.</summary>
    public bool IsServerSide { get; }

    public TcpConnectionState State { get; private set; }

    /// <summary>This side's initial sequence number (the value carried by the SYN this side sent).</summary>
    public uint LocalInitialSequenceNumber { get; private set; }

    /// <summary>The remote side's initial sequence number (the value carried by the SYN it sent) - 0 until learned.</summary>
    public uint RemoteInitialSequenceNumber { get; private set; }

    /// <summary>SND.NXT - the sequence number this side will use for its next outgoing segment.</summary>
    public uint LocalNextSequenceNumber { get; private set; }

    /// <summary>RCV.NXT - the sequence number this side expects next from the remote side; also the value it acknowledges with.</summary>
    public uint RemoteNextSequenceNumber { get; private set; }

    /// <summary>Invoked for every application data payload delivered to this connection - the seam a future application protocol (Phase 23+) builds on.</summary>
    public Action<TcpConnection, IPacketPayload>? OnDataReceived { get; }

    /// <summary>Every application-data payload delivered so far, oldest first - basic transport diagnostics (brief section 33).</summary>
    public IReadOnlyList<IPacketPayload> ReceivedData => _receivedData.AsReadOnly();

    public bool IsOpen => State is not TcpConnectionState.Closed;

    internal void SetLocalInitialSequence(uint isn)
    {
        LocalInitialSequenceNumber = isn;
        LocalNextSequenceNumber = isn;
    }

    internal void SetRemoteInitialSequence(uint isn)
    {
        RemoteInitialSequenceNumber = isn;
        RemoteNextSequenceNumber = isn;
    }

    internal void AdvanceLocalSequence(int byBytes) => LocalNextSequenceNumber = unchecked(LocalNextSequenceNumber + (uint)byBytes);

    internal void AdvanceRemoteSequence(int byBytes) => RemoteNextSequenceNumber = unchecked(RemoteNextSequenceNumber + (uint)byBytes);

    internal void DeliverData(IPacketPayload payload)
    {
        _receivedData.Add(payload);
        OnDataReceived?.Invoke(this, payload);
    }

    /// <summary>
    /// Moves to <paramref name="to"/> if the transition is legal for the current state; throws
    /// <see cref="DomainException"/> otherwise. See <see cref="TcpConnectionManager"/>'s class
    /// documentation for the full, deliberately limited transition table this phase supports.
    /// </summary>
    internal void TransitionTo(TcpConnectionState to)
    {
        if (!IsLegalTransition(State, to))
        {
            throw new DomainException($"TCP connection {Key} cannot move from '{State}' to '{to}'.");
        }

        State = to;
    }

    private static bool IsLegalTransition(TcpConnectionState from, TcpConnectionState to) => (from, to) switch
    {
        (TcpConnectionState.Closed, TcpConnectionState.SynSent) => true,
        (TcpConnectionState.Closed, TcpConnectionState.SynReceived) => true,
        (TcpConnectionState.SynSent, TcpConnectionState.Established) => true,
        (TcpConnectionState.SynReceived, TcpConnectionState.Established) => true,
        (TcpConnectionState.Established, TcpConnectionState.FinWait1) => true,
        (TcpConnectionState.Established, TcpConnectionState.CloseWait) => true,
        (TcpConnectionState.FinWait1, TcpConnectionState.FinWait2) => true,
        (TcpConnectionState.FinWait1, TcpConnectionState.Closing) => true,
        (TcpConnectionState.FinWait1, TcpConnectionState.TimeWait) => true,
        (TcpConnectionState.FinWait2, TcpConnectionState.TimeWait) => true,
        (TcpConnectionState.Closing, TcpConnectionState.TimeWait) => true,
        (TcpConnectionState.CloseWait, TcpConnectionState.LastAck) => true,
        (TcpConnectionState.LastAck, TcpConnectionState.Closed) => true,
        (TcpConnectionState.TimeWait, TcpConnectionState.Closed) => true,

        // RST forces an immediate close from any open state.
        ( not TcpConnectionState.Closed, TcpConnectionState.Closed) => true,

        _ => false,
    };

    public override string ToString() => $"{Key} [{State}]";
}
