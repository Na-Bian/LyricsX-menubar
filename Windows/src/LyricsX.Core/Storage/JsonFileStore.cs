using System.Text.Json;
using System.Text.Json.Serialization;

namespace LyricsX.Core.Storage;

public sealed class JsonFileStore<T>(string rootDirectory, Func<T> defaultFactory)
    where T : class
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<T> LoadAsync(string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Join(rootDirectory, fileName);
        if (!File.Exists(path))
        {
            return defaultFactory();
        }

        try
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, FileOptions.Asynchronous);
            return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken)
                ?? defaultFactory();
        }
        catch (JsonException)
        {
            var backups = Path.Join(rootDirectory, "backups");
            Directory.CreateDirectory(backups);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'.'fff'Z'");
            var backupPath = Path.Join(
                backups,
                $"{Path.GetFileNameWithoutExtension(fileName)}-{stamp}.invalid.json");
            File.Move(path, backupPath);
            return defaultFactory();
        }
    }

    public async Task SaveAsync(string fileName, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(rootDirectory);
        var path = Path.Join(rootDirectory, fileName);
        var tempPath = path + ".tmp";
        await using (var stream = new FileStream(
            tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 4096, FileOptions.WriteThrough | FileOptions.Asynchronous))
        {
            await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken);
        }

        File.Move(tempPath, path, true);
    }
}
