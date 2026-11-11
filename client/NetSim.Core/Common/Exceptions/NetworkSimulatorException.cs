namespace NetSim.Core.Common.Exceptions;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The base of every error that OUR code throws on purpose (not errors of .NET itself).
// "abstract" = nobody throws this one directly - only the more specific errors built on it.
// ": Exception" = it is built on .NET's normal Exception, so try/catch works with it as usual
public abstract class NetworkSimulatorException : Exception
{
    // Purpose: Builds the error with a message.
    // Input:   message - the text that explains what went wrong.
    // Output:  none (a constructor).
    // "protected" = only the errors built on this class can call it; ": base(message)" = hand the text to Exception
    protected NetworkSimulatorException(string message)
        : base(message)
    {
    }

    // Purpose: Builds the error with a message and the earlier error that caused it.
    // Input:   message - the text; innerException - the original error, kept for debugging.
    // Output:  none (a constructor).
    protected NetworkSimulatorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
