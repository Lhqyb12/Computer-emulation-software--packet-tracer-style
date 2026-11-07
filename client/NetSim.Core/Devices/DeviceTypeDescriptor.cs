namespace NetSim.Core.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The details of one supported device kind that a screen needs in order to list it:
// which kind it is, and the name to show the user ("PC", not "Pc").
// It has no icon on purpose: an icon is a drawing, and drawings belong to the UI layer, not to Core.
// "record" = a small class that only holds data; two records with the same values are equal.
// "sealed" = no other class can inherit from it
public sealed record DeviceTypeDescriptor(DeviceType DeviceType, string DisplayName);
