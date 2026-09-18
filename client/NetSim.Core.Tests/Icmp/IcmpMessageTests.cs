using NetSim.Core.Common.Exceptions;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Icmp;

public class IcmpMessageTests
{
    private static readonly IPv4Address SourceIp = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address DestinationIp = IPv4Address.Parse("192.168.1.20");

    private static NetSim.Core.Icmp.IcmpOriginalDatagramInfo SampleOriginalDatagram() =>
        NetSim.Core.Icmp.IcmpOriginalDatagramInfo.FromPacket(
            IPv4Packet.Create(SourceIp, DestinationIp, RawPayload.OfSize(4), ProtocolNumber.Icmp, timeToLive: 1));

    [Fact]
    public void CreateEchoRequest_SetsTypeCodeIdentifierSequenceAndData()
    {
        var request = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(1234, 1, RawPayload.FromText("Hello"));

        Assert.Equal(NetSim.Core.Icmp.IcmpType.EchoRequest, request.Type);
        Assert.Equal(NetSim.Core.Icmp.IcmpCode.Zero, request.Code);
        Assert.Equal(1234, request.Identifier);
        Assert.Equal(1, request.SequenceNumber);
        Assert.Equal("Hello", System.Text.Encoding.UTF8.GetString(request.Data.Data.Span));
        Assert.True(request.IsEchoRequest);
        Assert.False(request.IsEchoReply);
        Assert.False(request.IsError);
    }

    [Fact]
    public void CreateEchoRequest_WithoutData_UsesAnEmptyPayload()
    {
        var request = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(1, 1);

        Assert.Equal(0, request.Data.Length);
    }

    [Fact]
    public void CreateEchoReply_SetsTypeZero()
    {
        var reply = NetSim.Core.Icmp.IcmpMessage.CreateEchoReply(1234, 1, RawPayload.FromText("Hello"));

        Assert.Equal(NetSim.Core.Icmp.IcmpType.EchoReply, reply.Type);
        Assert.True(reply.IsEchoReply);
    }

    [Fact]
    public void CreateEchoReplyTo_PreservesIdentifierSequenceAndData()
    {
        var request = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(100, 2, RawPayload.FromText("payload"));

        var reply = NetSim.Core.Icmp.IcmpMessage.CreateEchoReplyTo(request);

        Assert.True(reply.IsEchoReply);
        Assert.Equal(request.Identifier, reply.Identifier);
        Assert.Equal(request.SequenceNumber, reply.SequenceNumber);
        Assert.Equal(request.Data.Data.ToArray(), reply.Data.Data.ToArray());
    }

    [Fact]
    public void CreateEchoReplyTo_ARealEchoReply_Throws()
    {
        var reply = NetSim.Core.Icmp.IcmpMessage.CreateEchoReply(1, 1);

        Assert.Throws<DomainException>(() => NetSim.Core.Icmp.IcmpMessage.CreateEchoReplyTo(reply));
    }

    [Theory]
    [InlineData(100, 1)]
    [InlineData(100, 2)]
    [InlineData(200, 1)]
    public void IdentifierAndSequence_DistinguishOneEchoFromAnother(ushort identifier, ushort sequence)
    {
        var request = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(identifier, sequence);

        Assert.Equal(identifier, request.Identifier);
        Assert.Equal(sequence, request.SequenceNumber);
    }

    [Fact]
    public void CreateTimeExceeded_CarriesTheOriginalDatagramInfo()
    {
        var original = SampleOriginalDatagram();

        var message = NetSim.Core.Icmp.IcmpMessage.CreateTimeExceeded(original);

        Assert.Equal(NetSim.Core.Icmp.IcmpType.TimeExceeded, message.Type);
        Assert.Equal(NetSim.Core.Icmp.IcmpCode.TimeToLiveExceededInTransit, message.Code);
        Assert.Same(original, message.OriginalDatagram);
        Assert.True(message.IsError);
    }

    [Fact]
    public void CreateDestinationUnreachable_CarriesTheOriginalDatagramInfoAndReasonCode()
    {
        var original = SampleOriginalDatagram();

        var message = NetSim.Core.Icmp.IcmpMessage.CreateDestinationUnreachable(original, NetSim.Core.Icmp.IcmpCode.HostUnreachable);

        Assert.Equal(NetSim.Core.Icmp.IcmpType.DestinationUnreachable, message.Type);
        Assert.Equal(NetSim.Core.Icmp.IcmpCode.HostUnreachable, message.Code);
        Assert.Same(original, message.OriginalDatagram);
        Assert.True(message.IsError);
    }

    // ---- Checksum ----

    [Fact]
    public void EveryFactory_ProducesAValidChecksum()
    {
        var request = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(1, 1, RawPayload.FromText("abc"));
        var reply = NetSim.Core.Icmp.IcmpMessage.CreateEchoReply(1, 1, RawPayload.FromText("abc"));
        var original = SampleOriginalDatagram();
        var unreachable = NetSim.Core.Icmp.IcmpMessage.CreateDestinationUnreachable(original, NetSim.Core.Icmp.IcmpCode.HostUnreachable);
        var timeExceeded = NetSim.Core.Icmp.IcmpMessage.CreateTimeExceeded(original);

        Assert.True(request.HasValidChecksum);
        Assert.True(reply.HasValidChecksum);
        Assert.True(unreachable.HasValidChecksum);
        Assert.True(timeExceeded.HasValidChecksum);
    }

    [Fact]
    public void ChecksumChanges_WhenTheMessageContentChanges()
    {
        var a = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(1, 1, RawPayload.FromText("abc"));
        var b = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(1, 1, RawPayload.FromText("abd"));

        Assert.NotEqual(a.Checksum, b.Checksum);
    }

    [Fact]
    public void ChecksumChanges_WhenIdentifierOrSequenceChanges()
    {
        var a = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(1, 1);
        var b = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(2, 1);
        var c = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(1, 2);

        Assert.NotEqual(a.Checksum, b.Checksum);
        Assert.NotEqual(a.Checksum, c.Checksum);
    }

    [Fact]
    public void WithChecksum_ProducesAMessageWithAnInvalidChecksum_WithoutChangingOtherFields()
    {
        var request = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(1234, 1, RawPayload.FromText("Hello"));
        var corrupted = request.WithChecksum(unchecked((ushort)(request.Checksum + 1)));

        Assert.False(corrupted.HasValidChecksum);
        Assert.Equal(request.Identifier, corrupted.Identifier);
        Assert.Equal(request.SequenceNumber, corrupted.SequenceNumber);
        Assert.Equal(request.Data.Data.ToArray(), corrupted.Data.Data.ToArray());
    }

    [Fact]
    public void ComputeChecksum_MatchesTheStoredChecksum_ForAnUncorruptedMessage()
    {
        var request = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(1234, 1, RawPayload.FromText("Hello"));

        Assert.Equal(request.Checksum, request.ComputeChecksum());
    }

    // ---- IPacketPayload shape ----

    [Fact]
    public void PayloadType_IsIcmp()
    {
        Assert.Equal("ICMP", NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(1, 1).PayloadType);
    }

    [Fact]
    public void Length_IsHeaderPlusData()
    {
        var request = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(1, 1, RawPayload.OfSize(10));

        Assert.Equal(NetSim.Core.Icmp.IcmpMessage.HeaderSizeBytes + 10, request.Length);
    }

    [Fact]
    public void Length_ForAnErrorMessage_IncludesTheOriginalDatagramContribution()
    {
        var message = NetSim.Core.Icmp.IcmpMessage.CreateTimeExceeded(SampleOriginalDatagram());

        Assert.Equal(
            NetSim.Core.Icmp.IcmpMessage.HeaderSizeBytes + NetSim.Core.Icmp.IcmpOriginalDatagramInfo.SimulatedLengthBytes,
            message.Length);
    }

    [Fact]
    public void EncapsulatedPayload_IsTheDataPayload_NeverAnotherProtocolLayer()
    {
        var data = RawPayload.FromText("abc");
        var request = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(1, 1, data);

        Assert.Same(data, request.EncapsulatedPayload);
    }

    // ---- Validation ----

    [Fact]
    public void Validate_AWellFormedEchoRequest_IsValid()
    {
        var request = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(1, 1, RawPayload.FromText("abc"));

        Assert.True(request.Validate().IsValid);
    }

    [Fact]
    public void Validate_AWellFormedErrorMessage_IsValid()
    {
        var message = NetSim.Core.Icmp.IcmpMessage.CreateTimeExceeded(SampleOriginalDatagram());

        Assert.True(message.Validate().IsValid);
    }

    [Fact]
    public void ToString_DescribesTheMessage()
    {
        var request = NetSim.Core.Icmp.IcmpMessage.CreateEchoRequest(1234, 5, RawPayload.OfSize(4));

        Assert.Contains("Echo Request", request.ToString());
        Assert.Contains("id=1234", request.ToString());
        Assert.Contains("seq=5", request.ToString());
    }
}
