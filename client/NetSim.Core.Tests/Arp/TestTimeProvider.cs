namespace NetSim.Core.Tests.Arp;

/// <summary>
/// A minimal manually-advanced <see cref="TimeProvider"/> for exercising ARP cache expiration
/// deterministically - no dependency on <c>Microsoft.Extensions.TimeProvider.Testing</c>.
/// </summary>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public TestTimeProvider(DateTimeOffset start) => _now = start;

    public static TestTimeProvider StartingAtEpoch() => new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
