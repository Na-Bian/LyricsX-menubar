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

    /// <summary>
    /// Deserializes an array row by row: malformed entries become null so one
    /// broken remote row never aborts the valid ones.
    /// </summary>
    internal static List<T?> DeserializeRows<T>(string json) where T : class
    {
        var rows = new List<T?>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return rows;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                try
                {
                    rows.Add(element.Deserialize<T>(Options));
                }
                catch (JsonException)
                {
                    rows.Add(null);
                }
            }
        }
        catch (JsonException)
        {
        }

        return rows;
    }
}
