using Avalonia.Controls;   // brings in UserControl - the Avalonia base class of a piece of screen

namespace NetSim.UI.Views;   // the "address" of this file: the Views part of the UI layer

// The code half of DevicePropertiesView.axaml (step 9).
// "partial" = the other half of this class is built from the .axaml file (every control written there).
// ": UserControl" = a piece of screen made of other controls. It has no logic of its own - everything the panel does is
// in DevicePropertiesViewModel
public partial class DevicePropertiesView : UserControl
{
    // Purpose:       Builds the panel's controls.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing (a constructor) - every control written in DevicePropertiesView.axaml exists.
    public DevicePropertiesView()
    {
        // Load DevicePropertiesView.axaml and build every control written in it
        InitializeComponent();
    }
}
