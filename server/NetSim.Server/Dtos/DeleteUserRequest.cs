namespace NetSim.Server.Dtos;

// The data of a "delete-user" message: which user to delete
public class DeleteUserRequest
{
    // The id (primary key in the Users table) of the user to delete
    public int Id { get; set; }
}
