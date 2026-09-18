using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Vlans;

namespace NetSim.Core.Tests.Ethernet;

/// <summary>Phase 26 - the optional 802.1Q VLAN tag on <see cref="EthernetFrame"/>.</summary>
public class EthernetFrameVlanTests
{
    private static readonly MacAddress Src = MacAddress.Parse("02:00:00:00:00:01");
    private static readonly MacAddress Dst = MacAddress.Parse("02:00:00:00:00:02");

    private static EthernetFrame Frame() =>
        EthernetFrame.Create(Src, Dst, EtherType.IPv4, RawPayload.OfSize(64));

    [Fact]
    public void ANewlyCreatedFrame_IsUntagged()
    {
        var frame = Frame();

        Assert.False(frame.IsVlanTagged);
        Assert.Null(frame.VlanTag);
        Assert.Null(frame.VlanId);
    }

    [Fact]
    public void Tagged_ReturnsACopyCarryingTheTag_LeavingTheOriginalUntouched()
    {
        var frame = Frame();

        var tagged = frame.Tagged(VlanTag.For(new VlanId(10)));

        Assert.True(tagged.IsVlanTagged);
        Assert.Equal(new VlanId(10), tagged.VlanId);
        Assert.Equal(Src, tagged.SourceMac);
        Assert.Equal(Dst, tagged.DestinationMac);
        Assert.Same(frame.Payload, tagged.Payload);
        Assert.False(frame.IsVlanTagged); // original unchanged - immutable
    }

    [Fact]
    public void Untagged_StripsTheTag_AndIsANoOpWhenAlreadyUntagged()
    {
        var frame = Frame();
        var tagged = frame.Tagged(VlanTag.For(new VlanId(10)));

        var stripped = tagged.Untagged();
        Assert.False(stripped.IsVlanTagged);

        Assert.Same(frame, frame.Untagged());
    }

    [Fact]
    public void Length_GrowsByFourBytesWhenTagged()
    {
        var frame = Frame();
        var tagged = frame.Tagged(VlanTag.For(new VlanId(10)));

        Assert.Equal(frame.Length + EthernetFrame.VlanTagSizeBytes, tagged.Length);
    }

    [Fact]
    public void ATaggedFrame_IsStillStructurallyValid()
    {
        var tagged = Frame().Tagged(new VlanTag(new VlanId(10), priorityCodePoint: 5, dropEligible: true));

        Assert.True(tagged.Validate().IsValid);
        Assert.Equal(5, tagged.VlanTag!.Value.PriorityCodePoint);
        Assert.True(tagged.VlanTag.Value.DropEligible);
    }

    [Fact]
    public void Create_CanTakeAVlanTagDirectly()
    {
        var frame = EthernetFrame.Create(Src, Dst, EtherType.Arp, RawPayload.OfSize(28), VlanTag.For(new VlanId(42)));

        Assert.Equal(new VlanId(42), frame.VlanId);
    }
}
