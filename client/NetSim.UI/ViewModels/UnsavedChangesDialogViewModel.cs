using System;
using CommunityToolkit.Mvvm.Input;
using NetSim.UI.Common;
using NetSim.UI.Dialogs;

namespace NetSim.UI.ViewModels;

/// <summary>Three-way "unsaved changes" prompt, used by both Close Project and application shutdown.</summary>
public partial class UnsavedChangesDialogViewModel : ViewModelBase
{
    private readonly Action<UnsavedChangesDecision> _onDecision;

    public UnsavedChangesDialogViewModel(string projectName, Action<UnsavedChangesDecision> onDecision)
    {
        ProjectName = projectName;
        _onDecision = onDecision;
    }

    public string ProjectName { get; }

    public string Message => $"'{ProjectName}' has unsaved changes. Do you want to save them before continuing?";

    [RelayCommand]
    private void Save() => _onDecision(UnsavedChangesDecision.Save);

    [RelayCommand]
    private void DontSave() => _onDecision(UnsavedChangesDecision.DontSave);

    [RelayCommand]
    private void Cancel() => _onDecision(UnsavedChangesDecision.Cancel);
}
