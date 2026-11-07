namespace NetSim.Core.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// A fixed list of the possible device kinds. Every device in a network is exactly one of these
// values - nothing else is allowed. "enum" = a type whose only possible values are the names listed here
public enum DeviceType
{
    Router,   // connects different networks and passes packets between them
    Switch,   // connects devices inside one local network
    Pc,       // a desktop computer
    Server,   // a computer that gives services to others
    Laptop,   // a portable computer
}
