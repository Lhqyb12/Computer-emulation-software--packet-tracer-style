using CommunityToolkit.Mvvm.ComponentModel;  // ObservableObject, [ObservableProperty]
using NetSim.UI.Common;                      // ViewModelBase

namespace NetSim.UI.Dialogs;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Holds the dialog that is open right now. ": ObservableObject" = it can tell the screen when
// ActiveDialog changes, so the pop-up layer appears and disappears by itself
public sealed partial class DialogHost : ObservableObject, IDialogHost
{
    // The open dialog's view model, or null = no dialog is open.
    // [ObservableProperty] writes the public "ActiveDialog" property that IDialogHost requires
    [ObservableProperty]
    private ViewModelBase? _activeDialog;
}
