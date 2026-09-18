using NetSim.Core.Common.Exceptions;
using NetSim.Core.Dns;

namespace NetSim.Core.Tests.Dns;

public class DomainNameTests
{
    [Theory]
    [InlineData("example.com")]
    [InlineData("www.example.com")]
    [InlineData("mail.example.com")]
    [InlineData("server.lab.local")]
    [InlineData("a.b")]
    [InlineData("x-y.example.com")]
    public void Parse_ValidDomain_Succeeds(string text)
    {
        var name = DomainName.Parse(text);

        Assert.Equal(text, name.ToString());
        Assert.False(name.IsRoot);
    }

    [Theory]
    [InlineData("-example.com")]
    [InlineData("example-.com")]
    [InlineData("exa mple.com")]
    [InlineData("exa_mple.com")]
    [InlineData("example..com")]
    public void Parse_InvalidDomain_Throws(string text)
    {
        Assert.Throws<DomainException>(() => DomainName.Parse(text));
        Assert.False(DomainName.TryParse(text, out _));
    }

    [Fact]
    public void Parse_TrailingDot_IsStripped()
    {
        var name = DomainName.Parse("www.example.com.");

        Assert.Equal("www.example.com", name.ToString());
    }

    [Fact]
    public void Parse_NullText_TryParseFails()
    {
        Assert.False(DomainName.TryParse(null, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    public void Parse_EmptyOrDot_ProducesRoot(string text)
    {
        var name = DomainName.Parse(text);

        Assert.True(name.IsRoot);
        Assert.Empty(name.Labels);
        Assert.Equal(".", name.ToString());
    }

    [Fact]
    public void Root_IsTheDefaultValue()
    {
        Assert.Equal(default, DomainName.Root);
        Assert.True(default(DomainName).IsRoot);
    }

    [Fact]
    public void Equality_IsCaseInsensitive()
    {
        var a = DomainName.Parse("Example.COM");
        var b = DomainName.Parse("example.com");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_IsCaseInsensitive_ForWwwPrefixedNames()
    {
        var a = DomainName.Parse("WWW.Example.com");
        var b = DomainName.Parse("www.example.com");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Labels_AreParsedMostSpecificFirst()
    {
        var name = DomainName.Parse("www.example.com");

        Assert.Equal(["www", "example", "com"], name.Labels);
    }

    [Fact]
    public void Labels_AreNormalizedToLowercase()
    {
        var name = DomainName.Parse("WWW.EXAMPLE.COM");

        Assert.Equal(["www", "example", "com"], name.Labels);
    }

    [Fact]
    public void Parent_ReturnsTheNextLevelUp()
    {
        var name = DomainName.Parse("www.example.com");

        Assert.Equal(DomainName.Parse("example.com"), name.Parent);
        Assert.Equal(DomainName.Parse("com"), name.Parent.Parent);
    }

    [Fact]
    public void Parent_OfRoot_IsRoot()
    {
        Assert.Equal(DomainName.Root, DomainName.Root.Parent);
    }

    [Fact]
    public void IsSubdomainOf_DetectsHierarchy()
    {
        var www = DomainName.Parse("www.example.com");
        var example = DomainName.Parse("example.com");
        var other = DomainName.Parse("example.net");

        Assert.True(www.IsSubdomainOf(example));
        Assert.False(example.IsSubdomainOf(www));
        Assert.False(www.IsSubdomainOf(other));
        Assert.False(example.IsSubdomainOf(example));
        Assert.True(example.IsSubdomainOfOrEqualTo(example));
    }

    [Fact]
    public void FromLabels_RoundTripsWithParse()
    {
        var fromLabels = DomainName.FromLabels(["www", "example", "com"]);
        var parsed = DomainName.Parse("www.example.com");

        Assert.Equal(parsed, fromLabels);
    }

    [Fact]
    public void DifferentDomains_AreNotEqual()
    {
        Assert.NotEqual(DomainName.Parse("example.com"), DomainName.Parse("example.net"));
    }
}
