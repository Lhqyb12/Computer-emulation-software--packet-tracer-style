using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Transport;

/// <summary>
/// A transport-layer port number: an immutable value object wrapping the valid 0-65535 range (brief
/// section 5). Modelled the same way as <see cref="Networking.IPv4Address"/> / <see cref="Networking.MacAddress"/>
/// - the single place a port is validated, so the rest of the domain passes a <see cref="Port"/>
/// around rather than a bare, unchecked <see cref="int"/>.
/// </summary>
public readonly record struct Port : IComparable<Port>
{
    public const int MinValue = 0;

    public const int MaxValue = 65535;

    /// <summary>Creates a port directly from a <see cref="ushort"/> - always in range, so this never throws.</summary>
    public Port(ushort value) => Value = value;

    /// <summary>The port number, 0-65535.</summary>
    public ushort Value { get; }

    /// <summary>Port 0 - conventionally "not set" / a wildcard, never a real listening port.</summary>
    public static Port Zero { get; } = new(0);

    /// <summary>
    /// Validates and creates a port from a plain <see cref="int"/> (e.g. user input or a config
    /// value that is not already known to be in range). Throws <see cref="DomainException"/> for a
    /// negative value or one above <see cref="MaxValue"/>.
    /// </summary>
    public static Port Create(int value)
    {
        if (value < MinValue || value > MaxValue)
        {
            throw new DomainException($"Port {value} is outside the valid range {MinValue}-{MaxValue}.");
        }

        return new Port((ushort)value);
    }

    /// <summary>Non-throwing counterpart of <see cref="Create"/>.</summary>
    public static bool TryCreate(int value, out Port port)
    {
        if (value < MinValue || value > MaxValue)
        {
            port = default;
            return false;
        }

        port = new Port((ushort)value);
        return true;
    }

    /// <summary>0-1023, 1024-49151 or 49152-65535 - see <see cref="PortCategory"/>.</summary>
    public PortCategory Category => Value switch
    {
        <= 1023 => PortCategory.WellKnown,
        <= 49151 => PortCategory.Registered,
        _ => PortCategory.DynamicOrPrivate,
    };

    public bool IsWellKnown => Category == PortCategory.WellKnown;

    public bool IsRegistered => Category == PortCategory.Registered;

    public bool IsDynamicOrPrivate => Category == PortCategory.DynamicOrPrivate;

    public int CompareTo(Port other) => Value.CompareTo(other.Value);

    public static implicit operator Port(ushort value) => new(value);

    public static explicit operator ushort(Port port) => port.Value;

    public static bool operator <(Port left, Port right) => left.Value < right.Value;

    public static bool operator >(Port left, Port right) => left.Value > right.Value;

    public static bool operator <=(Port left, Port right) => left.Value <= right.Value;

    public static bool operator >=(Port left, Port right) => left.Value >= right.Value;

    public override string ToString() => Value.ToString();
}
