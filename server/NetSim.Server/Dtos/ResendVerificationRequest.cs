namespace NetSim.Server.Dtos;

// The data of a "resend-verification" message: the user asks for a new verification code
public class ResendVerificationRequest
{
    // The email of the account that is still waiting to be verified
    public string Email { get; set; } = string.Empty;
}
