namespace Wegs_Cafe.Models;

public class Category : BaseModel
{
    public string Name { get; set; } = string.Empty;   // e.g. "Coffee", "Pastries"
    public string? IconUrl { get; set; }
    public int SortOrder { get; set; }
}
