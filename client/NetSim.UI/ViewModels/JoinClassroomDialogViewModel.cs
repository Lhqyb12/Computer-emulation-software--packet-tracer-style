using System;                           // Action
using CommunityToolkit.Mvvm.ComponentModel;  // [ObservableProperty]
using CommunityToolkit.Mvvm.Input;           // [RelayCommand]
using NetSim.UI.Common;                 // ViewModelBase

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The "Join a class" dialog: the student types the code the teacher gave them.
// Only the length is checked here; whether a class with this code exists is checked by
// IClassroomService.JoinClassroom after the dialog closes
public partial class JoinClassroomDialogViewModel : ViewModelBase
{
    // The same length as ClassroomService.JoinCodeLength
    private const int JoinCodeLength = 6;

    private readonly Action<string> _onJoin;     // called with the code when "Join" is pressed
    private readonly Action _onCancel;           // called when "Cancel" is pressed

    // The text box of the code
    [ObservableProperty]
    private string _joinCode = string.Empty;

    // The red line under the text box (null = no error)
    [ObservableProperty]
    private string? _validationError;

    // Purpose: Builds the dialog.
    // Input:   onJoin, onCancel - what to do when the user closes it (passed in by DialogService).
    // Output:  none (a constructor).
    public JoinClassroomDialogViewModel(Action<string> onJoin, Action onCancel)
    {
        _onJoin = onJoin;       // keep the "Join" function
        _onCancel = onCancel;   // keep the "Cancel" function
    }

    // Purpose: Checks the code's length and closes the dialog with it.
    // Input:   nothing (reads JoinCode).
    // Output:  nothing. Sets ValidationError instead of closing if the code is the wrong length.
    [RelayCommand]
    private void Join()
    {
        // Small letters are fine - turn them into capitals, like the codes are shown
        var code = JoinCode.Trim().ToUpperInvariant();

        // Not exactly 6 characters - show the error and stay open
        if (code.Length != JoinCodeLength)
        {
            ValidationError = $"The code has {JoinCodeLength} characters.";
            return;   // stop here - nothing more to do
        }

        // The length is fine - hide any old error
        ValidationError = null;

        // Call the "Join" function with the code - DialogService closes the dialog
        _onJoin(code);
    }

    // Purpose: Closes the dialog without joining.
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void Cancel() => _onCancel();
}
