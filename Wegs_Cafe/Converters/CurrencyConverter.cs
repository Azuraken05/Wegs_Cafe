using System.Globalization;

namespace Wegs_Cafe.Converters;

/// <summary>Formats a decimal value as Philippine Peso currency (e.g. 120 -> "₱120.00").</summary>
public class CurrencyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? string.Empty : $"₱{System.Convert.ToDecimal(value):N2}";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Inverts a boolean value.</summary>
public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool booleanValue && !booleanValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool booleanValue && !booleanValue;
}

/// <summary>Returns true when a string is not null or whitespace.</summary>
public class StringNotEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Returns true when a string is null or whitespace.</summary>
public class StringEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts an image_url (Cloudinary HTTP URL or local file path) into a cached MAUI ImageSource.
/// Injects Cloudinary on-the-fly thumbnail transformation (/upload/w_300,h_300,c_fill,q_auto,f_auto/)
/// and enables 30-day device disk caching.
/// </summary>
public class ImagePathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value as string;
        if (string.IsNullOrWhiteSpace(path)) return null;

        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var transformedUrl = InjectCloudinaryThumbnail(path);
            return new UriImageSource
            {
                Uri = new Uri(transformedUrl),
                CachingEnabled = true,
                CacheValidity = TimeSpan.FromDays(30)
            };
        }

        return File.Exists(path) ? ImageSource.FromFile(path) : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public static string InjectCloudinaryThumbnail(string url)
    {
        const string uploadSegment = "/upload/";
        const string transformSegment = "/upload/w_300,h_300,c_fill,q_auto,f_auto/";

        if (url.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase) &&
            url.Contains(uploadSegment, StringComparison.OrdinalIgnoreCase) &&
            !url.Contains("w_300,h_300", StringComparison.OrdinalIgnoreCase))
        {
            var index = url.IndexOf(uploadSegment, StringComparison.OrdinalIgnoreCase);
            return url[..index] + transformSegment + url[(index + uploadSegment.Length)..];
        }

        return url;
    }
}
