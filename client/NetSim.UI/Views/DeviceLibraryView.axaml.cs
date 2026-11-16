using Avalonia;                  // StyledElement - any element that has a DataContext
using Avalonia.Controls;         // UserControl - the Avalonia base class of this view
using Avalonia.Input;            // PointerPressedEventArgs - "a mouse button was pressed"
using Avalonia.Interactivity;    // RoutingStrategies - which way an event travels through the screen
using NetSim.UI.Devices;         // DeviceLibraryEntry - one card's data
using NetSim.UI.ViewModels;      // DeviceLibraryViewModel

namespace NetSim.UI.Views;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The code half of DeviceLibraryView.axaml. "partial" = the other half is built from the .axaml file
public partial class DeviceLibraryView : UserControl
{
    // Purpose:       Builds this view and starts listening to mouse presses on the card list.
    // Precondition:  none.
    // Postcondition: the controls of DeviceLibraryView.axaml exist, and pressing a card runs OnDeviceListPointerPressed.
    public DeviceLibraryView()
    {
        // Load DeviceLibraryView.axaml and build the controls written in it
        InitializeComponent();

        // Hear every mouse press on the card list on its way DOWN to the card (Tunnel), BEFORE the list
        // itself reacts - so "place a device" starts the very moment a card is pressed (step 3)
        DeviceList.AddHandler(PointerPressedEvent, OnDeviceListPointerPressed, RoutingStrategies.Tunnel);
    }

    // Purpose:       Starts "place a device" the moment the left button is pressed on a card - the start
    //                of a drag onto the canvas.
    // Precondition:  e is the mouse event (Avalonia passes it in); the view's DataContext is the
    //                DeviceLibraryViewModel.
    // Postcondition: when the left button was pressed on a card - that card is the selected one, so its
    //                device kind is waiting to be placed (the canvas finishes the drag when the button
    //                is let go over it). Otherwise nothing changed.
    // Why not leave it to the list? A list may select a card only when the button is let go - and in a
    // drag the button is let go over the CANVAS, so the card would never be selected in time.
    private void OnDeviceListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only the left mouse button starts a drag
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;   // stop here - another button
        }

        // The element that was pressed (the icon, the name, the card...) - each one's DataContext is the
        // card's data. "is not ... entry" = if it is not a card, stop
        if ((e.Source as StyledElement)?.DataContext is not DeviceLibraryEntry entry)
        {
            return;   // stop here - pressed between the cards
        }

        // Select the card - the view model then starts "place a device" for its kind
        if (DataContext is DeviceLibraryViewModel viewModel)
        {
            viewModel.SelectedEntry = entry;
        }
    }
}
