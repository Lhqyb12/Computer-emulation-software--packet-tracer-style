using Microsoft.EntityFrameworkCore;  // AnyAsync / FirstOrDefaultAsync - asynchronous DB queries
using NetSim.Server.Data;             // AppDbContext
using NetSim.Server.Dtos;             // RegisterRequest / LoginRequest / AuthResponse
using NetSim.Server.Models;           // User

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


    // Dependency Injection again: AppDbContext is injected by the DI container (registered as Scoped in
    // Program.cs), so this class doesn't know/care how the connection is actually created
    public AuthService(AppDbContext db, EmailSender email, TokenService tokens)
    {
        _db = db;
        _email = email;
        _tokens = tokens;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
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

        // Generic success message - we don't return, for example, the new user's internal Id, to avoid exposing unnecessary information
        return new AuthResponse { Success = true, Message = "Account created." };

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
            return new AuthResponse { Success = false, Message = "Wrong email or password." };

        // Verify recomputes PBKDF2 on the entered password, using the same salt and iteration count stored in
        // user.PasswordHash, and compares in constant time (see PasswordHasher.Verify) - again, the original
        // password is never "decrypted"
        bool ok = PasswordHasher.Verify(request.Password, user.PasswordHash);
        // The exact same generic message as the "user doesn't exist" case - the consistency of the message is the important part here
        if (!ok)
            return new AuthResponse { Success = false, Message = "Wrong email or password." };

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

    
    public async Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // אותו עיקרון בדיוק כמו ב-LoginAsync: בין אם האימייל קיים ובין אם לא, מחזירים תמיד את
        // אותה הודעה גנרית - אחרת התוקף יכול להשתמש בנקודת הקצה הזו כדי "לגלות" אילו אימיילים
        // בכלל רשומים אצלנו (User Enumeration), רק על ידי צפייה אם קיבל מייל או לא
        if (user is not null)
        {
            string code = GenerateResetCode();

            // בדיוק כמו סיסמה - שומרים רק hash של הקוד, לא את הקוד עצמו
            user.ResetCodeHash = PasswordHasher.Hash(code);
            user.ResetCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);
            user.ResetAttempts = 0;

            await _db.SaveChangesAsync();

            await _email.SendAsync(
                user.Email,
                "Your NetSim password reset code",
                $"Your password reset code is: {code}\n\nThis code expires in 15 minutes. If you didn't request this, you can ignore this email.");
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
            return new AuthResponse { Success = false, Message = "Invalid or expired code." };

        // Verify מריץ את אותה השוואה בזמן-קבוע (constant-time) שכבר מכירה מ-LoginAsync - מגנה גם כאן
        // מפני Timing Attack על הקוד עצמו
        bool codeOk = PasswordHasher.Verify(request.Code, user.ResetCodeHash);
        if (!codeOk)
        {
            user.ResetAttempts++;
            if (user.ResetAttempts >= MaxResetAttempts)
            {
                user.ResetCodeHash = null;
                user.ResetCodeExpiresAt = null;
                user.ResetAttempts = 0;
            }
            await _db.SaveChangesAsync();
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
        await _db.SaveChangesAsync();

        return new AuthResponse { Success = true, Message = "Password has been reset." };
    }


    private static string GenerateResetCode()
    {
        // RandomNumberGenerator (לא System.Random הרגיל!) - מקור אקראיות קריפטוגרפי מאובטח.
        // חובה להשתמש בו כל פעם שהערך האקראי "שומר" על משהו (כאן - מי שיודע את הקוד יכול לאפס
        // את הסיסמה של מישהו אחר), כי System.Random ניתן לניחוש/שחזור בתנאים מסוימים
        return System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

}
