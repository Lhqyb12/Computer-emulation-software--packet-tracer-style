using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSim.UI.Common;
using NetSim.UI.Dialogs;

namespace NetSim.UI.ViewModels;

/// <summary>
/// Pure input collector for creating a project: validates only what can be checked without a
/// round trip (blank/too-long name). Uniqueness is checked by
/// <see cref="Application.Services.IProjectService.CreateProject"/> after this dialog resolves -
/// see <see cref="IDialogService"/>.
/// </summary>
public partial class NewProjectDialogViewModel : ViewModelBase
{
    private const int MaxNameLength = 100;

    private readonly Action<NewProjectDialogResult> _onCreate;
    private readonly Action _onCancel;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string? _validationError;

    public NewProjectDialogViewModel(Action<NewProjectDialogResult> onCreate, Action onCancel)
    {
        _onCreate = onCreate;
        _onCancel = onCancel;
    }

    [RelayCommand]
    private void Create()
    {
        var trimmedName = Name.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            ValidationError = "Project name cannot be empty.";
            return;
        }

        if (trimmedName.Length > MaxNameLength)
        {
            ValidationError = $"Project name cannot exceed {MaxNameLength} characters.";
            return;
        }

        ValidationError = null;
        var description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        _onCreate(new NewProjectDialogResult(trimmedName, description));
    }

    [RelayCommand]
    private void Cancel() => _onCancel();
}
