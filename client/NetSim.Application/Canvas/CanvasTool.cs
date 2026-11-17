namespace NetSim.Application.Canvas;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The tool the user picked in the canvas toolbar - it decides what a left click on the canvas does (steps 4-6).
// "enum" = a closed list of named options; a variable of this type can only hold one of them
public enum CanvasTool
{
    Select,    // step 4: click / drag devices and cables to select and move them
    Pan,       // step 5: drag the canvas itself to scroll it
    Connect,   // step 6: click one device, then a second one, and a cable is made between them
}
