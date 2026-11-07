namespace NetSim.Server.Dtos;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The data of a "verify-email" message: a new user proves they own their email address
public class VerifyEmailRequest
{
    // The email of the account that is being verified
    public string Email { get; set; } = string.Empty;

    // The 6-digit code that was emailed to that address
    public string Code { get; set; } = string.Empty;
}
