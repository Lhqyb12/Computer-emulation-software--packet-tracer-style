namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Is a cable passing traffic right now.
public enum ConnectionState
{
    Down,   // not working
    Up,     // working
}
