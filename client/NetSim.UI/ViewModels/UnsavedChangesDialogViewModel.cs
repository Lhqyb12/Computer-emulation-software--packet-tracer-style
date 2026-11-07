using System;                        // Action<T> - "a function passed in, to be called later"
using CommunityToolkit.Mvvm.Input;   // [RelayCommand]
using NetSim.UI.Common;              // ViewModelBase
using NetSim.UI.Dialogs;             // UnsavedChangesDecision

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The "Unsaved Changes" dialog: Save / Don't Save / Cancel. Used before closing a project
public partial class UnsavedChangesDialogViewModel : ViewModelBase
{
    // The one function DialogService passed in; all three buttons call it, each with its own choice
    private readonly Action<UnsavedChangesDecision> _onDecision;

    // Purpose: Builds the dialog.
    // Input:   projectName - shown in the message; onDecision - what to call with the user's choice.
    // Output:  none (a constructor).
    public UnsavedChangesDialogViewModel(string projectName, Action<UnsavedChangesDecision> onDecision)
    {
        ProjectName = projectName;   // keep the name for the message
        _onDecision = onDecision;    // keep the function
    }

    // The project's name. Read-only
    public string ProjectName { get; }

    // The message the view shows, built from the name
    public string Message => $"'{ProjectName}' has unsaved changes. Do you want to save them before continuing?";

    // Purpose: The "Save" button.
    // Input:   nothing.
    // Output:  nothing (answers "Save").
    [RelayCommand]
    private void Save() => _onDecision(UnsavedChangesDecision.Save);

    // Purpose: The "Don't Save" button.
    // Input:   nothing.
    // Output:  nothing (answers "DontSave").
    [RelayCommand]
    private void DontSave() => _onDecision(UnsavedChangesDecision.DontSave);

    // Purpose: The "Cancel" button.
    // Input:   nothing.
    // Output:  nothing (answers "Cancel").
    [RelayCommand]
    private void Cancel() => _onDecision(UnsavedChangesDecision.Cancel);
}
