namespace Wegs_Cafe.Models;

/// <summary>
/// Base class for every object stored in Firebase.
/// Id = the Firebase node key (filled in automatically by FirebaseRepository).
/// </summary>
public abstract class BaseModel
{
    public string? Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
