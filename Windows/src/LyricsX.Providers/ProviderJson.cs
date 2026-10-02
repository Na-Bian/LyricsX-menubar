using System.Text.Json;
using System.Text.Json.Serialization;

namespace LyricsX.Providers;

public static class ProviderJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}
