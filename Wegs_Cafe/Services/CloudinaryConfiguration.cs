namespace Wegs_Cafe.Services;

/// <summary>
/// Cloudinary unsigned image upload configuration for product catalog photos.
/// Images are downsized to max 600x600 JPEG (80% quality) before upload.
/// </summary>
public static class CloudinaryConfiguration
{
    public const string CloudName = "wexg4z0y";
    public const string UnsignedUploadPreset = "wegs_cafe_products_unsigned";
    public const string UploadEndpoint = "https://api.cloudinary.com/v1_1/wexg4z0y/image/upload";
    public const int MaxImageDimension = 600;
    public const int JpegQuality = 80;
}
