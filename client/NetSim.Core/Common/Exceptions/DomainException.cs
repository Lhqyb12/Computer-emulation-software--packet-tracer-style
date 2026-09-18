namespace NetSim.Core.Common.Exceptions;

/// <summary>
/// Raised when a domain rule (a rule about the network model itself, independent of
/// any application workflow or external system) is violated.
/// </summary>
public class DomainException : NetworkSimulatorException
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
