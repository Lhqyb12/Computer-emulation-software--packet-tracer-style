namespace NetSim.Core.Common.Exceptions;

/// <summary>
/// Base type for every exception raised intentionally by NetworkSimulator code,
/// as opposed to exceptions raised by the runtime or third-party libraries.
/// </summary>
public abstract class NetworkSimulatorException : Exception
{
    protected NetworkSimulatorException(string message)
        : base(message)
    {
    }

    protected NetworkSimulatorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
