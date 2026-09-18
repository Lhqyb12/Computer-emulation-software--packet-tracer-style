using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSim.UI.Common;
using NetSim.UI.Theming;

namespace NetSim.UI.ViewModels;

/// <summary>
/// Not a real application screen - a development-only page (Phase 5, section 30) that renders
/// every design-system primitive together (colors, typography, buttons, inputs, cards, tabs,
/// status/notification styles, icons, tooltips, loading, empty state) so the team can eyeball
/// consistency before dozens of future screens are built on top of it. Reachable from the
/// sidebar for convenience.
/// </summary>
public partial class DesignSystemDemoViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private bool _isSampleChecked = true;

    [ObservableProperty]
    private bool _isSampleToggled;

    [ObservableProperty]
    private double _sampleProgress = 45;

    public DesignSystemDemoViewModel(IThemeService themeService)
    {
        _themeService = themeService;
    }

    public string Title => "Design System Demo";

    [RelayCommand]
    private void PreviewDark() => _themeService.ApplyTheme(AppTheme.Dark);

    [RelayCommand]
    private void PreviewLight() => _themeService.ApplyTheme(AppTheme.Light);

    [RelayCommand]
    private void PreviewSystem() => _themeService.ApplyTheme(AppTheme.System);
}
