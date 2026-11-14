namespace Wegs_Cafe.Models;

/// <summary>
/// Profile data stored under /users/{uid}. Id = Firebase Auth UID.
/// (Roles can be added here later, e.g. a "Role" property.)
/// </summary>
public class AppUser : BaseModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? PhotoUrl { get; set; }
}
