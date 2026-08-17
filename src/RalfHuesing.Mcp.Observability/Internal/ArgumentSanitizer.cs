using System.Text.Json;
using System.Text.Json.Nodes;

namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Replaces values of well-known sensitive argument keys with a redaction marker
/// before they are written to the JSONL log.
/// Operates recursively on nested JSON objects.
/// </summary>
internal static class ArgumentSanitizer
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "pwd",
        "secret",
        "token",
        "apiKey",
        "apikey",
        "accessToken",
        "authorization",
        "connectionString",
        "privateKey"
    };

    /// <summary>
    /// Returns a sanitized copy of <paramref name="arguments"/>.
    /// The original dictionary is not modified.
    /// </summary>
    internal static IReadOnlyDictionary<string, JsonElement>? Sanitize(
        IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return arguments;
        }

        var result = new Dictionary<string, JsonElement>(arguments.Count, StringComparer.Ordinal);
        foreach (var (key, value) in arguments)
        {
            result[key] = SensitiveKeys.Contains(key)
                ? JsonSerializer.SerializeToElement(ObservabilityConstants.RedactedMarker)
                : SanitizeElement(value);
        }

        return result;
    }

    private static JsonElement SanitizeElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object && element.ValueKind != JsonValueKind.Array)
        {
            return element;
        }

        var node = JsonNode.Parse(element.GetRawText());
        if (node is not null)
        {
            SanitizeNode(node);
        }

        return JsonSerializer.SerializeToElement(node);
    }

    private static void SanitizeNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            SanitizeObject(obj);
        }
        else if (node is JsonArray arr)
        {
            SanitizeArray(arr);
        }
    }

    private static void SanitizeObject(JsonObject obj)
    {
        foreach (var key in obj.Select(kv => kv.Key).ToList())
        {
            if (SensitiveKeys.Contains(key))
            {
                obj[key] = JsonValue.Create(ObservabilityConstants.RedactedMarker);
            }
            else if (obj[key] is { } child)
            {
                SanitizeNode(child);
            }
        }
    }

    private static void SanitizeArray(JsonArray arr)
    {
        for (var i = 0; i < arr.Count; i++)
        {
            if (arr[i] is { } child)
            {
                SanitizeNode(child);
            }
        }
    }
}
