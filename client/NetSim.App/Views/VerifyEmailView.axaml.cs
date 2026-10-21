using Avalonia.Controls;  // UserControl

namespace NetSim.App.Views;

// Code-behind of VerifyEmailView.axaml - the "enter the code we emailed you" screen.
// "partial" = the other half of this class is generated from the .axaml file at build time.
// ": UserControl" = a reusable piece of screen that is shown inside a window.
// There is no logic here on purpose: everything the screen does lives in its view model
public partial class VerifyEmailView : UserControl
{
    // Purpose: Builds the VerifyEmailView screen's controls from VerifyEmailView.axaml.
    // Input:   nothing.
    // Output:  none (a constructor) - a new screen object.
    // The constructor, run when the screen is created
    public VerifyEmailView()
    {
        // Builds the controls described in VerifyEmailView.axaml
        InitializeComponent();
    }
}
