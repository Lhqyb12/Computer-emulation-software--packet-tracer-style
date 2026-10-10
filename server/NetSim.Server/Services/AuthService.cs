using Microsoft.EntityFrameworkCore;  // AnyAsync / FirstOrDefaultAsync - asynchronous DB queries
using NetSim.Server.Data;             // AppDbContext
using NetSim.Server.Dtos;             // RegisterRequest / LoginRequest / AuthResponse
using NetSim.Server.Models;           // User
using Google.Apis.Auth;


namespace NetSim.Server.Services;

// This is where all the business logic for register/login lives - deliberately kept separate from
// AuthController (responsible only for HTTP) and from AppDbContext (responsible only for DB access).
// This separation of concerns makes testing easier (AuthService can be tested without HTTP at all) and
// improves maintainability
public class AuthService
{
    private readonly AppDbContext _db;
    private readonly EmailSender _email;
    private readonly TokenService _tokens;

    private const int MaxResetAttempts = 5;
    private const int MaxVerifyAttempts = 5;

    private readonly IConfiguration _config;



    // Dependency Injection again: AppDbContext is injected by the DI container (registered as Scoped in
    // Program.cs), so this class doesn't know/care how the connection is actually created
    // Records security events (failed logins, password resets...) for the Super Admin dashboard
    private readonly SecurityLog _security;

    public AuthService(AppDbContext db, EmailSender email, TokenService tokens, IConfiguration config,
        SecurityLog security)
    {
        _db = db;
        _email = email;
        _tokens = tokens;
        _config = config;
        _security = security;
    }



    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Check #0: the email has to look like a real address. The client checks this too, but anyone can
        // call the API directly and skip the client - so the server never trusts that it was checked
        if (!EmailPolicy.IsValid(request.Email))
            return new AuthResponse { Success = false, Message = "Please enter a valid email address." };


        // Check #1: password policy - checked first and before any DB call on purpose (fail fast).
        // If the password is weak, there's no point spending a DB query checking for a duplicate email -
        // this saves load and response time
        string? passwordError = PasswordPolicy.Validate(request.Password, request.Username);
        if (passwordError is not null)
            return new AuthResponse { Success = false, Message = passwordError };

        // Check #2: duplicate email. AnyAsync generates a SQL "EXISTS(...)" query -
        // much more efficient than ToListAsync/FirstOrDefaultAsync, because the DB can stop at the first
        // match and doesn't need to return any actual columns, just true/false
        bool emailTaken = await _db.Users.AnyAsync(u => u.Email == request.Email);
        if (emailTaken)
            return new AuthResponse { Success = false, Message = "Email is already registered." };

        // Only after both checks pass do we hash the password. From this point on, "request.Password"
        // (the plain text) is no longer used in the code at all; the hash variable is the only thing that gets kept
        string hash = PasswordHasher.Hash(request.Password);

        // Creates a new Entity (User) - note that PasswordHash gets the hash, never the raw request.Password
        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = hash,
        };

        // Add only "marks" the Entity as Added in EF Core's Change Tracker - nothing has actually been sent to the DB yet
        _db.Users.Add(user);
        try
        {
            // Only here, in SaveChangesAsync, does EF Core actually build and run the INSERT statement against Postgres (inside a transaction)
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Extremely rare: two registrations with the same email arrived at almost the exact same
            // moment, and both passed the AnyAsync check above before either one finished saving. The
            // database's own unique index (see AppDbContext.OnModelCreating) is the real safety net here -
            // this catch just turns its rejection into the same friendly message as the normal case above,
            // instead of letting an unhandled exception crash the request with a 500
            return new AuthResponse { Success = false, Message = "Email is already registered." };
        }
        await _security.RecordAsync("Account created", user.Email, "Registered with a password - waiting for email verification");

        // The account exists now, but it is locked (EmailVerified = false) until the code is typed in
        bool sent = await SendVerificationCodeAsync(user);
        if (!sent)
        {
            // We could not deliver the code, so nobody could ever unlock this account. Deleting the row
            // lets the user fix the address (or simply try again) instead of hitting "already registered"
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return new AuthResponse { Success = false, Message = "We couldn't send an email to that address. Check it and try again." };
        }

        // Generic success message - we don't return, for example, the new user's internal Id, to avoid exposing unnecessary information
        return new AuthResponse { Success = true, Message = "Account created. Check your email for a verification code." };

    }

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
            await _security.RecordAsync("Login failed", request.Email, "Wrong password");
            return new AuthResponse { Success = false, Message = "Wrong email or password." };
        }
        
        // Checked only AFTER the password was proven correct - so this answer is given only to the person
        // who registered the account, and an attacker can't use it to find out which emails are registered
        if (!user.EmailVerified)
        {
            await SendVerificationCodeAsync(user);
            await _security.RecordAsync("Login blocked", user.Email, "Email not verified yet - a new code was sent");
            return new AuthResponse
            {
                Success = false,
                NeedsVerification = true,
                Message = "Please verify your email first. We sent you a new code.",
            };
        }


       user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new AuthResponse
        {
            Success = true,
            Message = "Signed in.",
            Role = user.Role,
            Token = _tokens.CreateToken(user),
        };


    }

    public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["Google:ClientId"]! }
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (InvalidJwtException)
        {
            await _security.RecordAsync("Google sign-in failed", null, "The Google token was rejected");
            return new AuthResponse { Success = false, Message = "Google sign-in failed." };
        }


        if (!payload.EmailVerified)
            return new AuthResponse { Success = false, Message = "Your Google email is not verified." };

        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Subject)
                   ?? await _db.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);

        if (user is null)
        {
            user = new User
            {
                Username = payload.Name ?? payload.Email,
                Email = payload.Email,
                PasswordHash = string.Empty,
                GoogleId = payload.Subject,
                // Google already checked this address (payload.EmailVerified above), so we don't ask again
                EmailVerified = true,

            };
            _db.Users.Add(user);
        }
        else if (user.GoogleId is null)
        {
            user.GoogleId = payload.Subject;
        }
        
        if (!user.EmailVerified)
        {
            // Someone registered this address with a password but never proved they own it - it may not
            // have been this person at all. Google has now proven who the real owner is, so the account
            // becomes verified and the unproven password is thrown away (an empty hash never matches)
            user.PasswordHash = string.Empty;
            user.VerifyCodeHash = null;
            user.VerifyCodeExpiresAt = null;
            user.EmailVerified = true;
        }


        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new AuthResponse
        {
            Success = true,
            Message = "Signed in with Google.",
            Email = user.Email,
            Role = user.Role,
            Token = _tokens.CreateToken(user),
        };

    }


    
    public async Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // אותו עיקרון בדיוק כמו ב-LoginAsync: בין אם האימייל קיים ובין אם לא, מחזירים תמיד את
        // אותה הודעה גנרית - אחרת התוקף יכול להשתמש בנקודת הקצה הזו כדי "לגלות" אילו אימיילים
        // בכלל רשומים אצלנו (User Enumeration), רק על ידי צפייה אם קיבל מייל או לא
        if (user is null)
        {
            // Many of these in a row, for different emails, is what "guessing which emails are
            // registered" looks like from the server's side
            await _security.RecordAsync("Password reset requested", request.Email, "Unknown email - no code was sent");
        }
        else
        {

            string code = GenerateResetCode();

            // בדיוק כמו סיסמה - שומרים רק hash של הקוד, לא את הקוד עצמו
            user.ResetCodeHash = PasswordHasher.Hash(code);
            user.ResetCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);
            user.ResetAttempts = 0;

            await _db.SaveChangesAsync();
            await _security.RecordAsync("Password reset requested", user.Email, "A reset code was sent by email");


            try
            {
                await _email.SendAsync(
                    user.Email,
                    "Your NetSim password reset code",
                    $"Your password reset code is: {code}\n\nThis code expires in 15 minutes. If you didn't request this, you can ignore this email.");
            }
            catch (Exception)
            {
                // Without this, a failed send crashed the request with a 500 - but only for emails that
                // ARE registered, which told an attacker exactly what the generic message below hides.
                // The failure is written to the owner's security log; the client sees no difference
                await _security.RecordAsync("Password reset email failed", user.Email, "The email could not be sent");
            }

        }

        return new AuthResponse { Success = true, Message = "If an account exists for that email, a reset code was sent." };
    }


    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // אותה הודעה גנרית בדיוק, בין אם: האימייל לא קיים, אין בקשת איפוס פעילה, או שהקוד פג תוקף -
        // שוב, אנטי-אנומרציה: לא לחשוף לתוקף פוטנציאלי אף פרט על למה זה נכשל
        if (user is null || user.ResetCodeHash is null || user.ResetCodeExpiresAt is null
            || user.ResetCodeExpiresAt < DateTime.UtcNow)

        {
            await _security.RecordAsync("Password reset failed", request.Email, "No active reset code for this email");
            return new AuthResponse { Success = false, Message = "Invalid or expired code." };
        }


        // Verify מריץ את אותה השוואה בזמן-קבוע (constant-time) שכבר מכירה מ-LoginAsync - מגנה גם כאן
        // מפני Timing Attack על הקוד עצמו
        bool codeOk = PasswordHasher.Verify(request.Code, user.ResetCodeHash);
        if (!codeOk)
        {
            user.ResetAttempts++;
            int attempt = user.ResetAttempts;
            bool locked = attempt >= MaxResetAttempts;
            if (locked)
            {
                user.ResetCodeHash = null;
                user.ResetCodeExpiresAt = null;
                user.ResetAttempts = 0;
            }
            await _db.SaveChangesAsync();

            await _security.RecordAsync("Password reset failed", user.Email, $"Wrong code (attempt {attempt} of {MaxResetAttempts})");
            if (locked)
            {
                await _security.RecordAsync("Reset code locked", user.Email, "Too many wrong codes - the code was cancelled");
            }
            return new AuthResponse { Success = false, Message = "Invalid or expired code." };
        }

        // כאן, בניגוד לבדיקות שלמעלה, כן מחזירים הודעה ספציפית (חולשת הסיסמה) - כי בשלב הזה כבר
        // אימתנו שהקוד נכון, אז אין כבר שום סיכון של User Enumeration - זה בדיוק כמו RegisterAsync
        string? passwordError = PasswordPolicy.Validate(request.NewPassword, user.Username);
        if (passwordError is not null)
            return new AuthResponse { Success = false, Message = passwordError };

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        // "שורפים" את הקוד אחרי שימוש - כך שאותו קוד לא יוכל לשמש שוב פעם שנייה (חד-פעמי),
        // וגם מבטלים כל בקשת איפוס ישנה שהייתה פעילה
        user.ResetCodeHash = null;
        user.ResetCodeExpiresAt = null;
        user.ResetAttempts = 0;
        // Typing a code that was sent to this address proves ownership of it, exactly like the
        // verification code does - so a successful reset also counts as verifying the email
        user.EmailVerified = true;

        await _db.SaveChangesAsync();
        await _security.RecordAsync("Password changed", user.Email, "Reset with an emailed code");


        return new AuthResponse { Success = true, Message = "Password has been reset." };
    }

        public async Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // One generic message for every reason this can fail (unknown email, already verified, no code,
        // expired code) - the same anti-enumeration idea as in ResetPasswordAsync
        if (user is null || user.EmailVerified || user.VerifyCodeHash is null
            || user.VerifyCodeExpiresAt is null || user.VerifyCodeExpiresAt < DateTime.UtcNow)
        {
            await _security.RecordAsync("Email verification failed", request.Email, "No active verification code for this email");
            return new AuthResponse { Success = false, Message = "Invalid or expired code." };
        }

        bool codeOk = PasswordHasher.Verify(request.Code, user.VerifyCodeHash);
        if (!codeOk)
        {
            // A 6-digit code has only 1,000,000 options - without a limit it could simply be guessed
            user.VerifyAttempts++;
            int attempt = user.VerifyAttempts;
            bool locked = attempt >= MaxVerifyAttempts;
            if (locked)
            {
                user.VerifyCodeHash = null;
                user.VerifyCodeExpiresAt = null;
                user.VerifyAttempts = 0;
            }
            await _db.SaveChangesAsync();

            await _security.RecordAsync("Email verification failed", user.Email, $"Wrong code (attempt {attempt} of {MaxVerifyAttempts})");
            if (locked)
            {
                await _security.RecordAsync("Verification code locked", user.Email, "Too many wrong codes - the code was cancelled");
            }
            return new AuthResponse { Success = false, Message = "Invalid or expired code." };
        }

        user.EmailVerified = true;
        // The code is single-use: once it worked, it is erased
        user.VerifyCodeHash = null;
        user.VerifyCodeExpiresAt = null;
        user.VerifyAttempts = 0;
        await _db.SaveChangesAsync();
        await _security.RecordAsync("Email verified", user.Email, "Verified with an emailed code");

        return new AuthResponse { Success = true, Message = "Email verified." };
    }

    public async Task<AuthResponse> ResendVerificationAsync(ResendVerificationRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is not null && !user.EmailVerified)
        {
            // A code lives 15 minutes, so "expires more than 14 minutes from now" means it was sent less
            // than a minute ago. Refusing to send another one that fast stops this endpoint from being
            // used to flood someone's inbox
            bool sentRecently = user.VerifyCodeExpiresAt is not null
                && user.VerifyCodeExpiresAt > DateTime.UtcNow.AddMinutes(14);

            if (!sentRecently)
            {
                await SendVerificationCodeAsync(user);
                await _security.RecordAsync("Verification code resent", user.Email, "A new code was sent by email");
            }
        }

        // Always the same answer - whether the email exists, is already verified, or a code really was sent
        return new AuthResponse { Success = true, Message = "If that account is waiting for verification, a new code was sent." };
    }

    // Creates a new verification code, stores only its hash (exactly like the reset code), and emails it.
    // Returns false if the email could not be sent
    private async Task<bool> SendVerificationCodeAsync(User user)
    {
        string code = GenerateResetCode();

        user.VerifyCodeHash = PasswordHasher.Hash(code);
        user.VerifyCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);
        user.VerifyAttempts = 0;
        await _db.SaveChangesAsync();

        try
        {
            await _email.SendAsync(
                user.Email,
                "Your NetSim verification code",
                $"Your email verification code is: {code}\n\nThis code expires in 15 minutes. If you didn't create a NetSim account, you can ignore this email.");
            return true;
        }
        catch (Exception)
        {
            // A broken address or an unreachable mail server must not crash the request with a 500
            await _security.RecordAsync("Verification email failed", user.Email, "The email could not be sent");
            return false;
        }
    }




    private static string GenerateResetCode()
    {
        // RandomNumberGenerator (לא System.Random הרגיל!) - מקור אקראיות קריפטוגרפי מאובטח.
        // חובה להשתמש בו כל פעם שהערך האקראי "שומר" על משהו (כאן - מי שיודע את הקוד יכול לאפס
        // את הסיסמה של מישהו אחר), כי System.Random ניתן לניחוש/שחזור בתנאים מסוימים
        return System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

}
