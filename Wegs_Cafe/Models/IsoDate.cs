namespace Wegs_Cafe.Models;

/// <summary>
/// All timestamps are ISO 8601 UTC strings (yyyy-MM-ddTHH:mm:ssZ) so PHP/Laravel
/// can parse them with Carbon::parse(). Never store DateTime directly.
/// </summary>
public static class IsoDate
{
    public const string Format = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public static string Now() => DateTime.UtcNow.ToString(Format, System.Globalization.CultureInfo.InvariantCulture);

    public static string From(DateTime dt) => dt.ToUniversalTime().ToString(Format, System.Globalization.CultureInfo.InvariantCulture);

    public static DateTime? Parse(string? iso)
        => DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    public static DateTime? ToLocal(string? iso) => Parse(iso)?.ToLocalTime();
}
