namespace NetSim.Server.Dtos;

// The data of a "forgot-password" message: step 1 of resetting a password
public class ForgotPasswordRequest
{
    // The email of the account whose password was forgotten; the reset code is sent to this address
    public string Email { get; set; } = string.Empty;
}
