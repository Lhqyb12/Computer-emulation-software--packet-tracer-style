using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Common;

/// <summary>
/// Shared domain-validation helpers, used to keep entity constructors and mutators
/// consistent about what counts as an invalid value.
/// </summary>
// "internal" = only code inside this same project (NetSim.Core) can see
// this class at all. Other projects (UI, Application, ...) don't even know it exists.
internal static class Guard
{
    // Checks that a name/text value isn't null, empty, or just spaces.
    // Every entity (device, network, packet, ...) calls this once, instead of
    // each one writing its own copy of the same check.
    public static string AgainstNullOrWhiteSpace(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            // Invalid input -> stop everything and report which parameter was bad.
            throw new DomainException($"'{paramName}' cannot be null, empty, or whitespace.");
        }

        // Valid input -> trim leading/trailing spaces and hand back the clean value.
        return value.Trim();
    }
}
