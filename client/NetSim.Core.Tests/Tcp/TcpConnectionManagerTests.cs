using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tcp;
using NetSim.Core.Transport;

namespace NetSim.Core.Tests.Tcp;

/// <summary>
/// Unit-level tests for <see cref="TcpConnectionManager"/>: listeners, active/passive open, the
/// three-way handshake, data transfer with sequence/ack bookkeeping, normal termination (both the
/// active- and passive-closer paths), reset, and invalid-state handling. Drives the manager
/// directly with hand-built <see cref="TcpSegment"/>s (as <see cref="ITcpLayer"/> would after
/// gating structure/checksum/ownership) - no Ethernet/ARP/IPv4 plumbing needed here; see
/// <see cref="TcpHandshakeIntegrationTests"/> for the full wire-level scenario.
/// </summary>
public class TcpConnectionManagerTests
{
    private static readonly IPv4Address ClientIp = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address ServerIp = IPv4Address.Parse("192.168.1.20");
    private static readonly Port ServerPort = Port.Create(80);

    private sealed record Lab(TcpConnectionManager Manager, NetworkInterface ClientInterface, NetworkInterface ServerInterface)
    {
        public NetworkDevice ServerDevice => ServerInterface.Device;
    }

    private static Lab BuildLab(uint seed = 1000, uint step = 10000)
    {
        var manager = new TcpConnectionManager(new SequentialInitialSequenceNumberGenerator(seed, step));
        var client = new Pc("Client");
        var clientInterface = client.AddInterface("Eth0", InterfaceType.FastEthernet);
        clientInterface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(ClientIp, 24));
        var server = new Pc("Server");
        var serverInterface = server.AddInterface("Eth0", InterfaceType.FastEthernet);
        serverInterface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(ServerIp, 24));
        return new Lab(manager, clientInterface, serverInterface);
    }

    // ---- Listeners ----

    [Fact]
    public void Listen_ThenIsListening_ReturnsTrue()
    {
        var lab = BuildLab();

        lab.Manager.Listen(lab.ServerDevice, ServerPort);

        Assert.True(lab.Manager.IsListening(lab.ServerDevice, ServerPort));
    }

    [Fact]
    public void Listen_SamePortTwice_Throws()
    {
        var lab = BuildLab();
        lab.Manager.Listen(lab.ServerDevice, ServerPort);

        Assert.Throws<DomainException>(() => lab.Manager.Listen(lab.ServerDevice, ServerPort));
    }

    [Fact]
    public void StopListening_RemovesTheListener()
    {
        var lab = BuildLab();
        lab.Manager.Listen(lab.ServerDevice, ServerPort);

        Assert.True(lab.Manager.StopListening(lab.ServerDevice, ServerPort));
        Assert.False(lab.Manager.IsListening(lab.ServerDevice, ServerPort));
    }

    // ---- Active open / Connect ----

    [Fact]
    public void Connect_CreatesAConnectionInSynSent_AndReturnsTheSynSegment()
    {
        var lab = BuildLab();

        var result = lab.Manager.Connect(lab.ClientInterface, ServerIp, ServerPort, Port.Create(50000));

        Assert.Equal(TcpConnectionState.SynSent, result.Connection.State);
        Assert.True(result.Syn.IsSyn);
        Assert.False(result.Syn.IsAck);
        Assert.Equal(1000u, result.Syn.SequenceNumber);
    }

    [Fact]
    public void Connect_NoIPv4OnInterface_Throws()
    {
        var lab = BuildLab();
        var bareDevice = new Pc("Bare");
        var bareInterface = bareDevice.AddInterface("Eth0", InterfaceType.FastEthernet);

        Assert.Throws<DomainException>(() => lab.Manager.Connect(bareInterface, ServerIp, ServerPort, Port.Create(50000)));
    }

    [Fact]
    public void Connect_DuplicateConnection_Throws()
    {
        var lab = BuildLab();
        lab.Manager.Connect(lab.ClientInterface, ServerIp, ServerPort, Port.Create(50000));

        Assert.Throws<DomainException>(() => lab.Manager.Connect(lab.ClientInterface, ServerIp, ServerPort, Port.Create(50000)));
    }

    [Fact]
    public void FindConnection_ReturnsTheTrackedConnection()
    {
        var lab = BuildLab();
        var result = lab.Manager.Connect(lab.ClientInterface, ServerIp, ServerPort, Port.Create(50000));

        var found = lab.Manager.FindConnection(result.Connection.Key);

        Assert.Same(result.Connection, found);
    }

    [Fact]
    public void FindConnection_Unknown_ReturnsNull()
    {
        var lab = BuildLab();

        Assert.Null(lab.Manager.FindConnection(new TcpConnectionKey(ClientIp, Port.Create(1), ServerIp, Port.Create(2))));
    }

    // ---- Three-way handshake ----

    [Fact]
    public void ThreeWayHandshake_ClientAndServer_BothReachEstablished()
    {
        var lab = BuildLab();
        lab.Manager.Listen(lab.ServerDevice, ServerPort);
        var clientPort = Port.Create(50000);

        var connectResult = lab.Manager.Connect(lab.ClientInterface, ServerIp, ServerPort, clientPort);
        var client = connectResult.Connection;
        Assert.Equal(TcpConnectionState.SynSent, client.State);

        // Server receives SYN -> SynReceived + SYN/ACK.
        var synReport = lab.Manager.AcceptSegment(lab.ServerInterface, ServerIp, ClientIp, connectResult.Syn);
        Assert.Equal(TcpProcessingOutcomeKind.SynReceived, synReport.Kind);
        var server = synReport.Connection!;
        Assert.Equal(TcpConnectionState.SynReceived, server.State);
        Assert.True(synReport.ResponseSegment!.IsSynAck);
        Assert.Equal(connectResult.Syn.SequenceNumber + 1, synReport.ResponseSegment.AcknowledgmentNumber);

        // Client receives SYN+ACK -> Established + final ACK.
        var handshakeReport = lab.Manager.AcceptSegment(lab.ClientInterface, ClientIp, ServerIp, synReport.ResponseSegment);
        Assert.Equal(TcpProcessingOutcomeKind.HandshakeCompleted, handshakeReport.Kind);
        Assert.Equal(TcpConnectionState.Established, client.State);
        Assert.True(handshakeReport.ResponseSegment!.IsAck);
        Assert.False(handshakeReport.ResponseSegment.IsSyn);

        // Server receives the final ACK -> Established.
        var establishedReport = lab.Manager.AcceptSegment(lab.ServerInterface, ServerIp, ClientIp, handshakeReport.ResponseSegment);
        Assert.Equal(TcpProcessingOutcomeKind.ConnectionEstablished, establishedReport.Kind);
        Assert.Equal(TcpConnectionState.Established, server.State);
        Assert.Null(establishedReport.ResponseSegment);
    }

    [Fact]
    public void Handshake_NoListenerAndNoConnection_RefusesWithReset()
    {
        var lab = BuildLab();
        var syn = TcpSegment.CreateSyn(ClientIp, ServerIp, Port.Create(50000), ServerPort, 1000);

        var report = lab.Manager.AcceptSegment(lab.ServerInterface, ServerIp, ClientIp, syn);

        Assert.Equal(TcpProcessingOutcomeKind.ConnectionRefused, report.Kind);
        Assert.True(report.ResponseSegment!.IsRst);
        Assert.Null(report.Connection);
    }

    // ---- Data transfer ----

    [Fact]
    public void Send_OnEstablishedConnection_AdvancesLocalSequenceByPayloadLength()
    {
        var lab = BuildLab();
        var (client, server) = EstablishConnection(lab);

        var segment = lab.Manager.Send(client, RawPayload.FromText("Hello"));

        Assert.Equal(1001u, segment.SequenceNumber);
        Assert.Equal(1006u, client.LocalNextSequenceNumber);
    }

    [Fact]
    public void Send_OnANonEstablishedConnection_Throws()
    {
        var lab = BuildLab();
        var result = lab.Manager.Connect(lab.ClientInterface, ServerIp, ServerPort, Port.Create(50000));

        Assert.Throws<DomainException>(() => lab.Manager.Send(result.Connection, RawPayload.FromText("Hello")));
    }

    [Fact]
    public void DataSegment_InSequence_IsDeliveredAndAcknowledged()
    {
        var lab = BuildLab();
        var (client, server) = EstablishConnection(lab);
        var dataSegment = lab.Manager.Send(client, RawPayload.FromText("Hello"));

        var report = lab.Manager.AcceptSegment(lab.ServerInterface, ServerIp, ClientIp, dataSegment);

        Assert.Equal(TcpProcessingOutcomeKind.DataDelivered, report.Kind);
        Assert.Equal("Hello", System.Text.Encoding.UTF8.GetString(((RawPayload)report.DeliveredPayload!).Data.Span));
        Assert.True(report.ResponseSegment!.IsAck);
        Assert.Equal(dataSegment.SequenceNumber + 5u, report.ResponseSegment.AcknowledgmentNumber);
        Assert.Single(server.ReceivedData);
    }

    [Fact]
    public void DataSegment_WithWrongSequenceNumber_IsIgnored()
    {
        var lab = BuildLab();
        var (client, _) = EstablishConnection(lab);
        var outOfOrder = TcpSegment.CreatePushAck(
            ClientIp, ServerIp, client.Key.LocalPort, client.Key.RemotePort, client.LocalNextSequenceNumber + 100, client.RemoteNextSequenceNumber, RawPayload.FromText("late"));

        var report = lab.Manager.AcceptSegment(lab.ServerInterface, ServerIp, ClientIp, outOfOrder);

        Assert.Equal(TcpProcessingOutcomeKind.Ignored, report.Kind);
    }

    [Fact]
    public void AckProcessing_PureAck_IsProcessedWithNoResponse()
    {
        var lab = BuildLab();
        var (client, server) = EstablishConnection(lab);
        var dataSegment = lab.Manager.Send(client, RawPayload.FromText("Hi"));
        var dataReport = lab.Manager.AcceptSegment(lab.ServerInterface, ServerIp, ClientIp, dataSegment);

        var ackReport = lab.Manager.AcceptSegment(lab.ClientInterface, ClientIp, ServerIp, dataReport.ResponseSegment!);

        Assert.Equal(TcpProcessingOutcomeKind.AckProcessed, ackReport.Kind);
        Assert.Null(ackReport.ResponseSegment);
    }

    // ---- Termination ----

    [Fact]
    public void NormalClose_ActiveCloserReachesTimeWait_PassiveCloserReachesClosedAutomatically()
    {
        var lab = BuildLab();
        var (client, server) = EstablishConnection(lab);

        // Client (active closer) sends FIN.
        var clientFin = lab.Manager.Close(client);
        Assert.Equal(TcpConnectionState.FinWait1, client.State);
        Assert.True(clientFin.IsFin);

        // Server receives FIN -> CloseWait, sends ACK.
        var finReport = lab.Manager.AcceptSegment(lab.ServerInterface, ServerIp, ClientIp, clientFin);
        Assert.Equal(TcpProcessingOutcomeKind.FinReceived, finReport.Kind);
        Assert.Equal(TcpConnectionState.CloseWait, server.State);

        // Client receives the ACK of its FIN -> FinWait2.
        var ackOfFinReport = lab.Manager.AcceptSegment(lab.ClientInterface, ClientIp, ServerIp, finReport.ResponseSegment!);
        Assert.Equal(TcpProcessingOutcomeKind.AckProcessed, ackOfFinReport.Kind);
        Assert.Equal(TcpConnectionState.FinWait2, client.State);

        // Server (passive closer) closes too -> LastAck, sends FIN.
        var serverFin = lab.Manager.Close(server);
        Assert.Equal(TcpConnectionState.LastAck, server.State);

        // Client receives server's FIN -> TimeWait, sends ACK.
        var serverFinReport = lab.Manager.AcceptSegment(lab.ClientInterface, ClientIp, ServerIp, serverFin);
        Assert.Equal(TcpProcessingOutcomeKind.FinReceived, serverFinReport.Kind);
        Assert.Equal(TcpConnectionState.TimeWait, client.State);

        // Server receives the final ACK -> Closed, removed automatically.
        var finalReport = lab.Manager.AcceptSegment(lab.ServerInterface, ServerIp, ClientIp, serverFinReport.ResponseSegment!);
        Assert.Equal(TcpProcessingOutcomeKind.ConnectionClosed, finalReport.Kind);
        Assert.Equal(TcpConnectionState.Closed, server.State);
        Assert.Null(lab.Manager.FindConnection(server.Key));

        // The client still lingers in TimeWait until explicitly removed (no simulated timer).
        Assert.NotNull(lab.Manager.FindConnection(client.Key));
        Assert.True(lab.Manager.RemoveConnection(client.Key));
        Assert.Equal(TcpConnectionState.Closed, client.State);
    }

    [Fact]
    public void Close_OnAConnectionThatIsNeitherEstablishedNorCloseWait_Throws()
    {
        var lab = BuildLab();
        var result = lab.Manager.Connect(lab.ClientInterface, ServerIp, ServerPort, Port.Create(50000));

        Assert.Throws<DomainException>(() => lab.Manager.Close(result.Connection));
    }

    // ---- Reset ----

    [Fact]
    public void Reset_InitiatedLocally_TransitionsToClosedAndRemovesTheConnection()
    {
        var lab = BuildLab();
        var (client, _) = EstablishConnection(lab);

        var rst = lab.Manager.Reset(client);

        Assert.True(rst.IsRst);
        Assert.Equal(TcpConnectionState.Closed, client.State);
        Assert.Null(lab.Manager.FindConnection(client.Key));
    }

    [Fact]
    public void Reset_ReceivedFromPeer_TransitionsToClosedAndRemovesTheConnection()
    {
        var lab = BuildLab();
        var (client, server) = EstablishConnection(lab);
        var rst = TcpSegment.CreateReset(ClientIp, ServerIp, client.Key.LocalPort, client.Key.RemotePort, client.LocalNextSequenceNumber);

        var report = lab.Manager.AcceptSegment(lab.ServerInterface, ServerIp, ClientIp, rst);

        Assert.Equal(TcpProcessingOutcomeKind.Reset, report.Kind);
        Assert.Equal(TcpConnectionState.Closed, server.State);
        Assert.Null(lab.Manager.FindConnection(server.Key));
    }

    // ---- Helpers ----

    /// <summary>Drives a full handshake and returns (client, server) both in <see cref="TcpConnectionState.Established"/>.</summary>
    private static (TcpConnection Client, TcpConnection Server) EstablishConnection(Lab lab)
    {
        lab.Manager.Listen(lab.ServerDevice, ServerPort);
        var connectResult = lab.Manager.Connect(lab.ClientInterface, ServerIp, ServerPort, Port.Create(50000));
        var synReport = lab.Manager.AcceptSegment(lab.ServerInterface, ServerIp, ClientIp, connectResult.Syn);
        var handshakeReport = lab.Manager.AcceptSegment(lab.ClientInterface, ClientIp, ServerIp, synReport.ResponseSegment!);
        lab.Manager.AcceptSegment(lab.ServerInterface, ServerIp, ClientIp, handshakeReport.ResponseSegment!);

        return (connectResult.Connection, synReport.Connection!);
    }
}
