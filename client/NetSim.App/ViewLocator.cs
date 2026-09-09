using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using NetSim.App.ViewModels;

namespace NetSim.App;

/// <summary>
/// Avalonia asks this class: "I have a ViewModel object – which View (control)
/// should I show for it?" It answers by swapping "ViewModel" for "View" in the
/// type name, e.g. LoginViewModel -> LoginView.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null)
            return new TextBlock { Text = "(no view model)" };

        var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        return type is not null
            ? (Control)Activator.CreateInstance(type)!
            : new TextBlock { Text = "View not found: " + name };
    }

    // Only run for our own view models.
    public bool Match(object? data) => data is ViewModelBase;
}
