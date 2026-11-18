using Avalonia;                  // brings in StyledElement - any screen element that has a DataContext
using Avalonia.Controls;         // brings in UserControl - the Avalonia base class of this view
using Avalonia.Input;            // brings in PointerPressedEventArgs - the details of "a mouse button was pressed"
using Avalonia.Interactivity;    // brings in RoutingStrategies - which way an event travels through the screen
using NetSim.UI.Devices;         // brings in DeviceLibraryEntry - the data of one card in the library
using NetSim.UI.ViewModels;      // brings in DeviceLibraryViewModel - the library panel's view model

namespace NetSim.UI.Views;   // the "address" of this file: the Views part of the UI layer

// The code half of DeviceLibraryView.axaml.
// "partial" = the other half of this class is built from the .axaml file (the controls written there).
// ": UserControl" = a piece of screen made of other controls
public partial class DeviceLibraryView : UserControl
{
    // Purpose:       Builds this view and starts listening to mouse presses on the card list.
    // Precondition:  receives nothing.
    // Postcondition: returns nothing (a constructor) - the controls of DeviceLibraryView.axaml exist, and pressing a card
    //                runs OnDeviceListPointerPressed.
    public DeviceLibraryView()
    {
        // Load DeviceLibraryView.axaml and build every control written in it
        InitializeComponent();

        // DeviceList = the card list (named with x:Name="DeviceList" in the .axaml).
        // AddHandler(event, method, route): hear every mouse press on the list on its way DOWN to the card (Tunnel),
        // BEFORE the list itself reacts - so "place a device" starts the very moment a card is pressed (step 3)
        DeviceList.AddHandler(PointerPressedEvent, OnDeviceListPointerPressed, RoutingStrategies.Tunnel);
    }

    // Purpose:       Starts "place a device" the moment the left button is pressed on a card - the start of a drag onto the canvas.
    // Precondition:  receives sender - who raised it; e - the mouse event (Avalonia passes it in).
    //                (The view's DataContext is the DeviceLibraryViewModel.)
    // Postcondition: returns nothing - when the left button was pressed on a card, that card is the selected one, so its
    //                device kind is waiting to be placed (the canvas finishes the drag when the button is let go over it).
    //                Otherwise nothing changed.
    // Why not leave it to the list? A list may select a card only when the button is LET GO - and in a drag the button is
    // let go over the CANVAS, so the card would never be selected in time.
    private void OnDeviceListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // GetCurrentPoint(this).Properties = which buttons are down right now. Only the LEFT button starts a drag
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;   // stop here - another button
        }

        // e.Source = the element that was actually pressed (the icon, the name, the card...). Each of them has the card's
        // data as its DataContext. "as StyledElement" = use it only if it is an element with a DataContext (else null);
        // "?." = if it is null, the whole thing is null; "is not DeviceLibraryEntry entry" = if it is not a card's data, stop
        if ((e.Source as StyledElement)?.DataContext is not DeviceLibraryEntry entry)
        {
            return;   // stop here - pressed between the cards
        }

        // This view's own DataContext is the library's view model ("is ... viewModel" = if so, keep it in "viewModel")
        if (DataContext is DeviceLibraryViewModel viewModel)
        {
            // Select the card - the view model then starts "place a device" for its kind
            viewModel.SelectedEntry = entry;
        }
    }
}
