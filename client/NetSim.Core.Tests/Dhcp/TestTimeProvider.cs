namespace NetSim.Core.Tests.Dhcp;

/// <summary>Manually-advanced <see cref="TimeProvider"/> for deterministic DHCP lease-timing tests - mirrors <c>Core.Tests.Arp.TestTimeProvider</c>.</summary>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public TestTimeProvider(DateTimeOffset start) => _now = start;

    public static TestTimeProvider StartingAtEpoch() => new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
