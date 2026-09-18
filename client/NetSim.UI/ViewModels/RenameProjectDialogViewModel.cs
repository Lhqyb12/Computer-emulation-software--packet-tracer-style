using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSim.UI.Common;

namespace NetSim.UI.ViewModels;

public partial class RenameProjectDialogViewModel : ViewModelBase
{
    private const int MaxNameLength = 100;

    private readonly Action<string> _onRename;
    private readonly Action _onCancel;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string? _validationError;

    public RenameProjectDialogViewModel(string currentName, Action<string> onRename, Action onCancel)
    {
        _name = currentName;
        _onRename = onRename;
        _onCancel = onCancel;
    }

    [RelayCommand]
    private void Rename()
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
        _onRename(trimmedName);
    }

    [RelayCommand]
    private void Cancel() => _onCancel();
}
