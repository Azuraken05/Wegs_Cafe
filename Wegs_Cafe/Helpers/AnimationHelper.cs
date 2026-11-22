namespace Wegs_Cafe.Helpers;

/// <summary>
/// Fluid 60fps UI animations using .NET MAUI Animation API.
/// Used across Dashboard cards, Point of Sale product tiles, Cart drawers, and Low-Stock Alert banners.
/// </summary>
public static class AnimationHelper
{
    /// <summary>Subtle staggered FadeTo + TranslateTo slide-up (150ms–250ms, CubicOut) when cards or grids load.</summary>
    public static async Task AnimateEntranceAsync(VisualElement element, uint durationMilliseconds = 220, int delayMilliseconds = 0)
    {
        if (element == null) return;
        try
        {
            element.Opacity = 0;
            element.TranslationY = 18;
            if (delayMilliseconds > 0)
                await Task.Delay(delayMilliseconds);

            await Task.WhenAll(
                element.FadeToAsync(1.0, durationMilliseconds, Easing.CubicOut),
                element.TranslateToAsync(0, 0, durationMilliseconds, Easing.CubicOut));
        }
        catch
        {
            element.Opacity = 1.0;
            element.TranslationY = 0;
        }
    }

    /// <summary>Tactile ScaleTo(0.96, 80, Easing.CubicInOut) spring-back effect when pressing buttons or product tiles.</summary>
    public static async Task AnimatePressAsync(VisualElement? element)
    {
        if (element == null) return;
        await element.ScaleToAsync(0.96, 80, Easing.CubicInOut);
        await element.ScaleToAsync(1.0, 90, Easing.CubicOut);
    }

    /// <summary>Smooth bottom-sheet / slide-in drawer open transition with backdrop dim fade.</summary>
    public static async Task OpenDrawerAsync(VisualElement backdrop, VisualElement drawer)
    {
        backdrop.Opacity = 0;
        drawer.TranslationY = 420;
        await Task.WhenAll(
            backdrop.FadeToAsync(0.5, 200, Easing.CubicOut),
            drawer.TranslateToAsync(0, 0, 220, Easing.CubicOut));
    }

    /// <summary>Smooth bottom-sheet / slide-in drawer close transition.</summary>
    public static async Task CloseDrawerAsync(VisualElement backdrop, VisualElement drawer)
    {
        await Task.WhenAll(
            backdrop.FadeToAsync(0, 180, Easing.CubicIn),
            drawer.TranslateToAsync(0, 420, 180, Easing.CubicIn));
    }

    /// <summary>Gentle pulse/attention slide-down animation when low-stock notifications appear on the Dashboard.</summary>
    public static async Task AnimateAlertBannerAsync(VisualElement banner)
    {
        if (banner == null) return;
        banner.Opacity = 0;
        banner.TranslationY = -14;
        await Task.WhenAll(
            banner.FadeToAsync(1.0, 220, Easing.CubicOut),
            banner.TranslateToAsync(0, 0, 220, Easing.CubicOut));
        await banner.ScaleToAsync(1.015, 120, Easing.CubicInOut);
        await banner.ScaleToAsync(1.0, 120, Easing.CubicOut);
    }
}
