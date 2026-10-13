using Avalonia.Controls;  // Window

namespace NetSim.App.Views;

// Code-behind of MainWindow.axaml - the main window of the client, which hosts one screen at a time.
// "partial" = the other half of this class is generated from the .axaml file at build time.
// ": Window" = this class is a window.
// There is no logic here on purpose: everything the screen does lives in its view model
public partial class MainWindow : Window
{
    // The constructor, run when the screen is created
    public MainWindow()
    {
        // Builds the controls described in MainWindow.axaml
        InitializeComponent();
    }
}
