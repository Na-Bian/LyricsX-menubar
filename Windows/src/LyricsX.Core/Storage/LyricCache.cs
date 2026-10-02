using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LyricsX.Core.Parsing;

namespace LyricsX.Core.Storage;

public sealed class LyricCache(string rootDirectory)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string cacheDirectory = Path.Join(rootDirectory, "cache");
    private readonly LrcParser parser = new();
    private readonly LrcSerializer serializer = new();

    public async Task StoreAsync(
        string fingerprint,
        LyricCandidate candidate,
        string originalQuery,
        double score,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(cacheDirectory);
        var baseName = ToFileName(fingerprint);
        var lrc = serializer.Serialize(candidate.Document);
        await File.WriteAllTextAsync(
            Path.Join(cacheDirectory, baseName + ".lrc"),
            lrc,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        var entry = new CacheEntry(
            candidate.ProviderId,
            originalQuery,
            score,
            DateTime.UtcNow,
            fingerprint);
        await File.WriteAllTextAsync(
            Path.Join(cacheDirectory, baseName + ".json"),
            JsonSerializer.Serialize(entry, Options),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    public async Task<LyricDocument?> TryGetAsync(
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var path = Path.Join(cacheDirectory, ToFileName(fingerprint) + ".lrc");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return parser.Parse(await File.ReadAllTextAsync(path, cancellationToken));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string ToFileName(string fingerprint) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)))
            .ToLowerInvariant();

    private sealed record CacheEntry(
        string ProviderId,
        string OriginalQuery,
        double Score,
        DateTime RetrievedAtUtc,
        string Fingerprint);
}
