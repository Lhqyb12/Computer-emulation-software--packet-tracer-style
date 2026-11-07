using System;                           // Action
using CommunityToolkit.Mvvm.ComponentModel;  // [ObservableProperty]
using CommunityToolkit.Mvvm.Input;           // [RelayCommand]
using NetSim.UI.Common;                 // ViewModelBase

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The "Create a class" dialog. Like NewProjectDialogViewModel, it only collects the name and checks
// what can be checked on the spot; the class itself is created by IClassroomService afterwards
public partial class CreateClassroomDialogViewModel : ViewModelBase
{
    // The same limit as ClassroomService.MaxNameLength
    private const int MaxNameLength = 60;

    private readonly Action<string> _onCreate;   // called with the name when "Create" is pressed
    private readonly Action _onCancel;           // called when "Cancel" is pressed

    // The text box of the class name
    [ObservableProperty]
    private string _name = string.Empty;

    // The red line under the text box (null = no error)
    [ObservableProperty]
    private string? _validationError;

    // Purpose: Builds the dialog.
    // Input:   onCreate, onCancel - what to do when the user closes it (passed in by DialogService).
    // Output:  none (a constructor).
    public CreateClassroomDialogViewModel(Action<string> onCreate, Action onCancel)
    {
        _onCreate = onCreate;   // keep the "Create" function
        _onCancel = onCancel;   // keep the "Cancel" function
    }

    // Purpose: Checks the name and closes the dialog with it.
    // Input:   nothing (reads Name).
    // Output:  nothing. Sets ValidationError instead of closing if the name is not valid.
    [RelayCommand]
    private void Create()
    {
        // The name without spaces at both ends
        var trimmed = Name.Trim();

        // Empty name - show the error and stay open
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            ValidationError = "Class name cannot be empty.";
            return;   // stop here - nothing more to do
        }

        // Too long - show the error and stay open
        if (trimmed.Length > MaxNameLength)
        {
            ValidationError = $"Class name cannot exceed {MaxNameLength} characters.";
            return;   // stop here - nothing more to do
        }

        // The name is fine - hide any old error
        ValidationError = null;

        // Call the "Create" function with the name - DialogService closes the dialog
        _onCreate(trimmed);
    }

    // Purpose: Closes the dialog without creating anything.
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void Cancel() => _onCancel();
}
