using Microsoft.Maui.Controls.Shapes;

namespace Wegs_Cafe.Helpers;

/// <summary>
/// Attached property that assigns a fresh RoundRectangle StrokeShape instance per Border
/// so Styles never share a single Shape BindableObject across multiple Borders on Android.
/// </summary>
public static class BorderStyleHelper
{
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.CreateAttached(
        "CornerRadius",
        typeof(double),
        typeof(BorderStyleHelper),
        0d,
        propertyChanged: OnCornerRadiusChanged);

    public static double GetCornerRadius(BindableObject view) => (double)view.GetValue(CornerRadiusProperty);
    public static void SetCornerRadius(BindableObject view, double value) => view.SetValue(CornerRadiusProperty, value);

    private static void OnCornerRadiusChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is Border border && newValue is double radius && radius > 0)
        {
            border.StrokeShape = new RoundRectangle { CornerRadius = radius };
        }
    }
}
