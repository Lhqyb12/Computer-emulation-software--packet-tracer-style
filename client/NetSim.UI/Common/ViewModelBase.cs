using System;                                // Func<T>, Exception, OperationCanceledException
using System.Threading.Tasks;                // Task - "a job that will finish later"
using CommunityToolkit.Mvvm.ComponentModel;  // ObservableObject, [ObservableProperty]

namespace NetSim.UI.Common;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The base class of every screen (view model) in the workspace. Every screen inherits from it, so
// every screen gets, without writing it again:
//   - IsBusy:       true while a command is running (the screen can show a spinner / disable buttons)
//   - ErrorMessage: the text of the last error, shown in the red box (null = no error)
//   - ExecuteAsync: runs a command safely (see below)
// "abstract" = you can't create a ViewModelBase by itself, only a class that inherits from it.
// ": ObservableObject" = inherits the ability to tell the screen "this property changed"
public abstract partial class ViewModelBase : ObservableObject
{
    // true while a command is running. [ObservableProperty] writes a public "IsBusy" property for this field
    [ObservableProperty]
    private bool _isBusy;

    // The error to show on the screen, or null. Generates a public "ErrorMessage" property
    [ObservableProperty]
    private string? _errorMessage;

    // Purpose: Runs one piece of work (usually a button's command) safely.
    // Input:   operation - the work to run: a function that returns a Task (so it may wait inside).
    // Output:  a Task that finishes when the work is done. It never throws: any error becomes ErrorMessage.
    // It does three things around the work, so no command has to write them itself:
    //   1. Refuses to start while another command is running (no double clicks running twice).
    //   2. Clears the old error and sets IsBusy while the work runs.
    //   3. Catches any exception and puts its message into ErrorMessage instead of crashing the app.
    // "protected" = only this class and the classes that inherit from it can call it
    protected async Task ExecuteAsync(Func<Task> operation)
    {
        // Already busy with another command - ignore this one
        if (IsBusy)
        {
            return;   // stop here - nothing more to do
        }

        // "try" = run this; if anything inside throws, jump to the matching "catch"
        try
        {
            IsBusy = true;          // the screen is busy now
            ErrorMessage = null;    // remove the error of the previous attempt

            // Run the work and wait for it. ConfigureAwait(true) = continue on the screen's thread
            // afterwards, because only that thread may change what is on the screen
            await operation().ConfigureAwait(true);
        }
        // The work was cancelled on purpose - that is not an error, so nothing is shown
        catch (OperationCanceledException)
        {
        }
        // Any other error (e.g. "Could not reach the server") - show its message in the red box
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        // "finally" always runs at the end - after success, after cancel, and after an error
        finally
        {
            IsBusy = false;         // not busy any more
        }
    }
}
