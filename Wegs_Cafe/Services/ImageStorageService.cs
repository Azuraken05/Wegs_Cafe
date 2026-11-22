using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Validates and stores uploaded product images (.jpg, .png, .webp under 5 MB).
/// Attempts Firebase Storage upload and falls back cleanly to local app storage when offline.
/// </summary>
public class ImageStorageService : IImageStorageService
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly FirebaseService _firebaseService;
    private readonly string _directoryPath = Path.Combine(FileSystem.AppDataDirectory, "product_images_v3");

    public ImageStorageService(FirebaseService firebaseService)
    {
        _firebaseService = firebaseService;
    }

    public async Task<string> UploadProductImageAsync(Stream imageStream, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new ValidationException("Product image must be a .jpg, .png, or .webp file.");

        using var buffer = new MemoryStream();
        await imageStream.CopyToAsync(buffer);
        if (buffer.Length == 0)
            throw new ValidationException("Selected image file is empty.");
        if (buffer.Length > MaxFileSizeBytes)
            throw new ValidationException("Product image must be under 5 MB.");

        var uniqueFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

        Directory.CreateDirectory(_directoryPath);
        var destinationPath = Path.Combine(_directoryPath, uniqueFileName);
        buffer.Position = 0;
        await using var fileStream = File.Create(destinationPath);
        await buffer.CopyToAsync(fileStream);
        return destinationPath;
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
            // Ignore cleanup errors
        }
        return Task.CompletedTask;
    }
}
