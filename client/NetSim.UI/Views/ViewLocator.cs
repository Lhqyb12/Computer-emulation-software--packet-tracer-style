using System;                        // StringComparison, Activator
using Avalonia.Controls;             // Control, TextBlock
using Avalonia.Controls.Templates;   // IDataTemplate
using NetSim.UI.Common;              // ViewModelBase

namespace NetSim.UI.Views;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Turns a view model into the view that draws it, by the names:
// "NetSim.UI.ViewModels.HomeViewModel" -> "NetSim.UI.Views.HomeView".
// So a new screen needs no registration here - only matching names.
// It is listed in App.axaml under DataTemplates, so whenever a control has to show a ViewModelBase
// (e.g. the middle of the Shell), Avalonia asks this class to build the view
public sealed class ViewLocator : IDataTemplate
{
    // Purpose: Builds the view for one view model.
    // Input:   param - the view model to show (may be null).
    // Output:  the view (a Control) connected to the view model, or a text that says what went wrong.
    public Control Build(object? param)
    {
        // Nothing to show
        if (param is null)
        {
            return new TextBlock { Text = "(no view model)" };
        }

        // The type of the view model, e.g. HomeViewModel
        var viewModelType = param.GetType();

        // Its full name, with "ViewModels" -> "Views" and "ViewModel" -> "View".
        // "!" = we promise FullName is not null (a normal type always has one).
        // Ordinal = compare letter by letter, with no language rules
        var viewTypeName = viewModelType.FullName!
            .Replace("ViewModels", "Views", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        // Look for a type with that name in the same project (assembly) as the view model
        var viewType = viewModelType.Assembly.GetType(viewTypeName);

        // Found it, and creating it gives a Control (Activator = create an object from a type, at run time)
        if (viewType is not null && Activator.CreateInstance(viewType) is Control control)
        {
            // Connect the view to the view model - its bindings now read from it
            control.DataContext = param;
            return control;   // give the ready view to Avalonia, which draws it
        }

        // No such view - show which name was missing, to find the mistake quickly
        return new TextBlock { Text = $"View not found: {viewTypeName}" };
    }

    // Purpose: Tells Avalonia whether this class handles an object.
    // Input:   data - the object to show.
    // Output:  true only for the workspace's view models (they inherit from this project's ViewModelBase).
    public bool Match(object? data) => data is ViewModelBase;
}
