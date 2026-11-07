namespace NetSim.Server.Dtos;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The data of a "delete-user" message: which user to delete
public class DeleteUserRequest
{
    // The id (primary key in the Users table) of the user to delete
    public int Id { get; set; }
}
