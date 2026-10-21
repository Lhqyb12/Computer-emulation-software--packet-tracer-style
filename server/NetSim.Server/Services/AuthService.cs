using Microsoft.EntityFrameworkCore;  // AnyAsync / FirstOrDefaultAsync - asynchronous DB queries
using NetSim.Server.Data;             // AppDbContext
using NetSim.Server.Dtos;             // RegisterRequest / LoginRequest / AuthResponse
using NetSim.Server.Models;           // User
using Google.Apis.Auth;               // GoogleJsonWebSignature - checks a token that Google signed


// The "folder" this class belongs to. Other files reach it with "using NetSim.Server.Services"
namespace NetSim.Server.Services;

// This is where all the business logic for register/login lives - deliberately kept separate from
// MessageRouter (responsible only for understanding which action a message asks for) and from
// AppDbContext (responsible only for DB access).
// This separation of concerns makes testing easier (AuthService can be tested without a network at all)
// and improves maintainability
public class AuthService
{
    // The connection to the database. "readonly" = set once in the constructor and never replaced
    private readonly AppDbContext _db;

    // Sends real emails (verification codes and reset codes)
    private readonly EmailSender _email;

    // Creates the JWT a client receives after a successful login
    private readonly TokenService _tokens;

    // How many wrong reset codes are allowed before the code is cancelled
    private const int MaxResetAttempts = 5;

    // How many wrong verification codes are allowed before the code is cancelled
    private const int MaxVerifyAttempts = 5;

    // The server's settings (appsettings.json and user secrets) - the Google client id is read from here
    private readonly IConfiguration _config;



    // Records security events (failed logins, password resets...) for the Super Admin dashboard
    private readonly SecurityLog _security;

    // Purpose: Builds the auth service with the objects it needs.
    // Input:   db - the database; email - sends mail; tokens - creates JWTs; config - the settings;
    //          security - the security event log.
    // Output:  none (a constructor) - a new AuthService.
    // The constructor. Dependency Injection: we do not create these five objects ourselves; the DI
    // container (see Program.cs) creates them and passes them in, so this class doesn't know/care
    // how the database connection or the mail sender are actually built
    public AuthService(AppDbContext db, EmailSender email, TokenService tokens, IConfiguration config,
        SecurityLog security)
    {
        // Keep the five objects in the fields so the methods below can use them
        _db = db;
        _email = email;
        _tokens = tokens;
        _config = config;
        _security = security;
    }



    // Purpose: Validates a registration, saves a new unverified user and emails them a verification code.
    // Input:   request - the username, email and password the client sent.
    // Output:  an AuthResponse: Success = true if the account was created, otherwise false with the reason
    //          (invalid email, weak password, email taken, email could not be sent).
    // Creates a new account from the username, email and password the client sent.
    // "async Task<AuthResponse>" = the method can pause at an "await" without blocking a thread,
    // and in the end it gives back an AuthResponse (success or failure + a message)
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Check #0: the email has to look like a real address. The client checks this too, but anyone can
        // write their own client and talk to the server directly - so the server never trusts that it was checked
        if (!EmailPolicy.IsValid(request.Email))
            return new AuthResponse { Success = false, Message = "Please enter a valid email address." };


        // Check #1: password policy - checked first and before any DB call on purpose (fail fast).
        // If the password is weak, there's no point spending a DB query checking for a duplicate email -
        // this saves load and response time.
        // Validate returns null when the password is fine, or the text of the problem when it is not
        string? passwordError = PasswordPolicy.Validate(request.Password, request.Username);

        // Not null = there is a problem with the password; return it to the client as the message
        if (passwordError is not null)
            return new AuthResponse { Success = false, Message = passwordError };

        // Check #2: duplicate email. AnyAsync generates a SQL "EXISTS(...)" query -
        // much more efficient than ToListAsync/FirstOrDefaultAsync, because the DB can stop at the first
        // match and doesn't need to return any actual columns, just true/false.
        // "u => u.Email == request.Email" = the condition: a user whose email equals the one in the request
        bool emailTaken = await _db.Users.AnyAsync(u => u.Email == request.Email);

        // The email already belongs to an account - refuse
        if (emailTaken)
            return new AuthResponse { Success = false, Message = "Email is already registered." };

        // Only after both checks pass do we hash the password. From this point on, "request.Password"
        // (the plain text) is no longer used in the code at all; the hash variable is the only thing that gets kept
        string hash = PasswordHasher.Hash(request.Password);

        // Creates a new Entity (User) - note that PasswordHash gets the hash, never the raw request.Password
        var user = new User
        {
            Username = request.Username,  // the display name the user chose
            Email = request.Email,        // the address the verification code will be sent to
            PasswordHash = hash,          // salt + hash, never the password itself
        };

        // Add only "marks" the Entity as Added in EF Core's Change Tracker - nothing has actually been sent to the DB yet
        _db.Users.Add(user);

        // The save can fail, so it runs inside "try"
        try
        {
            // Only here, in SaveChangesAsync, does EF Core actually build and run the INSERT statement against Postgres (inside a transaction)
            await _db.SaveChangesAsync();
        }
        // DbUpdateException = the database refused the INSERT
        catch (DbUpdateException)
        {
            // Extremely rare: two registrations with the same email arrived at almost the exact same
            // moment, and both passed the AnyAsync check above before either one finished saving. The
            // database's own unique index (see AppDbContext.OnModelCreating) is the real safety net here -
            // this catch just turns its rejection into the same friendly message as the normal case above,
            // instead of letting an unhandled exception end the request with "Internal server error"
            return new AuthResponse { Success = false, Message = "Email is already registered." };
        }

        // Write the new account to the security log (shown in the Super Admin dashboard)
        await _security.RecordAsync("Account created", user.Email, "Registered with a password - waiting for email verification");

        // The account exists now, but it is locked (EmailVerified = false) until the code is typed in.
        // sent = true if the email with the code really went out
        bool sent = await SendVerificationCodeAsync(user);

        // The email could not be sent
        if (!sent)
        {
            // We could not deliver the code, so nobody could ever unlock this account. Deleting the row
            // lets the user fix the address (or simply try again) instead of hitting "already registered".
            // Remove marks the row for deletion, SaveChangesAsync runs the DELETE
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return new AuthResponse { Success = false, Message = "We couldn't send an email to that address. Check it and try again." };
        }

        // Generic success message - we don't return, for example, the new user's internal Id, to avoid exposing unnecessary information
        return new AuthResponse { Success = true, Message = "Account created. Check your email for a verification code." };

    }

    // Purpose: Checks an email and a password and signs the user in.
    // Input:   request - the email and password the client sent.
    // Output:  an AuthResponse: on success holds the Role and a JWT Token; NeedsVerification = true if the email
    //          is not verified yet; otherwise Success = false with "Wrong email or password."
    // Checks an email and a password, and on success returns a token that proves who the user is
    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // FirstOrDefaultAsync returns the matching user or null if none was found (unlike First, which would throw)
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // *A key security principle*: if the email doesn't exist at all, we return exactly the same message
        // as when the password is wrong (below) - the generic "Wrong email or password".
        // This prevents "User Enumeration": an attacker can't tell the difference between "this email isn't
        // registered" and "the password is wrong", and therefore can't discover which email addresses are
        // registered in the system by trial and error
        if (user is null)
        {
            // The client gets the generic message; the real reason is written only to the owner's
            // security log, which the client can never see
            await _security.RecordAsync("Login failed", request.Email, "Unknown email");
            return new AuthResponse { Success = false, Message = "Wrong email or password." };
        }

        // Verify recomputes PBKDF2 on the entered password, using the same salt and iteration count stored in
        // user.PasswordHash, and compares in constant time (see PasswordHasher.Verify) - again, the original
        // password is never "decrypted"
        bool ok = PasswordHasher.Verify(request.Password, user.PasswordHash);

        // The exact same generic message as the "user doesn't exist" case - the consistency of the message is the important part here
        if (!ok)
        {
            // The real reason (wrong password) goes only to the security log
            await _security.RecordAsync("Login failed", request.Email, "Wrong password");
            return new AuthResponse { Success = false, Message = "Wrong email or password." };
        }

        // Checked only AFTER the password was proven correct - so this answer is given only to the person
        // who registered the account, and an attacker can't use it to find out which emails are registered
        if (!user.EmailVerified)
        {
            // Send a fresh code, so the user has one to type in the verification screen
            await SendVerificationCodeAsync(user);

            // Write to the security log that this login was stopped
            await _security.RecordAsync("Login blocked", user.Email, "Email not verified yet - a new code was sent");

            // NeedsVerification tells the client to open the "enter your code" screen
            return new AuthResponse
            {
                Success = false,                                                  // not signed in
                NeedsVerification = true,                                         // the client should show the code screen
                Message = "Please verify your email first. We sent you a new code.",
            };
        }


        // Remember when this user last signed in. UtcNow = the time without a time zone, the same everywhere
        user.LastLoginAt = DateTime.UtcNow;

        // Save that change (an UPDATE statement)
        await _db.SaveChangesAsync();

        // The login succeeded
        return new AuthResponse
        {
            Success = true,
            Message = "Signed in.",
            Role = user.Role,                    // "User" or "Admin" - the client decides which screens to show
            Token = _tokens.CreateToken(user),   // the JWT the client sends back with every later request
        };


    }

    // Purpose: Checks a Google ID token and signs in the matching user, creating the account if it is new.
    // Input:   request - the ID token the client got from Google.
    // Output:  an AuthResponse: on success holds the Email, Role and Token; otherwise Success = false with the reason.
    // Sign-in with a Google account. The client sends an "ID token" it received from Google;
    // we check that Google really signed it, and then find (or create) the matching user
    public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request)
    {
        // payload = the details inside the token (email, name, Google's id for this person).
        // Declared here, before the "try", so it can still be used after it
        GoogleJsonWebSignature.Payload payload;

        // Checking the token can fail, so it runs inside "try"
        try
        {
            // The rules for the check
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                // Audience = who the token was issued for. It must be OUR Google client id; a token that
                // Google gave to some other application is refused. "!" = we promise the setting is not null
                Audience = new[] { _config["Google:ClientId"]! }
            };

            // Checks Google's signature, the expiry time and the audience. Throws if any of them is wrong
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        // InvalidJwtException = the token is fake, expired, or was not issued for us
        catch (InvalidJwtException)
        {
            // Write it to the security log. null = we do not know whose email this was
            await _security.RecordAsync("Google sign-in failed", null, "The Google token was rejected");
            return new AuthResponse { Success = false, Message = "Google sign-in failed." };
        }


        // Google itself says this person never proved they own the address - do not trust it
        if (!payload.EmailVerified)
            return new AuthResponse { Success = false, Message = "Your Google email is not verified." };

        // Look for the user: first by Google's id (payload.Subject), and if nothing was found ("??")
        // by the email address - that covers an account that was registered with a password earlier
        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Subject)
                   ?? await _db.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);

        // No such user at all - this is their first sign-in, so create the account now
        if (user is null)
        {
            user = new User
            {
                Username = payload.Name ?? payload.Email,  // the name from Google, or the email if there is no name
                Email = payload.Email,
                PasswordHash = string.Empty,               // no password: an empty hash never matches any password
                GoogleId = payload.Subject,                // Google's permanent id for this person
                // Google already checked this address (payload.EmailVerified above), so we don't ask again
                EmailVerified = true,

            };

            // Mark the new user to be inserted; the save happens below
            _db.Users.Add(user);
        }
        // The user exists (found by email) but was never linked to a Google account
        else if (user.GoogleId is null)
        {
            // Link it, so next time the user is found by the Google id
            user.GoogleId = payload.Subject;
        }

        // The account exists but its email was never verified
        if (!user.EmailVerified)
        {
            // Someone registered this address with a password but never proved they own it - it may not
            // have been this person at all. Google has now proven who the real owner is, so the account
            // becomes verified and the unproven password is thrown away (an empty hash never matches)
            user.PasswordHash = string.Empty;
            user.VerifyCodeHash = null;        // the waiting verification code is cancelled
            user.VerifyCodeExpiresAt = null;   // and so is its expiry time
            user.EmailVerified = true;
        }


        // Remember when this user last signed in
        user.LastLoginAt = DateTime.UtcNow;

        // Save everything that changed above (a new user, a new link, or just the login time)
        await _db.SaveChangesAsync();

        // The login succeeded
        return new AuthResponse
        {
            Success = true,
            Message = "Signed in with Google.",
            Email = user.Email,                  // the client did not type an email, so we tell it which one this is
            Role = user.Role,                    // "User" or "Admin"
            Token = _tokens.CreateToken(user),   // the JWT for later requests
        };

    }



    // Purpose: Creates a password reset code, stores its hash and emails it to the user.
    // Input:   request - the email the user typed.
    // Output:  an AuthResponse that always reports success, so it never reveals whether the email is registered.
    // Step 1 of "forgot my password": creates a 6-digit code and emails it to the user
    public async Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        // Find the user by email. null = no such user
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // Exactly the same principle as in LoginAsync: whether the email exists or not, we always return
        // the same generic message - otherwise an attacker could use this action to "discover" which
        // emails are registered with us at all (User Enumeration), just by watching whether an email arrived
        if (user is null)
        {
            // Many of these in a row, for different emails, is what "guessing which emails are
            // registered" looks like from the server's side
            await _security.RecordAsync("Password reset requested", request.Email, "Unknown email - no code was sent");
        }
        // The user exists
        else
        {

            // A fresh random 6-digit code
            string code = GenerateResetCode();

            // Exactly like a password - only a hash of the code is stored, never the code itself
            user.ResetCodeHash = PasswordHasher.Hash(code);

            // The code is valid for 15 minutes from now
            user.ResetCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);

            // A new code starts with zero wrong attempts
            user.ResetAttempts = 0;

            // Save the three fields to the database
            await _db.SaveChangesAsync();

            // Write the request to the security log
            await _security.RecordAsync("Password reset requested", user.Email, "A reset code was sent by email");


            // Sending an email can fail, so it runs inside "try"
            try
            {
                // Three arguments: who to send to, the subject, and the text of the email.
                // "$" before the text lets us put the value of {code} inside it; "\n" = a new line
                await _email.SendAsync(
                    user.Email,
                    "Your NetSim password reset code",
                    $"Your password reset code is: {code}\n\nThis code expires in 15 minutes. If you didn't request this, you can ignore this email.");
            }
            // Any error from the mail server
            catch (Exception)
            {
                // Without this, a failed send ended the request with "Internal server error" - but only
                // for emails that ARE registered, which told an attacker exactly what the generic message
                // below hides. The failure is written to the owner's security log; the client sees no difference
                await _security.RecordAsync("Password reset email failed", user.Email, "The email could not be sent");
            }

        }

        // Always the same answer, whether the email exists or not
        return new AuthResponse { Success = true, Message = "If an account exists for that email, a reset code was sent." };
    }


    // Purpose: Checks the reset code and, if it is right, replaces the user's password.
    // Input:   request - the email, the code and the new password.
    // Output:  an AuthResponse: Success = true if the password was replaced, otherwise false with the reason
    //          (invalid or expired code, too many attempts, weak password).
    // Step 2 of "forgot my password": checks the code the user typed and, if it is right, sets the new password
    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        // Find the user by email. null = no such user
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // Exactly the same generic message, whether: the email doesn't exist, there is no active reset
        // request, or the code has expired - again, anti-enumeration: a possible attacker learns nothing
        // about why it failed. "||" = or; "< DateTime.UtcNow" = the expiry time has already passed
        if (user is null || user.ResetCodeHash is null || user.ResetCodeExpiresAt is null
            || user.ResetCodeExpiresAt < DateTime.UtcNow)

        {
            // The real reason goes only to the security log
            await _security.RecordAsync("Password reset failed", request.Email, "No active reset code for this email");
            return new AuthResponse { Success = false, Message = "Invalid or expired code." };
        }


        // Verify runs the same constant-time comparison that LoginAsync uses for passwords - here it
        // protects the code itself against a Timing Attack
        bool codeOk = PasswordHasher.Verify(request.Code, user.ResetCodeHash);

        // The code is wrong
        if (!codeOk)
        {
            // Count this wrong attempt
            user.ResetAttempts++;

            // Keep the number in a local variable, because the field may be set back to 0 just below
            int attempt = user.ResetAttempts;

            // true = this was the last allowed attempt
            bool locked = attempt >= MaxResetAttempts;

            // Too many wrong codes: a 6-digit code has only 1,000,000 options, so without a limit
            // it could simply be guessed
            if (locked)
            {
                // Cancel the code completely; the user has to ask for a new one
                user.ResetCodeHash = null;
                user.ResetCodeExpiresAt = null;
                user.ResetAttempts = 0;
            }

            // Save the new attempt count (or the cancellation)
            await _db.SaveChangesAsync();

            // Write the wrong attempt to the security log, for example "Wrong code (attempt 2 of 5)"
            await _security.RecordAsync("Password reset failed", user.Email, $"Wrong code (attempt {attempt} of {MaxResetAttempts})");

            // A second, separate log line when the code was cancelled
            if (locked)
            {
                await _security.RecordAsync("Reset code locked", user.Email, "Too many wrong codes - the code was cancelled");
            }

            // The same generic message as above
            return new AuthResponse { Success = false, Message = "Invalid or expired code." };
        }

        // Here, unlike the checks above, we DO return a specific message (why the password is weak) -
        // at this point the code was already proven correct, so there is no risk of User Enumeration any
        // more. This is exactly like RegisterAsync
        string? passwordError = PasswordPolicy.Validate(request.NewPassword, user.Username);

        // Not null = the new password is too weak; return the reason
        if (passwordError is not null)
            return new AuthResponse { Success = false, Message = passwordError };

        // Replace the stored hash with the hash of the new password
        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);

        // "Burn" the code after it was used - the same code can never be used a second time (single-use),
        // and any older reset request that was still active is cancelled too
        user.ResetCodeHash = null;
        user.ResetCodeExpiresAt = null;
        user.ResetAttempts = 0;

        // Typing a code that was sent to this address proves ownership of it, exactly like the
        // verification code does - so a successful reset also counts as verifying the email
        user.EmailVerified = true;

        // Save all the changes above
        await _db.SaveChangesAsync();

        // Write the password change to the security log
        await _security.RecordAsync("Password changed", user.Email, "Reset with an emailed code");


        // The password was replaced
        return new AuthResponse { Success = true, Message = "Password has been reset." };
    }

    // Purpose: Checks the email verification code and, if it is right, unlocks the account.
    // Input:   request - the email and the code the user typed.
    // Output:  an AuthResponse: Success = true if the email was verified, otherwise false ("Invalid or expired code.").
    // Checks the code a new user received by email, and if it is right unlocks the account
    public async Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest request)
    {
        // Find the user by email. null = no such user
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // One generic message for every reason this can fail (unknown email, already verified, no code,
        // expired code) - the same anti-enumeration idea as in ResetPasswordAsync
        if (user is null || user.EmailVerified || user.VerifyCodeHash is null
            || user.VerifyCodeExpiresAt is null || user.VerifyCodeExpiresAt < DateTime.UtcNow)
        {
            // The real reason goes only to the security log
            await _security.RecordAsync("Email verification failed", request.Email, "No active verification code for this email");
            return new AuthResponse { Success = false, Message = "Invalid or expired code." };
        }

        // Compare the typed code with the stored hash, in constant time
        bool codeOk = PasswordHasher.Verify(request.Code, user.VerifyCodeHash);

        // The code is wrong
        if (!codeOk)
        {
            // A 6-digit code has only 1,000,000 options - without a limit it could simply be guessed
            user.VerifyAttempts++;

            // Keep the number in a local variable, because the field may be set back to 0 just below
            int attempt = user.VerifyAttempts;

            // true = this was the last allowed attempt
            bool locked = attempt >= MaxVerifyAttempts;

            // Too many wrong codes
            if (locked)
            {
                // Cancel the code completely; the user has to ask for a new one
                user.VerifyCodeHash = null;
                user.VerifyCodeExpiresAt = null;
                user.VerifyAttempts = 0;
            }

            // Save the new attempt count (or the cancellation)
            await _db.SaveChangesAsync();

            // Write the wrong attempt to the security log
            await _security.RecordAsync("Email verification failed", user.Email, $"Wrong code (attempt {attempt} of {MaxVerifyAttempts})");

            // A second, separate log line when the code was cancelled
            if (locked)
            {
                await _security.RecordAsync("Verification code locked", user.Email, "Too many wrong codes - the code was cancelled");
            }

            // The same generic message as above
            return new AuthResponse { Success = false, Message = "Invalid or expired code." };
        }

        // The code is right: unlock the account
        user.EmailVerified = true;

        // The code is single-use: once it worked, it is erased
        user.VerifyCodeHash = null;
        user.VerifyCodeExpiresAt = null;
        user.VerifyAttempts = 0;

        // Save the changes
        await _db.SaveChangesAsync();

        // Write the verification to the security log
        await _security.RecordAsync("Email verified", user.Email, "Verified with an emailed code");

        // The account can be used now
        return new AuthResponse { Success = true, Message = "Email verified." };
    }

    // Purpose: Sends a new verification code to an account that is still waiting for verification.
    // Input:   request - the account's email.
    // Output:  an AuthResponse that always reports success, so it never reveals whether the account exists.
    // Sends a new verification code to a user who did not receive (or lost) the first one
    public async Task<AuthResponse> ResendVerificationAsync(ResendVerificationRequest request)
    {
        // Find the user by email. null = no such user
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // Only an existing account that is still waiting for verification can get a new code
        if (user is not null && !user.EmailVerified)
        {
            // A code lives 15 minutes, so "expires more than 14 minutes from now" means it was sent less
            // than a minute ago. Refusing to send another one that fast stops this action from being
            // used to flood someone's inbox
            bool sentRecently = user.VerifyCodeExpiresAt is not null
                && user.VerifyCodeExpiresAt > DateTime.UtcNow.AddMinutes(14);

            // At least a minute passed since the last code
            if (!sentRecently)
            {
                // Create, store and email a new code
                await SendVerificationCodeAsync(user);

                // Write it to the security log
                await _security.RecordAsync("Verification code resent", user.Email, "A new code was sent by email");
            }
        }

        // Always the same answer - whether the email exists, is already verified, or a code really was sent
        return new AuthResponse { Success = true, Message = "If that account is waiting for verification, a new code was sent." };
    }

    // Purpose: Creates a new verification code, saves its hash and expiry on the user, and emails the code.
    // Input:   user - the user to send the code to.
    // Output:  true if the email was sent, false if sending failed.
    // Creates a new verification code, stores only its hash (exactly like the reset code), and emails it.
    // Returns false if the email could not be sent
    private async Task<bool> SendVerificationCodeAsync(User user)
    {
        // A fresh random 6-digit code
        string code = GenerateResetCode();

        // Store only the hash of the code
        user.VerifyCodeHash = PasswordHasher.Hash(code);

        // The code is valid for 15 minutes from now
        user.VerifyCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);

        // A new code starts with zero wrong attempts
        user.VerifyAttempts = 0;

        // Save the three fields to the database
        await _db.SaveChangesAsync();

        // Sending an email can fail, so it runs inside "try"
        try
        {
            // Three arguments: who to send to, the subject, and the text of the email
            await _email.SendAsync(
                user.Email,
                "Your NetSim verification code",
                $"Your email verification code is: {code}\n\nThis code expires in 15 minutes. If you didn't create a NetSim account, you can ignore this email.");

            // The email went out
            return true;
        }
        // Any error from the mail server
        catch (Exception)
        {
            // A broken address or an unreachable mail server must not end the request with "Internal server error"
            await _security.RecordAsync("Verification email failed", user.Email, "The email could not be sent");

            // Tell the caller the code was not delivered
            return false;
        }
    }




    // Purpose: Creates a random 6-digit code.
    // Input:   nothing.
    // Output:  the code as text, with leading zeros kept (for example "004217").
    // Creates a random 6-digit code as text, for example "004217".
    // Used for both the reset code and the verification code
    private static string GenerateResetCode()
    {
        // RandomNumberGenerator (not the ordinary System.Random!) - a cryptographically secure source of randomness.
        // It must be used whenever the random value "guards" something (here - whoever knows the code can
        // reset someone else's password), because System.Random can be guessed/reproduced in some conditions.
        // GetInt32(0, 1_000_000) = a number from 0 to 999,999; ToString("D6") pads it with zeros to 6 digits
        return System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

}
