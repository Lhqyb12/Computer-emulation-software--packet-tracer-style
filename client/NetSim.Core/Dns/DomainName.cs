using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Dns;

/// <summary>
/// A fully-qualified DNS domain name (RFC 1035 section 3.1), modelled the same way as
/// <see cref="Networking.IPv4Address"/>/<see cref="Networking.MacAddress"/>: a single validated,
/// normalized internal representation so the rest of the domain passes a <see cref="DomainName"/>
/// around rather than a bare, unchecked string. Comparison is case-insensitive
/// (<c>Example.COM</c> and <c>example.com</c> are the same domain name) - the normalized form is
/// lower-case, which also means two differently-cased inputs never produce accidentally distinct
/// representations.
///
/// A name is a sequence of <see cref="Labels"/> (<c>www.example.com</c> -&gt; <c>www</c>,
/// <c>example</c>, <c>com</c>), most-specific label first. <see cref="Root"/> represents the DNS
/// root (zero labels) - callers write ordinary names like <c>example.com</c> without ever having to
/// spell out a trailing root label themselves; a trailing dot is accepted and simply stripped.
///
/// Deliberately does not implement every obscure DNS naming rule (punycode/IDNA, the 255-octet wire
/// length limit, reserved TLD lists, ...) - only structural validation that matters to the
/// simulator: 1-63 characters per label, letters/digits/hyphen, no leading/trailing hyphen, no empty
/// labels, and a reasonable overall length limit.
/// </summary>
public readonly struct DomainName : IEquatable<DomainName>, IComparable<DomainName>
{
    private const int MaxLabelLength = 63;
    private const int MaxNameLength = 253;

    private readonly string _normalized;
    private readonly string[] _labels;

    private DomainName(string normalized, string[] labels)
    {
        _normalized = normalized;
        _labels = labels;
    }

    /// <summary>The DNS root - zero labels. Also <c>default(DomainName)</c>.</summary>
    public static DomainName Root { get; } = new(string.Empty, []);

    /// <summary>The name's labels, most-specific (leftmost) first, already lower-cased. Empty for <see cref="Root"/>.</summary>
    public IReadOnlyList<string> Labels => _labels ?? [];

    /// <summary>True for <see cref="Root"/> - a name with no labels.</summary>
    public bool IsRoot => (_labels?.Length ?? 0) == 0;

    /// <summary>
    /// Parses a domain name such as <c>example.com</c> or <c>www.example.com.</c> (a trailing dot,
    /// if present, is stripped). Throws <see cref="DomainException"/> for anything structurally
    /// invalid: an empty label, a label over 63 characters, a label starting/ending with a hyphen, a
    /// character other than a letter/digit/hyphen, or a name over 253 characters.
    /// </summary>
    public static DomainName Parse(string? text) =>
        TryParse(text, out var result) ? result : throw new DomainException($"'{text}' is not a valid domain name.");

    /// <summary>Non-throwing counterpart of <see cref="Parse"/>.</summary>
    public static bool TryParse(string? text, out DomainName result)
    {
        result = Root;
        if (text is null)
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length == 0 || trimmed == ".")
        {
            result = Root;
            return true;
        }

        if (trimmed.EndsWith('.'))
        {
            trimmed = trimmed[..^1];
        }

        if (trimmed.Length == 0 || trimmed.Length > MaxNameLength)
        {
            return false;
        }

        var parts = trimmed.Split('.');
        var labels = new string[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var label = parts[i];
            if (label.Length == 0 || label.Length > MaxLabelLength || !IsValidLabel(label))
            {
                return false;
            }

            labels[i] = label.ToLowerInvariant();
        }

        result = new DomainName(string.Join('.', labels), labels);
        return true;
    }

    /// <summary>Builds a name directly from already-normalized labels (most-specific first). Each label is validated exactly like <see cref="Parse"/>.</summary>
    public static DomainName FromLabels(IEnumerable<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return Parse(string.Join('.', labels));
    }

    private static bool IsValidLabel(string label)
    {
        if (label[0] == '-' || label[^1] == '-')
        {
            return false;
        }

        foreach (var c in label)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The parent domain (<c>www.example.com</c> -&gt; <c>example.com</c>). <see cref="Root"/>'s parent is itself.</summary>
    public DomainName Parent
    {
        get
        {
            var labels = Labels;
            return labels.Count == 0 ? Root : FromLabels(labels.Skip(1));
        }
    }

    /// <summary>True when this name is a strict subdomain of <paramref name="other"/> (e.g. <c>www.example.com</c> is a subdomain of <c>example.com</c>, but not of itself).</summary>
    public bool IsSubdomainOf(DomainName other)
    {
        var mine = Labels;
        var theirs = other.Labels;
        if (mine.Count <= theirs.Count)
        {
            return false;
        }

        var offset = mine.Count - theirs.Count;
        for (var i = 0; i < theirs.Count; i++)
        {
            if (mine[offset + i] != theirs[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when this name equals <paramref name="other"/> or is a subdomain of it.</summary>
    public bool IsSubdomainOfOrEqualTo(DomainName other) => Equals(other) || IsSubdomainOf(other);

    public bool Equals(DomainName other) => Normalized == other.Normalized;

    public override bool Equals(object? obj) => obj is DomainName other && Equals(other);

    public override int GetHashCode() => Normalized.GetHashCode(StringComparison.Ordinal);

    public int CompareTo(DomainName other) => string.CompareOrdinal(Normalized, other.Normalized);

    public static bool operator ==(DomainName left, DomainName right) => left.Equals(right);

    public static bool operator !=(DomainName left, DomainName right) => !left.Equals(right);

    private string Normalized => _normalized ?? string.Empty;

    /// <summary>The canonical lower-case, no-trailing-dot form, e.g. <c>www.example.com</c>. <see cref="Root"/> renders as <c>.</c>.</summary>
    public override string ToString() => IsRoot ? "." : Normalized;
}
