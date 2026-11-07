using System.Threading.Tasks;   // Task - "a job that will finish later"

namespace NetSim.UI.Dialogs;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Everything a screen may ask from the pop-up windows. Dialogs only collect what the user types or
// chooses, and do simple checks (empty / too long). The real work (creating, renaming...) is done
// afterwards by the screen, through the services
public interface IDialogService
{
    // Purpose: Shows the "New Project" dialog.
    // Input:   nothing.
    // Output:  a Task with the name + description, or null if the user cancelled.
    Task<NewProjectDialogResult?> ShowNewProjectAsync();

    // Purpose: Shows the "Rename Project" dialog, filled with the current name.
    // Input:   currentName - the name shown at the start.
    // Output:  a Task with the new name, or null if the user cancelled.
    Task<string?> ShowRenameProjectAsync(string currentName);

    // Purpose: Shows a general "are you sure?" dialog.
    // Input:   title, message; confirmText, cancelText and isDestructive are optional (have defaults).
    // Output:  a Task with true if the user confirmed, false if cancelled.
    Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel", bool isDestructive = false);

    // Purpose: Shows the three-way "unsaved changes" dialog (Save / Don't Save / Cancel).
    // Input:   projectName - shown in the message.
    // Output:  a Task with the user's choice.
    Task<UnsavedChangesDecision> ShowUnsavedChangesAsync(string projectName);

    // Purpose: Shows the "Create a class" dialog.
    // Input:   nothing.
    // Output:  a Task with the class name, or null if the user cancelled.
    Task<string?> ShowCreateClassroomAsync();

    // Purpose: Shows the "Join a class" dialog.
    // Input:   nothing.
    // Output:  a Task with the typed join code, or null if the user cancelled.
    Task<string?> ShowJoinClassroomAsync();
}

// What the "New Project" dialog returns: the name, and the optional description.
// A record = a small data-only class; the compiler writes the constructor and the properties
public sealed record NewProjectDialogResult(string Name, string? Description);

// The three choices of the "Unsaved Changes" dialog. An enum = a closed list of named values
public enum UnsavedChangesDecision
{
    Save,       // save, then continue
    DontSave,   // continue without saving (the changes are lost)
    Cancel,     // don't continue at all
}
