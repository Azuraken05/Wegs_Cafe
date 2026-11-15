using Newtonsoft.Json;

namespace Wegs_Cafe.Services;

internal static class JsonFile
{
    public static async Task<List<T>> ReadAsync<T>(string path)
        => File.Exists(path) ? JsonConvert.DeserializeObject<List<T>>(await File.ReadAllTextAsync(path)) ?? new() : new();

    public static Task WriteAsync<T>(string path, List<T> items)
        => File.WriteAllTextAsync(path, JsonConvert.SerializeObject(items, Formatting.Indented));
}
