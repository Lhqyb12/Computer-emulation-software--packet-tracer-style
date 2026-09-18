namespace NetSim.Server.Dtos;

// DTO שמתאר "שורה" במסך ניהול המשתמשים - בכוונה בלי PasswordHash,
// בדיוק מאותה סיבה כמו ב-AuthResponse: אין שום צורך שהמידע הרגיש הזה יצא מהשרת בכלל
public class UserSummaryDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string Role { get; set; } = string.Empty;
}
