using NetSim.Core.Common.Exceptions;   // DomainException

namespace NetSim.Core.Common;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Shared checks for values that come into the network model, so every device / cable / network
// checks them the same way instead of each one writing its own copy.
// "internal" = only code inside NetSim.Core can use it; "static" = no objects, just call Guard.Something(...)
internal static class Guard
{
    // Purpose: Checks that a text value (e.g. a device name) is not null, empty, or only spaces.
    // Input:   value - the text to check; paramName - its name, used in the error message.
    // Output:  the same text without spaces at both ends. Throws DomainException when it is invalid.
    public static string AgainstNullOrWhiteSpace(string value, string paramName)
    {
        // null, "" or "   " - not a valid value
        if (string.IsNullOrWhiteSpace(value))
        {
            // Stop here and say which value was bad
            throw new DomainException($"'{paramName}' cannot be null, empty, or whitespace.");
        }

        // Valid - return it cleaned
        return value.Trim();
    }
}
