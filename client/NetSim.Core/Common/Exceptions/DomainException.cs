namespace NetSim.Core.Common.Exceptions;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The error thrown when a rule of the network model is broken -
// e.g. an empty device name, or plugging a cable into a port that is already in use.
// ": NetworkSimulatorException" = it is one of OUR errors
public class DomainException : NetworkSimulatorException
{
    // Purpose: Builds the error with a message.
    // Input:   message - which rule was broken.
    // Output:  none (a constructor). ": base(message)" = hand the text to NetworkSimulatorException
    public DomainException(string message)
        : base(message)
    {
    }

    // Purpose: Builds the error with a message and the earlier error that caused it.
    // Input:   message - which rule was broken; innerException - the original error.
    // Output:  none (a constructor).
    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
