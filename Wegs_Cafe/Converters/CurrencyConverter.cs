using System.Globalization;

namespace Wegs_Cafe.Converters;

/// <summary>Formats a number as price, e.g. 120 -> "â‚±120.00".</summary>
public class CurrencyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? string.Empty : $"₱{System.Convert.ToDecimal(value):N2}";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Inverts a bool (e.g. IsBusy -> IsEnabled).</summary>
public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}


/// <summary>True when the string has text (used to show/hide message labels).</summary>
public class StringNotEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>image_url -> ImageSource (http URL or local file path). Empty = no image.</summary>
public class ImagePathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value as string;
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return ImageSource.FromUri(new Uri(path));
        return File.Exists(path) ? ImageSource.FromFile(path) : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>True when the string has NO text (shows placeholders).</summary>
public class StringEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
