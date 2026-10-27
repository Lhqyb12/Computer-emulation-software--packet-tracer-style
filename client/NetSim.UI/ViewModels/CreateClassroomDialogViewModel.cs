using System;                           // Action
using CommunityToolkit.Mvvm.ComponentModel;  // [ObservableProperty]
using CommunityToolkit.Mvvm.Input;           // [RelayCommand]
using NetSim.UI.Common;                 // ViewModelBase

namespace NetSim.UI.ViewModels;

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
        _onCreate = onCreate;
        _onCancel = onCancel;
    }

    // Purpose: Checks the name and closes the dialog with it.
    // Input:   nothing (reads Name).
    // Output:  nothing. Sets ValidationError instead of closing if the name is not valid.
    [RelayCommand]
    private void Create()
    {
        var trimmed = Name.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            ValidationError = "Class name cannot be empty.";
            return;
        }

        if (trimmed.Length > MaxNameLength)
        {
            ValidationError = $"Class name cannot exceed {MaxNameLength} characters.";
            return;
        }

        ValidationError = null;
        _onCreate(trimmed);
    }

    // Purpose: Closes the dialog without creating anything.
    [RelayCommand]
    private void Cancel() => _onCancel();
}
