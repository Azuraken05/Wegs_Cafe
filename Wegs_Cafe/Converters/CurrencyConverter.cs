using System.Globalization;
using Wegs_Cafe.Helpers;

namespace Wegs_Cafe.Converters;

/// <summary>Formats a number as price, e.g. 120 -> "₱120.00".</summary>
public class CurrencyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? string.Empty : $"{Constants.CurrencySymbol}{System.Convert.ToDecimal(value):N2}";

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
