using System;                                // Action - "a function passed in, to be called later"
using CommunityToolkit.Mvvm.ComponentModel;  // [ObservableProperty]
using CommunityToolkit.Mvvm.Input;           // [RelayCommand]
using NetSim.UI.Common;                      // ViewModelBase
using NetSim.UI.Dialogs;                     // NewProjectDialogResult

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The "New Project" dialog. It only collects the name and description, and checks what can be
// checked right here (empty / too long). Whether the name is already taken is checked by the
// project service after the dialog closes
public partial class NewProjectDialogViewModel : ViewModelBase
{
    // The longest name allowed - the same number as in ProjectService
    private const int MaxNameLength = 100;

    // The two functions DialogService passed in: what to do on "Create" and on "Cancel"
    private readonly Action<NewProjectDialogResult> _onCreate;   // gets the result
    private readonly Action _onCancel;                           // gets nothing

    // The name text box. [ObservableProperty] writes the public "Name" property the box is bound to
    [ObservableProperty]
    private string _name = string.Empty;

    // The description text box. Generates the public "Description" property
    [ObservableProperty]
    private string _description = string.Empty;

    // The red error line under the boxes (null = hidden). Generates "ValidationError"
    [ObservableProperty]
    private string? _validationError;

    // Purpose: Builds the dialog.
    // Input:   onCreate, onCancel - what to call when the user closes it (passed in by DialogService).
    // Output:  none (a constructor).
    public NewProjectDialogViewModel(Action<NewProjectDialogResult> onCreate, Action onCancel)
    {
        _onCreate = onCreate;   // keep the "Create" function
        _onCancel = onCancel;   // keep the "Cancel" function
    }

    // Purpose: The "Create" button - checks the name and closes the dialog with the result.
    // Input:   nothing (reads Name and Description).
    // Output:  nothing. If the name is not valid, shows ValidationError and the dialog stays open.
    [RelayCommand]
    private void Create()
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

        // Clean the description: null if empty, otherwise without spaces at both ends
        var description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        // Call the "Create" function with the result - DialogService closes the dialog and answers the caller
        _onCreate(new NewProjectDialogResult(trimmedName, description));
    }

    // Purpose: The "Cancel" button - closes the dialog without a result.
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void Cancel() => _onCancel();
}
