using System.Text.Json;

namespace RalfHuesing.Mcp.Observability.Internal;

internal static class JsonlSerializerOptions
{
    internal static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
