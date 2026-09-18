using System.Text;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Transport;
using NetSim.Core.Udp;

namespace NetSim.Core.Tests.Udp;

public class UdpDatagramTests
{
    private static readonly IPv4Address SourceIp = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address DestinationIp = IPv4Address.Parse("192.168.1.20");

    [Fact]
    public void Create_SetsSourceAndDestinationPorts()
    {
        var datagram = UdpDatagram.Create(SourceIp, DestinationIp, Port.Create(50000), Port.Create(5000));

        Assert.Equal(50000, datagram.SourcePort.Value);
        Assert.Equal(5000, datagram.DestinationPort.Value);
    }

    [Fact]
    public void Create_WithoutPayload_UsesAnEmptyPayload()
    {
        var datagram = UdpDatagram.Create(SourceIp, DestinationIp, Port.Create(1), Port.Create(2));

        Assert.Equal(0, datagram.Payload.Length);
        Assert.Equal(UdpDatagram.HeaderSizeBytes, datagram.Length);
    }

    [Fact]
    public void Length_IsHeaderPlusPayload()
    {
        var datagram = UdpDatagram.Create(SourceIp, DestinationIp, Port.Create(1), Port.Create(2), RawPayload.FromText("Hello UDP"));

        Assert.Equal(UdpDatagram.HeaderSizeBytes + 9, datagram.Length);
    }

    [Fact]
    public void Checksum_IsComputedAtCreation_AndValidatesAgainstItsAddresses()
    {
        var datagram = UdpDatagram.Create(SourceIp, DestinationIp, Port.Create(50000), Port.Create(5000), RawPayload.FromText("Hello"));

        Assert.NotEqual(0, datagram.Checksum);
        Assert.True(datagram.HasValidChecksum(SourceIp, DestinationIp));
    }

    [Fact]
    public void Checksum_ChangesWithPayload()
    {
        var a = UdpDatagram.Create(SourceIp, DestinationIp, Port.Create(1), Port.Create(2), RawPayload.FromText("Hello"));
        var b = UdpDatagram.Create(SourceIp, DestinationIp, Port.Create(1), Port.Create(2), RawPayload.FromText("World"));

        Assert.NotEqual(a.Checksum, b.Checksum);
    }

    [Fact]
    public void Checksum_ChangesWithAddresses()
    {
        var datagram = UdpDatagram.Create(SourceIp, DestinationIp, Port.Create(1), Port.Create(2), RawPayload.FromText("Hello"));

        var elsewhere = IPv4Address.Parse("10.0.0.5");
        Assert.False(datagram.HasValidChecksum(elsewhere, DestinationIp));
    }

    [Fact]
    public void WithChecksum_ProducesAnInvalidChecksum_WithoutChangingOtherFields()
    {
        var datagram = UdpDatagram.Create(SourceIp, DestinationIp, Port.Create(1), Port.Create(2), RawPayload.FromText("Hello"));

        var corrupted = datagram.WithChecksum(unchecked((ushort)(datagram.Checksum + 1)));

        Assert.False(corrupted.HasValidChecksum(SourceIp, DestinationIp));
        Assert.Equal(datagram.SourcePort, corrupted.SourcePort);
        Assert.Equal(datagram.DestinationPort, corrupted.DestinationPort);
        Assert.Equal(Encoding.UTF8.GetString(((RawPayload)datagram.Payload).Data.Span), Encoding.UTF8.GetString(((RawPayload)corrupted.Payload).Data.Span));
    }

    [Fact]
    public void PayloadType_IsUdp_AndEncapsulatedPayloadIsTheApplicationData()
    {
        var payload = RawPayload.FromText("Hello UDP");
        var datagram = UdpDatagram.Create(SourceIp, DestinationIp, Port.Create(1), Port.Create(2), payload);

        Assert.Equal("UDP", datagram.PayloadType);
        Assert.Same(payload, datagram.EncapsulatedPayload);
    }

    [Fact]
    public void Validate_IsValidForAnyWellFormedDatagram()
    {
        var datagram = UdpDatagram.Create(SourceIp, DestinationIp, Port.Create(1), Port.Create(2), RawPayload.FromText("Hello"));

        Assert.True(datagram.Validate().IsValid);
    }
}
