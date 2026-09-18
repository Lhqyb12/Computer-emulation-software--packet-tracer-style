using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;

namespace NetSim.UI.Views;

public partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();
    }

    // "Avalonia.Application" is spelled out fully per the project-wide naming-collision
    // decision recorded in docs/CONTEXT.md: this project tree sits under the "NetworkSimulator"
    // root alongside the NetSim.Application project, and the bare name "Application"
    // has been mis-resolved there before.
    private void OnExitClicked(object? sender, RoutedEventArgs e)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
