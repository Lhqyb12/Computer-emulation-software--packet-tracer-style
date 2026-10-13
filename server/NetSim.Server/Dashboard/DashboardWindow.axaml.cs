using Avalonia.Controls;  // Window

namespace NetSim.Server.Dashboard;

// Code-behind of DashboardWindow.axaml. "partial" because the other half of this class is generated
// from the .axaml file at build time. ": Window" = this class is a window
public partial class DashboardWindow : Window
{
    // The constructor, run when DashboardApp creates the window
    public DashboardWindow()
    {
        // Builds the controls described in DashboardWindow.axaml
        InitializeComponent();
    }
}
