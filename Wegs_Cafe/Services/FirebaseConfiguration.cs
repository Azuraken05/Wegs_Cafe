namespace Wegs_Cafe.Services;

/// <summary>
/// Centralized Firebase Realtime Database and Firebase Authentication configuration.
/// Firebase Storage is disabled on the Spark plan; product images use Cloudinary.
/// </summary>
public static class FirebaseConfiguration
{
    public const string RealtimeDatabaseUrl = "https://wegs-cafe-pos-default-rtdb.asia-southeast1.firebasedatabase.app/";
    public const string WebApiKey = "PASTE_YOUR_FIREBASE_WEB_API_KEY_HERE";

    public static bool IsWebApiKeyConfigured =>
        !string.IsNullOrWhiteSpace(WebApiKey) &&
        !WebApiKey.Contains("PASTE_YOUR_FIREBASE_WEB_API_KEY_HERE", StringComparison.OrdinalIgnoreCase);
}
