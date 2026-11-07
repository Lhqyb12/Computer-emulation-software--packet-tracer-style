namespace NetSim.Application.Common;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Why an action did not work. An enum = a closed list of named values, so a misspelled
// kind does not compile (with plain text it would)
public enum OperationErrorType
{
    // The thing asked for does not exist (e.g. a project that was deleted)
    NotFound,

    // The action clashes with what already exists (e.g. a project name that is taken)
    Conflict,

    // What the user typed breaks a rule (e.g. an empty or too long name)
    ValidationError,

    // The action can't run right now (e.g. "Save" when no project is open)
    InvalidState,
}
