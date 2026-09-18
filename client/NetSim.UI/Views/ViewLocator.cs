using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using NetSim.UI.Common;

namespace NetSim.UI.Views;

/// <summary>
/// Resolves the View for a given ViewModel by naming convention (e.g.
/// "NetSim.UI.ViewModels.HomeViewModel" -> "NetSim.UI.Views.HomeView"),
/// so new screens do not require any registration here. Registered as an
/// <see cref="Avalonia.Application.DataTemplates"/> entry so any control that puts a
/// <see cref="ViewModelBase"/> in its Content is rendered through the matching View.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control Build(object? param)
    {
        if (param is null)
        {
            return new TextBlock { Text = "(no view model)" };
        }

        var viewModelType = param.GetType();
        var viewTypeName = viewModelType.FullName!
            .Replace("ViewModels", "Views", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        var viewType = viewModelType.Assembly.GetType(viewTypeName);

        if (viewType is not null && Activator.CreateInstance(viewType) is Control control)
        {
            control.DataContext = param;
            return control;
        }

        return new TextBlock { Text = $"View not found: {viewTypeName}" };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
