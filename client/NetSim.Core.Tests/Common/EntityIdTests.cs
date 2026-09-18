using NetSim.Core.Common;

namespace NetSim.Core.Tests.Common;

public class EntityIdTests
{
    [Fact]
    public void New_ProducesUniqueIds()
    {
        var first = EntityId.New();
        var second = EntityId.New();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var id = EntityId.New();

        var copy = id;

        Assert.Equal(id, copy);
        Assert.True(id == copy);
    }

    [Fact]
    public void FromValue_ReconstructsAnIdEqualToTheOneItWasBuiltFrom()
    {
        var original = EntityId.New();

        var reconstructed = EntityId.FromValue(original.Value);

        Assert.Equal(original, reconstructed);
    }
}
