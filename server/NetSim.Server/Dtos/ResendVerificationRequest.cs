namespace NetSim.Server.Dtos;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The data of a "resend-verification" message: the user asks for a new verification code
public class ResendVerificationRequest
{
    // The email of the account that is still waiting to be verified
    public string Email { get; set; } = string.Empty;
}
