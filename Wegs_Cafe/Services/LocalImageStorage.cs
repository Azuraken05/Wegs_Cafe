namespace Wegs_Cafe.Services;

/// <summary>Saves product photos in the app's private storage. Returned value = image_url (a local path for now).</summary>
public class LocalImageStorage : IImageStorageService
{
    private readonly string _folder = Path.Combine(FileSystem.AppDataDirectory, "product_images");

    public async Task<string> SaveProductImageAsync(Stream image, string fileName)
    {
        Directory.CreateDirectory(_folder);
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        var path = Path.Combine(_folder, $"{Guid.NewGuid():N}{ext}");
        await using var file = File.Create(path);
        await image.CopyToAsync(file);
        return path;
    }

    public Task DeleteAsync(string imageUrl)
    {
        try
        {
            if (!imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) && File.Exists(imageUrl))
                File.Delete(imageUrl);
        }
        catch { /* best effort */ }
        return Task.CompletedTask;
    }
}

