namespace NetSim.Server.Dtos;

// The data of a "reset-password" message: step 2 of resetting a password
public class ResetPasswordRequest
{
    // The email of the account
    public string Email { get; set; } = string.Empty;

    // The 6-digit code that was emailed in step 1 - it proves the sender owns that email address
    public string Code { get; set; } = string.Empty;

    // The password the user wants from now on (plain text here; it is hashed before it is stored)
    public string NewPassword { get; set; } = string.Empty;
}
