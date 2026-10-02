namespace LyricsX.Core.Storage;

public sealed class ManualAssociationStore(string rootDirectory)
{
    private const string FileName = "associations.json";
    private readonly JsonFileStore<Dictionary<string, string>> store =
        new(rootDirectory, () => []);

    public async Task<string?> TryResolveAsync(
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var map = await store.LoadAsync(FileName, cancellationToken);
        return map.TryGetValue(fingerprint, out var cacheKey) ? cacheKey : null;
    }

    public async Task SetAsync(
        string fingerprint,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var map = await store.LoadAsync(FileName, cancellationToken);
        map[fingerprint] = cacheKey;
        await store.SaveAsync(FileName, map, cancellationToken);
    }

    public async Task RemoveAsync(string fingerprint, CancellationToken cancellationToken)
    {
        var map = await store.LoadAsync(FileName, cancellationToken);
        if (map.Remove(fingerprint))
        {
            await store.SaveAsync(FileName, map, cancellationToken);
        }
    }
}
