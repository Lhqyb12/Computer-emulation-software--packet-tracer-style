namespace NetSim.Server.Dtos;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The data of a "delete-project" message: which project to delete.
// Only the id - the server checks that this project belongs to the user in the token before deleting it
public class DeleteProjectRequest
{
    // The id of the project to delete (a Guid, unlike DeleteUserRequest's int - see ProjectRecord.Id)
    public Guid Id { get; set; }
}
