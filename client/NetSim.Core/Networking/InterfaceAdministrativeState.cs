namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Did the user turn this port on or off (later in the CLI: "no shutdown" / "shutdown").
// This is the user's choice - not whether a cable actually works (that is InterfaceOperationalState).
public enum InterfaceAdministrativeState
{
    Enabled,    // turned on by the user
    Disabled,   // turned off by the user - never works, even with a cable
}
