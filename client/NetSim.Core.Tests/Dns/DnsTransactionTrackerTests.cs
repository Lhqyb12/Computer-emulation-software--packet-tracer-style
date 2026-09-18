using NetSim.Core.Dns;

namespace NetSim.Core.Tests.Dns;

public class DnsTransactionTrackerTests
{
    [Fact]
    public void Begin_MarksTheIdAsOutstanding()
    {
        var tracker = new DnsTransactionTracker();

        var id = tracker.Begin();

        Assert.True(tracker.IsOutstanding(id));
    }

    [Fact]
    public void TryComplete_OutstandingId_SucceedsAndRemovesIt()
    {
        var tracker = new DnsTransactionTracker();
        var id = tracker.Begin();

        Assert.True(tracker.TryComplete(id));
        Assert.False(tracker.IsOutstanding(id));
    }

    [Fact]
    public void TryComplete_UnknownId_Fails()
    {
        var tracker = new DnsTransactionTracker();

        Assert.False(tracker.TryComplete(9999));
    }

    [Fact]
    public void TryComplete_TwiceForTheSameId_SucceedsOnlyOnce()
    {
        var tracker = new DnsTransactionTracker();
        var id = tracker.Begin();

        Assert.True(tracker.TryComplete(id));
        Assert.False(tracker.TryComplete(id));
    }

    [Fact]
    public void Begin_NeverReturnsAnIdThatIsAlreadyOutstanding()
    {
        var tracker = new DnsTransactionTracker();
        var seen = new HashSet<ushort>();

        for (var i = 0; i < 500; i++)
        {
            Assert.True(seen.Add(tracker.Begin()));
        }
    }
}
