using Avalonia.Controls;  // UserControl

namespace NetSim.App.Views;

// Code-behind of ForgotPasswordView.axaml - the "forgot my password" screen (ask for a code, then set a new password).
// "partial" = the other half of this class is generated from the .axaml file at build time.
// ": UserControl" = a reusable piece of screen that is shown inside a window.
// There is no logic here on purpose: everything the screen does lives in its view model
public partial class ForgotPasswordView : UserControl
{
    // Purpose: Builds the ForgotPasswordView screen's controls from ForgotPasswordView.axaml.
    // Input:   nothing.
    // Output:  none (a constructor) - a new screen object.
    // The constructor, run when the screen is created
    public ForgotPasswordView()
    {
        // Builds the controls described in ForgotPasswordView.axaml
        InitializeComponent();
    }
}
