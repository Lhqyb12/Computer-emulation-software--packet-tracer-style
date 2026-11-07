using System;                        // Action - "a function passed in, to be called later"
using CommunityToolkit.Mvvm.Input;   // [RelayCommand]
using NetSim.UI.Common;              // ViewModelBase

namespace NetSim.UI.ViewModels;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// A general "are you sure?" dialog, e.g. "Delete 'My Network'? This action cannot be undone."
// Nothing here changes after it is built, so it needs no [ObservableProperty]
public partial class ConfirmationDialogViewModel : ViewModelBase
{
    private readonly Action _onConfirm;   // what to call on "Confirm"
    private readonly Action _onCancel;    // what to call on "Cancel"

    // Purpose: Builds the dialog with its texts.
    // Input:   title, message - the texts; confirmText, cancelText - the button texts;
    //          isDestructive - true = red confirm button; onConfirm, onCancel - what to call on close.
    // Output:  none (a constructor).
    public ConfirmationDialogViewModel(
        string title,
        string message,
        string confirmText,
        string cancelText,
        bool isDestructive,
        Action onConfirm,
        Action onCancel)
    {
        Title = title;                  // the title
        Message = message;              // the message
        ConfirmText = confirmText;      // the confirm button's text
        CancelText = cancelText;        // the cancel button's text
        IsDestructive = isDestructive;  // red confirm button or not
        _onConfirm = onConfirm;         // keep the "Confirm" function
        _onCancel = onCancel;           // keep the "Cancel" function
    }

    // The texts the view shows. Read-only: set once in the constructor
    public string Title { get; }   // the title

    public string Message { get; }   // the message

    public string ConfirmText { get; }   // the confirm button's text

    public string CancelText { get; }   // the cancel button's text

    // The view paints the confirm button red when this is true
    public bool IsDestructive { get; }

    // Purpose: The confirm button - closes the dialog with "yes".
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void Confirm() => _onConfirm();

    // Purpose: The cancel button - closes the dialog with "no".
    // Input:   nothing.
    // Output:  nothing.
    [RelayCommand]
    private void Cancel() => _onCancel();
}
