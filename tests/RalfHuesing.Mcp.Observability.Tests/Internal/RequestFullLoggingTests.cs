using System.Text.Json;
using RalfHuesing.Mcp.Observability.Internal;

namespace RalfHuesing.Mcp.Observability.Tests.Internal;

public sealed class RequestFullLoggingTests
{
    [Fact]
    public void Sanitize_PreservesAllTopLevelKeysIncludingNull()
    {
        // @covers ArgumentSanitizer
        var input = new Dictionary<string, JsonElement>
        {
            ["a"] = JsonSerializer.SerializeToElement(1),
            ["b"] = JsonSerializer.SerializeToElement("text"),
            ["c"] = JsonSerializer.SerializeToElement(true),
            ["d"] = JsonSerializer.SerializeToElement<object?>(null),
            ["e"] = JsonSerializer.SerializeToElement(new { inner = "value" })
        };

        var sanitized = ArgumentSanitizer.Sanitize(input);

        Assert.NotNull(sanitized);
        Assert.Equal(5, sanitized.Count);
        Assert.True(sanitized.ContainsKey("a"));
        Assert.True(sanitized.ContainsKey("b"));
        Assert.True(sanitized.ContainsKey("c"));
        Assert.True(sanitized.ContainsKey("d"));
        Assert.True(sanitized.ContainsKey("e"));
        Assert.Null(sanitized["d"]);
    }

    [Fact]
    public void Sanitize_PreservesComplexTypesAndCollections()
    {
        // @covers ArgumentSanitizer
        var now = DateTime.UtcNow;
        var guid = Guid.NewGuid();
        int[] tags = [1, 2, 3];
        var input = new Dictionary<string, JsonElement>
        {
            ["when"] = JsonSerializer.SerializeToElement(now),
            ["id"] = JsonSerializer.SerializeToElement(guid),
            ["tags"] = JsonSerializer.SerializeToElement(tags),
            ["meta"] = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["nestedKey"] = "nestedValue"
            })
        };

        var sanitized = ArgumentSanitizer.Sanitize(input);

        Assert.NotNull(sanitized);
        Assert.Equal(4, sanitized.Count);

        var whenEl = (JsonElement)sanitized["when"]!;
        var idEl = (JsonElement)sanitized["id"]!;
        var tagsList = Assert.IsType<List<object?>>(sanitized["tags"]);
        var metaObj = Assert.IsType<Dictionary<string, object?>>(sanitized["meta"]);

        Assert.Equal(now.ToString("O"), whenEl.GetDateTime().ToString("O"));
        Assert.Equal(guid, idEl.GetGuid());
        Assert.Equal(3, tagsList.Count);
        var nestedValueEl = (JsonElement)metaObj["nestedKey"]!;
        Assert.Equal("nestedValue", nestedValueEl.GetString());
    }

    [Fact]
    public void Sanitize_HonorsAdditionalSensitiveKeys()
    {
        // @covers ArgumentSanitizer
        var additionalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sessionId"
        };

        var input = new Dictionary<string, JsonElement>
        {
            ["sessionId"] = JsonSerializer.SerializeToElement("abc"),
            ["user"] = JsonSerializer.SerializeToElement("alice")
        };

        var sanitized = ArgumentSanitizer.Sanitize(input, additionalKeys);

        Assert.NotNull(sanitized);
        Assert.Equal("***REDACTED***", sanitized["sessionId"]);
        Assert.Equal("alice", ((JsonElement)sanitized["user"]!).GetString());
    }
}
