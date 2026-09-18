using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSim.UI.Common;
using NetSim.UI.Theming;

namespace NetSim.UI.ViewModels;

/// <summary>
/// Only the Appearance section exists so far - the rest of Settings (a future phase) will be
/// added alongside project persistence/cloud features.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private AppTheme _selectedTheme;

    public SettingsViewModel(IThemeService themeService)
    {
        _themeService = themeService;
        _selectedTheme = themeService.CurrentTheme;
        _themeService.ThemeChanged += (_, theme) => SelectedTheme = theme;
    }

    public string Title => "Settings";

    public bool IsDarkSelected
    {
        get => SelectedTheme == AppTheme.Dark;
        set
        {
            if (value)
            {
                SetTheme(AppTheme.Dark);
            }
        }
    }

    public bool IsLightSelected
    {
        get => SelectedTheme == AppTheme.Light;
        set
        {
            if (value)
            {
                SetTheme(AppTheme.Light);
            }
        }
    }

    public bool IsSystemSelected
    {
        get => SelectedTheme == AppTheme.System;
        set
        {
            if (value)
            {
                SetTheme(AppTheme.System);
            }
        }
    }

    [RelayCommand]
    private void SetTheme(AppTheme theme) => _themeService.ApplyTheme(theme);

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        OnPropertyChanged(nameof(IsDarkSelected));
        OnPropertyChanged(nameof(IsLightSelected));
        OnPropertyChanged(nameof(IsSystemSelected));
    }
}
