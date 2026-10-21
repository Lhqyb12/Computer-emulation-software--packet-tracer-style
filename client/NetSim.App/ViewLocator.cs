using System;                      // Type, Activator, StringComparison
using Avalonia.Controls;           // Control, TextBlock
using Avalonia.Controls.Templates; // IDataTemplate
using NetSim.App.ViewModels;       // ViewModelBase

namespace NetSim.App;

/// <summary>
/// Avalonia asks this class: "I have a ViewModel object – which View (control)
/// should I show for it?" It answers by swapping "ViewModel" for "View" in the
/// type name, e.g. LoginViewModel -> LoginView.
/// </summary>
// ": IDataTemplate" = it promises to have the two methods Avalonia calls: Match and Build
public class ViewLocator : IDataTemplate
{
    // Purpose: Finds and creates the View that matches a view model, by name (LoginViewModel -> LoginView).
    // Input:   data - the view model object (may be null).
    // Output:  the new View control, or a TextBlock with an error text if no view was found.
    // Creates the view for a view model. data = the view model object
    public Control Build(object? data)
    {
        // No view model at all: show a short text instead of crashing
        if (data is null)
            return new TextBlock { Text = "(no view model)" };

        // The full name of the view model's class, e.g. "NetSim.App.ViewModels.LoginViewModel",
        // with every "ViewModel" replaced by "View" -> "NetSim.App.Views.LoginView".
        // "!" = we promise FullName is not null
        var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);

        // Look for a class with that name. null = there is no such class
        var type = Type.GetType(name);

        // Found: create an object of that class (Activator.CreateInstance is "new" for a type that is
        // known only while the program runs). "(Control)" = treat the result as a Control, the base
        // class of everything that can be shown on screen.
        // Not found: show a text that says which view is missing
        return type is not null
            ? (Control)Activator.CreateInstance(type)!
            : new TextBlock { Text = "View not found: " + name };
    }

    // Purpose: Tells Avalonia whether this locator handles the given object.
    // Input:   data - the object Avalonia wants to show.
    // Output:  true if it is one of our view models (ViewModelBase), otherwise false.
    // Only run for our own view models.
    // Avalonia calls Match first; Build is called only when it returns true
    public bool Match(object? data) => data is ViewModelBase;
}
