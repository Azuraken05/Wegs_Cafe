using System.Globalization;

namespace Wegs_Cafe.Models;

/// <summary>
/// Formats and parses all timestamps as ISO 8601 UTC strings (yyyy-MM-ddTHH:mm:ssZ)
/// for full interoperability with PHP / Laravel Carbon::parse().
/// </summary>
public static class IsoTimestamp
{
    public const string Format = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public static string Now() => DateTime.UtcNow.ToString(Format, CultureInfo.InvariantCulture);

    public static string From(DateTime dateTime)
        => dateTime.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture);

    public static DateTime? ParseUtc(string? isoString)
        => DateTime.TryParse(
            isoString,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;

    public static DateTime ParseOrDefault(string? isoString) => ParseUtc(isoString) ?? DateTime.MinValue;

    public static DateTime? ToLocal(string? isoString) => ParseUtc(isoString)?.ToLocalTime();
}
