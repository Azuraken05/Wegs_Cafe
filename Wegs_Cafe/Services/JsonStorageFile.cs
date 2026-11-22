using Newtonsoft.Json;

namespace Wegs_Cafe.Services;

internal static class JsonStorageFile
{
    public static async Task<List<T>> ReadListAsync<T>(string filePath)
        => File.Exists(filePath)
            ? JsonConvert.DeserializeObject<List<T>>(await File.ReadAllTextAsync(filePath)) ?? new List<T>()
            : new List<T>();

    public static Task WriteListAsync<T>(string filePath, List<T> items)
        => File.WriteAllTextAsync(filePath, JsonConvert.SerializeObject(items, Formatting.Indented));
}
