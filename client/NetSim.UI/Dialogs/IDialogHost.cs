using NetSim.UI.Common;   // ViewModelBase

namespace NetSim.UI.Dialogs;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The place that holds the open dialog. The Shell's view binds a dark pop-up layer to ActiveDialog:
// when it is not null the layer is shown, and the ViewLocator draws the dialog's view inside it.
// So dialogs are a layer inside the window, not separate operating-system windows
public interface IDialogHost
{
    // The open dialog's view model, or null when no dialog is open. DialogService sets it
    ViewModelBase? ActiveDialog { get; set; }
}
