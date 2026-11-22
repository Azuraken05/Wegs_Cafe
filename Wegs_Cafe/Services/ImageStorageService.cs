namespace Wegs_Cafe.Services;

/// <summary>
/// Stores uploaded product images and returns their image_url string.
/// Ready to switch to FirebaseStorageClient when live cloud upload is enabled.
/// </summary>
public class ImageStorageService : IImageStorageService
{
    private readonly string _directoryPath = Path.Combine(FileSystem.AppDataDirectory, "product_images_v2");

    public async Task<string> UploadProductImageAsync(Stream imageStream, string fileName)
    {
        Directory.CreateDirectory(_directoryPath);
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".jpg";

        var destinationPath = Path.Combine(_directoryPath, $"{Guid.NewGuid():N}{extension}");
        await using var fileStream = File.Create(destinationPath);
        await imageStream.CopyToAsync(fileStream);
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
