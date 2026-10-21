using Avalonia.Controls;  // UserControl

namespace NetSim.App.Views;

// Code-behind of RegisterView.axaml - the "create account" screen.
// "partial" = the other half of this class is generated from the .axaml file at build time.
// ": UserControl" = a reusable piece of screen that is shown inside a window.
// There is no logic here on purpose: everything the screen does lives in its view model
public partial class RegisterView : UserControl
{
    // Purpose: Builds the RegisterView screen's controls from RegisterView.axaml.
    // Input:   nothing.
    // Output:  none (a constructor) - a new screen object.
    // The constructor, run when the screen is created
    public RegisterView()
    {
        // Builds the controls described in RegisterView.axaml
        InitializeComponent();
    }
}
