using Avalonia.Controls;  // UserControl

namespace NetSim.App.Views;

// Code-behind of UsersView.axaml - the admin's user management screen.
// "partial" = the other half of this class is generated from the .axaml file at build time.
// ": UserControl" = a reusable piece of screen that is shown inside a window.
// There is no logic here on purpose: everything the screen does lives in its view model
public partial class UsersView : UserControl
{
    // The constructor, run when the screen is created
    public UsersView()
    {
        // Builds the controls described in UsersView.axaml
        InitializeComponent();
    }
}
