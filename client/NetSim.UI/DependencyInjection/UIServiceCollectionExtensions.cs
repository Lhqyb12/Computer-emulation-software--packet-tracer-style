using Microsoft.Extensions.DependencyInjection;
using NetSim.UI.Dialogs;
using NetSim.UI.Navigation;
using NetSim.UI.Theming;
using NetSim.UI.ViewModels;
using NetSim.UI.Views;

namespace NetSim.UI.DependencyInjection;

/// <summary>
/// Composition entry point for the UI layer. Future Views and ViewModels are registered
/// here as they are introduced.
/// </summary>
public static class UIServiceCollectionExtensions
{
    public static IServiceCollection AddUIServices(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IThemeVariantWriter, AvaloniaThemeVariantWriter>();
        services.AddSingleton<IThemeService, ThemeService>();

        // One dialog host/overlay for the app's single window - see UI/Dialogs/IDialogHost.cs.
        // The individual dialog ViewModels (NewProjectDialogViewModel, ...) are constructed
        // directly by DialogService (they take result/cancel callbacks, not DI dependencies),
        // so they are not registered here.
        services.AddSingleton<IDialogHost, DialogHost>();
        services.AddSingleton<IDialogService, DialogService>();

        services.AddSingleton<ShellViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<ProjectsViewModel>();
        services.AddTransient<NetworkCanvasViewModel>();
        services.AddTransient<DeviceLibraryViewModel>();
        services.AddTransient<DevicePropertiesViewModel>();
        services.AddTransient<NetworkWorkspaceViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<DesignSystemDemoViewModel>();

        services.AddTransient<MainWindow>();

        return services;
    }
}
