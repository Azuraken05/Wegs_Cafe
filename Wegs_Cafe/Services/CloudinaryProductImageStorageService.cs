using System.Net.Http.Headers;
using System.Text.Json;
using SkiaSharp;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Validates product images (.jpg, .png, .webp under 5 MB), downsizes to max 600x600 JPEG at 80% quality,
/// and uploads to Cloudinary's unsigned endpoint, returning the public secure_url.
/// </summary>
public class CloudinaryProductImageStorageService : IImageStorageService
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly string _localFallbackDirectory = Path.Combine(FileSystem.AppDataDirectory, "product_images_v3");

    public async Task<string> UploadProductImageAsync(Stream imageStream, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new ValidationException("Product image must be a .jpg, .png, or .webp file.");

        using var rawBuffer = new MemoryStream();
        await imageStream.CopyToAsync(rawBuffer);
        if (rawBuffer.Length == 0)
            throw new ValidationException("Selected image file is empty.");
        if (rawBuffer.Length > MaxFileSizeBytes)
            throw new ValidationException("Product image must be under 5 MB.");

        rawBuffer.Position = 0;
        var compressedJpegBytes = await Task.Run(() => ResizeAndCompressToJpeg(rawBuffer));
        var uploadFileName = $"{Guid.NewGuid():N}.jpg";

        try
        {
            using var formContent = new MultipartFormDataContent();
            formContent.Add(new StringContent(CloudinaryConfiguration.UnsignedUploadPreset), "upload_preset");
            formContent.Add(new StringContent("wegs_cafe_products"), "folder");

            var fileContent = new ByteArrayContent(compressedJpegBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            formContent.Add(fileContent, "file", uploadFileName);

            using var response = await SharedHttpClient.PostAsync(CloudinaryConfiguration.UploadEndpoint, formContent);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("secure_url", out var secureUrlProp))
                {
                    var secureUrl = secureUrlProp.GetString();
                    if (!string.IsNullOrWhiteSpace(secureUrl))
                        return secureUrl;
                }
            }
        }
        catch
        {
            // Fall back to local compressed JPEG storage if offline or Cloudinary is unreachable
        }

        Directory.CreateDirectory(_localFallbackDirectory);
        var localPath = Path.Combine(_localFallbackDirectory, uploadFileName);
        await File.WriteAllBytesAsync(localPath, compressedJpegBytes);
        return localPath;
    }

    public Task DeleteProductImageAsync(string imageUrl)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(imageUrl) &&
                !imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(imageUrl))
            {
                File.Delete(imageUrl);
            }
        }
        catch
        {
            // Ignore local file cleanup errors
        }
        return Task.CompletedTask;
    }

    private static byte[] ResizeAndCompressToJpeg(Stream inputStream)
    {
        using var original = SKBitmap.Decode(inputStream)
            ?? throw new ValidationException("Unable to decode the selected image file.");

        var maxDim = CloudinaryConfiguration.MaxImageDimension;
        var targetWidth = original.Width;
        var targetHeight = original.Height;

        if (original.Width > maxDim || original.Height > maxDim)
        {
            var scale = Math.Min((float)maxDim / original.Width, (float)maxDim / original.Height);
            targetWidth = Math.Max(1, (int)Math.Round(original.Width * scale));
            targetHeight = Math.Max(1, (int)Math.Round(original.Height * scale));
        }

        using var resized = (targetWidth != original.Width || targetHeight != original.Height)
            ? original.Resize(new SKImageInfo(targetWidth, targetHeight), SKSamplingOptions.Default)
            : original;

        using var image = SKImage.FromBitmap(resized ?? original);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, CloudinaryConfiguration.JpegQuality);
        return encoded.ToArray();
    }
}
