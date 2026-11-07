using System;                                // Action - "a function passed in, to be called later"
using CommunityToolkit.Mvvm.ComponentModel;  // [ObservableProperty]
using CommunityToolkit.Mvvm.Input;           // [RelayCommand]
using NetSim.UI.Common;                      // ViewModelBase

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The "Rename Project" dialog (also used by "Save As" to ask for a name). It only collects the new
// name and checks empty / too long; the project service checks whether it is taken
public partial class RenameProjectDialogViewModel : ViewModelBase
{
    // The longest name allowed - the same number as in ProjectService
    private const int MaxNameLength = 100;

    // The two functions DialogService passed in
    private readonly Action<string> _onRename;   // "Rename" - gets the new name
    private readonly Action _onCancel;           // "Cancel"

    // The name text box. Generates the public "Name" property. Filled by the constructor
    [ObservableProperty]
    private string _name;

    // The red error line (null = hidden). Generates "ValidationError"
    [ObservableProperty]
    private string? _validationError;

    // Purpose: Builds the dialog with the current name already in the text box.
    // Input:   currentName - the name to show; onRename, onCancel - what to call on close (from DialogService).
    // Output:  none (a constructor).
    public RenameProjectDialogViewModel(string currentName, Action<string> onRename, Action onCancel)
    {
        _name = currentName;    // the box starts with the current name (written to the field - nobody is bound yet)
        _onRename = onRename;   // keep the "Rename" function
        _onCancel = onCancel;   // keep the "Cancel" function
    }

    // Purpose: The "Rename" button - checks the name and closes the dialog with it.
    // Input:   nothing (reads Name).
    // Output:  nothing. If the name is not valid, shows ValidationError and the dialog stays open.
    [RelayCommand]
    private void Rename()
    {
        // The name without spaces at both ends
        var trimmedName = Name.Trim();

        // Empty name - show the error and stay open
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            ValidationError = "Project name cannot be empty.";
            return;   // stop here - nothing more to do
        }

        // Too long - show the error and stay open
        if (trimmedName.Length > MaxNameLength)
        {
            ValidationError = $"Project name cannot exceed {MaxNameLength} characters.";
            return;   // stop here - nothing more to do
        }

        // The name is fine - hide any old error
        ValidationError = null;

        // Call the "Rename" function with the new name - DialogService closes the dialog
        _onRename(trimmedName);
    }

    // Purpose: The "Cancel" button - closes the dialog without a name.
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void Cancel() => _onCancel();
}
