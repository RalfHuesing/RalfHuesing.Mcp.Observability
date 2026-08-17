using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RalfHuesing.Mcp.Observability.Internal;

/// <summary>
/// Replaces values of well-known sensitive argument keys with a redaction marker
/// before they are written to the JSONL log.
/// Operates recursively on nested JSON objects and arrays, on plain
/// <see cref="IDictionary{TKey, TValue}"/>-shaped arguments, and on free-form
/// response strings.
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

    private static HashSet<string> BuildKeySet(IEnumerable<string>? additional)
    {
        var keys = new HashSet<string>(SensitiveKeys, StringComparer.OrdinalIgnoreCase);
        if (additional is not null)
        {
            foreach (var key in additional)
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    /// <summary>
    /// Returns a sanitized copy of <paramref name="rawArguments"/>. The original
    /// input is not modified. Accepts the SDK's
    /// <c>IReadOnlyDictionary&lt;string, JsonElement&gt;</c>, plain
    /// <c>IReadOnlyDictionary&lt;string, object?&gt;</c>, a
    /// <see cref="JsonObject"/>, or any <see cref="IDictionary{TKey, TValue}"/>
    /// with <c>string</c> keys. Unknown input shapes return an empty dictionary
    /// (defensive fallback — the SDK contract is well-defined).
    /// </summary>
    internal static IReadOnlyDictionary<string, object?>? Sanitize(
        object? rawArguments,
        IEnumerable<string>? additionalKeys = null)
    {
        if (rawArguments is null)
        {
            return null;
        }

        var keys = BuildKeySet(additionalKeys);
        return rawArguments switch
        {
            IReadOnlyDictionary<string, JsonElement> typed => SanitizeFromJsonElementDict(typed, keys),
            IReadOnlyDictionary<string, object?> objDict => SanitizeFromObjectDict(objDict, keys),
            JsonObject jsonObj => SanitizeFromJsonObject(jsonObj, keys),
            IDictionary<string, object?> anyDict => SanitizeFromObjectDict(anyDict, keys),
            _ => new Dictionary<string, object?>()
        };
    }

    /// <summary>
    /// Replaces <c>key=value</c> and <c>"key":"value"</c> occurrences in
    /// <paramref name="rawText"/> for every sensitive key (built-in defaults
    /// merged with <paramref name="additionalKeys"/>). The input is not
    /// modified.
    /// </summary>
    internal static string? Sanitize(string? rawText, IEnumerable<string>? additionalKeys = null)
    {
        if (string.IsNullOrEmpty(rawText))
        {
            return rawText;
        }

        var keys = BuildKeySet(additionalKeys);
        var result = rawText;
        foreach (var key in keys)
        {
            var escaped = Regex.Escape(key);
            result = Regex.Replace(
                result,
                $@"\b{escaped}\s*=\s*[^\s,;]+",
                $"{key}={ObservabilityConstants.RedactedMarker}",
                RegexOptions.IgnoreCase);
            result = Regex.Replace(
                result,
                $"(\"{escaped}\"\\s*:\\s*\")[^\"]+(\")",
                $"$1{ObservabilityConstants.RedactedMarker}$2",
                RegexOptions.IgnoreCase);
        }

        return result;
    }

    private static Dictionary<string, object?> SanitizeFromJsonElementDict(
        IReadOnlyDictionary<string, JsonElement> dict,
        HashSet<string> keys)
    {
        if (dict.Count == 0)
        {
            return new Dictionary<string, object?>();
        }

        var result = new Dictionary<string, object?>(dict.Count, StringComparer.Ordinal);
        foreach (var (key, value) in dict)
        {
            result[key] = keys.Contains(key)
                ? ObservabilityConstants.RedactedMarker
                : SanitizeJsonElement(value, keys);
        }

        return result;
    }

    private static Dictionary<string, object?> SanitizeFromObjectDict(
        IEnumerable<KeyValuePair<string, object?>> dict,
        HashSet<string> keys)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in dict)
        {
            result[key] = keys.Contains(key)
                ? ObservabilityConstants.RedactedMarker
                : SanitizeObjectValue(value, keys);
        }

        return result;
    }

    private static Dictionary<string, object?> SanitizeFromJsonObject(
        JsonObject jsonObj,
        HashSet<string> keys)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var kvp in jsonObj)
        {
            result[kvp.Key] = keys.Contains(kvp.Key)
                ? ObservabilityConstants.RedactedMarker
                : SanitizeNode(kvp.Value, keys);
        }

        return result;
    }

    // Direct JsonElement traversal — no JsonNode.Parse / SerializeToElement round-trip.
    private static object? SanitizeJsonElement(JsonElement element, HashSet<string> keys)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var obj = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var prop in element.EnumerateObject())
                {
                    obj[prop.Name] = keys.Contains(prop.Name)
                        ? ObservabilityConstants.RedactedMarker
                        : SanitizeJsonElement(prop.Value, keys);
                }

                return obj;
            }
            case JsonValueKind.Array:
            {
                var arr = new List<object?>(element.GetArrayLength());
                foreach (var item in element.EnumerateArray())
                {
                    arr.Add(SanitizeJsonElement(item, keys));
                }

                return arr;
            }
            case JsonValueKind.Null:
                return null;
            default:
                return element;
        }
    }

    private static object? SanitizeObjectValue(object? value, HashSet<string> keys)
    {
        return value switch
        {
            null => null,
            JsonElement el => SanitizeJsonElement(el, keys),
            JsonNode node => SanitizeNode(node, keys),
            _ => value
        };
    }

    private static object? SanitizeNode(JsonNode? node, HashSet<string> keys)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonObject obj:
                return SanitizeFromJsonObject(obj, keys);
            case JsonArray arr:
            {
                var list = new List<object?>(arr.Count);
                foreach (var item in arr)
                {
                    list.Add(SanitizeNode(item, keys));
                }

                return list;
            }
            case JsonValue val:
                return val.GetValue<object>();
            default:
                return node;
        }
    }
}
