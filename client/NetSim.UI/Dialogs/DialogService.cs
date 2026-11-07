using System.Threading.Tasks;   // Task, TaskCompletionSource
using NetSim.UI.ViewModels;     // the view models of the six dialogs

namespace NetSim.UI.Dialogs;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Opens the pop-up windows (dialogs) and gives the caller the user's answer.
// How waiting for a click works: TaskCompletionSource ("tcs") makes a Task that does no work by
// itself - it just waits until someone calls tcs.TrySetResult(answer). The caller awaits that Task;
// the dialog's buttons call back here, and Complete() sets the answer, so the caller continues
public sealed class DialogService : IDialogService
{
    // Holds the dialog that is open right now; the pop-up layer on the screen shows whatever is in it
    private readonly IDialogHost _dialogHost;

    // Purpose: Builds the service.
    // Input:   dialogHost - passed in by the DI container (the same host the Shell shows).
    // Output:  none (a constructor).
    public DialogService(IDialogHost dialogHost)
    {
        _dialogHost = dialogHost;   // keep the host
    }

    // Purpose: Shows the "New Project" dialog and waits for the user to close it.
    // Input:   nothing.
    // Output:  a Task that finishes with the name + description, or null if the user cancelled.
    public Task<NewProjectDialogResult?> ShowNewProjectAsync()
    {
        // A Task that will finish only when Complete() gives it an answer
        var tcs = new TaskCompletionSource<NewProjectDialogResult?>();

        // The dialog gets two functions to call: one for "Create" (with the result), one for "Cancel"
        var dialog = new NewProjectDialogViewModel(
            onCreate: result => Complete(tcs, result),   // Create -> answer = what the user typed
            onCancel: () => Complete(tcs, null));        // Cancel -> answer = null

        // Put the dialog in the host - the pop-up layer appears on the screen
        _dialogHost.ActiveDialog = dialog;

        // Hand the waiting Task to the caller
        return tcs.Task;
    }

    // Purpose: Shows the "Rename Project" dialog, filled with the current name, and waits.
    // Input:   currentName - the name to show in the text box at the start.
    // Output:  a Task that finishes with the new name, or null if the user cancelled.
    public Task<string?> ShowRenameProjectAsync(string currentName)
    {
        // A Task that waits for an answer
        var tcs = new TaskCompletionSource<string?>();

        // The dialog, with the current name and the two functions to call
        var dialog = new RenameProjectDialogViewModel(
            currentName,                                 // shown in the text box
            onRename: name => Complete(tcs, name),       // Rename -> answer = the new name
            onCancel: () => Complete(tcs, null));        // Cancel -> answer = null

        // Show it
        _dialogHost.ActiveDialog = dialog;

        // Hand the waiting Task to the caller
        return tcs.Task;
    }

    // Purpose: Shows a general "are you sure?" dialog and waits.
    // Input:   title, message - the texts; confirmText, cancelText - the button texts (have defaults);
    //          isDestructive - true = paint the confirm button red (have default false).
    // Output:  a Task that finishes with true if the user confirmed, false if cancelled.
    // "= ..." after a parameter = its default value; the caller may leave it out
    public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel", bool isDestructive = false)
    {
        // A Task that waits for true / false
        var tcs = new TaskCompletionSource<bool>();

        // The dialog, with all the texts and the two functions to call
        var dialog = new ConfirmationDialogViewModel(
            title,                                       // the title
            message,                                     // the message
            confirmText,                                 // the confirm button's text
            cancelText,                                  // the cancel button's text
            isDestructive,                               // red confirm button or not
            onConfirm: () => Complete(tcs, true),        // Confirm -> answer = true
            onCancel: () => Complete(tcs, false));       // Cancel  -> answer = false

        // Show it
        _dialogHost.ActiveDialog = dialog;

        // Hand the waiting Task to the caller
        return tcs.Task;
    }

    // Purpose: Shows the "Unsaved Changes" dialog (Save / Don't Save / Cancel) and waits.
    // Input:   projectName - shown inside the message.
    // Output:  a Task that finishes with the user's choice.
    public Task<UnsavedChangesDecision> ShowUnsavedChangesAsync(string projectName)
    {
        // A Task that waits for one of the three choices
        var tcs = new TaskCompletionSource<UnsavedChangesDecision>();

        // The dialog: all three buttons call the same function, each with its own choice
        var dialog = new UnsavedChangesDialogViewModel(
            projectName,
            onDecision: decision => Complete(tcs, decision));

        // Show it
        _dialogHost.ActiveDialog = dialog;

        // Hand the waiting Task to the caller
        return tcs.Task;
    }

    // Purpose: Shows the "Create a class" dialog and waits for the user to close it.
    // Input:   nothing.
    // Output:  a Task that finishes with the class name, or null if the user cancelled.
    // The same pattern as ShowRenameProjectAsync: the TaskCompletionSource is the "promise" that
    // the caller awaits, and the dialog's buttons complete it
    public Task<string?> ShowCreateClassroomAsync()
    {
        // A Task that waits for an answer
        var tcs = new TaskCompletionSource<string?>();

        // The dialog and its two functions
        var dialog = new CreateClassroomDialogViewModel(
            onCreate: name => Complete(tcs, name),       // Create -> answer = the class name
            onCancel: () => Complete(tcs, null));        // Cancel -> answer = null

        // Show it
        _dialogHost.ActiveDialog = dialog;

        // Hand the waiting Task to the caller
        return tcs.Task;
    }

    // Purpose: Shows the "Join a class" dialog and waits for the user to close it.
    // Input:   nothing.
    // Output:  a Task that finishes with the join code, or null if the user cancelled.
    public Task<string?> ShowJoinClassroomAsync()
    {
        // A Task that waits for an answer
        var tcs = new TaskCompletionSource<string?>();

        // The dialog and its two functions
        var dialog = new JoinClassroomDialogViewModel(
            onJoin: code => Complete(tcs, code),         // Join   -> answer = the code
            onCancel: () => Complete(tcs, null));        // Cancel -> answer = null

        // Show it
        _dialogHost.ActiveDialog = dialog;

        // Hand the waiting Task to the caller
        return tcs.Task;
    }

    // Purpose: Closes the open dialog and gives the waiting caller its answer.
    // Input:   tcs - the waiting Task's source; result - the answer.
    // Output:  nothing.
    // "<T>" = works for any type of answer (text, true/false, a choice...)
    private void Complete<T>(TaskCompletionSource<T> tcs, T result)
    {
        // Remove the dialog from the host first - the pop-up layer disappears
        _dialogHost.ActiveDialog = null;

        // Give the answer; the caller's "await" now continues. "Try" = no error if it was already given
        tcs.TrySetResult(result);
    }
}
