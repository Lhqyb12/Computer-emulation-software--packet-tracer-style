using Avalonia.Controls;   // UserControl - the Avalonia base class of this view

namespace NetSim.UI.Views;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The code half of DeviceLibraryView.axaml. "partial" = the other half is built from the .axaml file
public partial class DeviceLibraryView : UserControl
{
    // Purpose: Builds this view.
    // Input:   nothing.
    // Output:  none (a constructor).
    public DeviceLibraryView()
    {
        // Load DeviceLibraryView.axaml and build the controls written in it
        InitializeComponent();
    }
}
