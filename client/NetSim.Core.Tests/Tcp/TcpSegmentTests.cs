using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tcp;
using NetSim.Core.Transport;

namespace NetSim.Core.Tests.Tcp;

public class TcpSegmentTests
{
    private static readonly IPv4Address SourceIp = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address DestinationIp = IPv4Address.Parse("192.168.1.20");
    private static readonly Port ClientPort = Port.Create(50000);
    private static readonly Port ServerPort = Port.Create(80);

    [Fact]
    public void Create_SetsAllHeaderFields()
    {
        var segment = TcpSegment.Create(
            SourceIp, DestinationIp, ClientPort, ServerPort, sequenceNumber: 1000, acknowledgmentNumber: 2000, TcpFlags.Ack, windowSize: 4096);

        Assert.Equal(ClientPort, segment.SourcePort);
        Assert.Equal(ServerPort, segment.DestinationPort);
        Assert.Equal(1000u, segment.SequenceNumber);
        Assert.Equal(2000u, segment.AcknowledgmentNumber);
        Assert.Equal(TcpFlags.Ack, segment.Flags);
        Assert.Equal(4096, segment.WindowSize);
    }

    [Theory]
    [InlineData(TcpFlags.Syn, false, false, false)]
    [InlineData(TcpFlags.Syn | TcpFlags.Ack, true, false, false)]
    [InlineData(TcpFlags.Ack, true, false, false)]
    [InlineData(TcpFlags.Fin | TcpFlags.Ack, true, true, false)]
    [InlineData(TcpFlags.Rst, false, false, true)]
    public void FlagQueries_ReflectTheFlagsSet(TcpFlags flags, bool expectAck, bool expectFin, bool expectRst)
    {
        var segment = TcpSegment.Create(SourceIp, DestinationIp, ClientPort, ServerPort, 1, 0, flags);

        Assert.True(segment.IsSyn == flags.Has(TcpFlags.Syn));
        Assert.Equal(expectAck, segment.IsAck);
        Assert.Equal(expectFin, segment.IsFin);
        Assert.Equal(expectRst, segment.IsRst);
    }

    [Fact]
    public void CreateSyn_HasOnlySynFlag()
    {
        var syn = TcpSegment.CreateSyn(SourceIp, DestinationIp, ClientPort, ServerPort, initialSequenceNumber: 1000);

        Assert.Equal(TcpFlags.Syn, syn.Flags);
        Assert.Equal(1000u, syn.SequenceNumber);
        Assert.Equal(0u, syn.AcknowledgmentNumber);
        Assert.Equal(1, syn.SequenceLength);
    }

    [Fact]
    public void CreateSynAck_HasSynAndAckFlags()
    {
        var synAck = TcpSegment.CreateSynAck(SourceIp, DestinationIp, ServerPort, ClientPort, initialSequenceNumber: 5000, acknowledgmentNumber: 1001);

        Assert.True(synAck.IsSynAck);
        Assert.Equal(5000u, synAck.SequenceNumber);
        Assert.Equal(1001u, synAck.AcknowledgmentNumber);
    }

    [Fact]
    public void CreateAck_HasOnlyAckFlag()
    {
        var ack = TcpSegment.CreateAck(SourceIp, DestinationIp, ClientPort, ServerPort, 1001, 5001);

        Assert.Equal(TcpFlags.Ack, ack.Flags);
        Assert.Equal(0, ack.SequenceLength);
    }

    [Fact]
    public void CreateFin_HasFinAndAckFlags_AndConsumesOneSequenceNumber()
    {
        var fin = TcpSegment.CreateFin(SourceIp, DestinationIp, ClientPort, ServerPort, 1050, 5050);

        Assert.True(fin.IsFin);
        Assert.True(fin.IsAck);
        Assert.Equal(1, fin.SequenceLength);
    }

    [Fact]
    public void CreateReset_WithNoAck_HasOnlyRstFlag()
    {
        var rst = TcpSegment.CreateReset(SourceIp, DestinationIp, ClientPort, ServerPort, sequenceNumber: 0, acknowledgmentNumber: 0);

        Assert.Equal(TcpFlags.Rst, rst.Flags);
    }

    [Fact]
    public void CreateReset_WithAck_HasRstAndAckFlags()
    {
        var rst = TcpSegment.CreateReset(SourceIp, DestinationIp, ClientPort, ServerPort, sequenceNumber: 0, acknowledgmentNumber: 12345);

        Assert.Equal(TcpFlags.Rst | TcpFlags.Ack, rst.Flags);
        Assert.Equal(12345u, rst.AcknowledgmentNumber);
    }

    [Fact]
    public void SequenceLength_DataOnly_IsPayloadLength()
    {
        var segment = TcpSegment.CreatePushAck(SourceIp, DestinationIp, ClientPort, ServerPort, 1000, 0, RawPayload.FromText("Hello"));

        Assert.Equal(5, segment.SequenceLength);
    }

    [Fact]
    public void SequenceLength_SynPlusData_IsDataPlusOne()
    {
        var segment = TcpSegment.Create(SourceIp, DestinationIp, ClientPort, ServerPort, 1000, 0, TcpFlags.Syn, payload: RawPayload.FromText("Hi"));

        Assert.Equal(3, segment.SequenceLength);
    }

    [Fact]
    public void Checksum_IsComputedAtCreation_AndValidatesAgainstItsAddresses()
    {
        var segment = TcpSegment.CreateSyn(SourceIp, DestinationIp, ClientPort, ServerPort, 1000);

        Assert.NotEqual(0, segment.Checksum);
        Assert.True(segment.HasValidChecksum(SourceIp, DestinationIp));
    }

    [Fact]
    public void Checksum_ChangesWithFlags()
    {
        var syn = TcpSegment.Create(SourceIp, DestinationIp, ClientPort, ServerPort, 1000, 0, TcpFlags.Syn);
        var synAck = TcpSegment.Create(SourceIp, DestinationIp, ClientPort, ServerPort, 1000, 0, TcpFlags.Syn | TcpFlags.Ack);

        Assert.NotEqual(syn.Checksum, synAck.Checksum);
    }

    [Fact]
    public void Checksum_ChangesWithAddresses()
    {
        var segment = TcpSegment.CreateSyn(SourceIp, DestinationIp, ClientPort, ServerPort, 1000);

        Assert.False(segment.HasValidChecksum(IPv4Address.Parse("10.0.0.1"), DestinationIp));
    }

    [Fact]
    public void WithChecksum_ProducesAnInvalidChecksum_WithoutChangingOtherFields()
    {
        var segment = TcpSegment.CreateSyn(SourceIp, DestinationIp, ClientPort, ServerPort, 1000);

        var corrupted = segment.WithChecksum(unchecked((ushort)(segment.Checksum + 1)));

        Assert.False(corrupted.HasValidChecksum(SourceIp, DestinationIp));
        Assert.Equal(segment.SequenceNumber, corrupted.SequenceNumber);
        Assert.Equal(segment.Flags, corrupted.Flags);
    }

    [Fact]
    public void Validate_SynAndRstTogether_IsInvalid()
    {
        var segment = TcpSegment.Create(SourceIp, DestinationIp, ClientPort, ServerPort, 1, 0, TcpFlags.Syn | TcpFlags.Rst);

        Assert.False(segment.Validate().IsValid);
    }

    [Fact]
    public void Validate_SynAndFinTogether_IsInvalid()
    {
        var segment = TcpSegment.Create(SourceIp, DestinationIp, ClientPort, ServerPort, 1, 0, TcpFlags.Syn | TcpFlags.Fin);

        Assert.False(segment.Validate().IsValid);
    }

    [Fact]
    public void Validate_AnOrdinarySegment_IsValid()
    {
        var segment = TcpSegment.CreateAck(SourceIp, DestinationIp, ClientPort, ServerPort, 1, 1);

        Assert.True(segment.Validate().IsValid);
    }

    [Fact]
    public void PayloadType_IsTcp_AndEncapsulatedPayloadIsTheApplicationData()
    {
        var payload = RawPayload.FromText("Hello");
        var segment = TcpSegment.CreatePushAck(SourceIp, DestinationIp, ClientPort, ServerPort, 1, 1, payload);

        Assert.Equal("TCP", segment.PayloadType);
        Assert.Same(payload, segment.EncapsulatedPayload);
    }

    [Fact]
    public void Describe_RendersFlagsJoinedWithPlus()
    {
        Assert.Equal("SYN+ACK", (TcpFlags.Syn | TcpFlags.Ack).Describe());
        Assert.Equal("FIN+ACK", (TcpFlags.Fin | TcpFlags.Ack).Describe());
        Assert.Equal("RST", TcpFlags.Rst.Describe());
        Assert.Equal("-", TcpFlags.None.Describe());
    }
}
