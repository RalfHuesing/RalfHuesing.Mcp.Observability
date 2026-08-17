using System.Text.Json;
using System.Text.Json.Nodes;
using RalfHuesing.Mcp.Observability.Internal;

namespace RalfHuesing.Mcp.Observability.Tests.Internal;

public sealed class ArgumentSanitizerTests
{
    [Fact]
    public void Sanitize_HandlesNullOrEmptyArguments()
    {
        Assert.Null(ArgumentSanitizer.Sanitize(null));

        var empty = new Dictionary<string, JsonElement>();
        var result = ArgumentSanitizer.Sanitize(empty);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("PASSWORD")]
    [InlineData("pwd")]
    [InlineData("secret")]
    [InlineData("token")]
    [InlineData("apiKey")]
    [InlineData("apikey")]
    [InlineData("APIKEY")]
    [InlineData("accessToken")]
    [InlineData("authorization")]
    [InlineData("connectionString")]
    [InlineData("privateKey")]
    public void Sanitize_RedactsKnownSensitiveKeys_CaseInsensitive(string keyName)
    {
        var input = new Dictionary<string, JsonElement>
        {
            [keyName] = JsonSerializer.SerializeToElement("super-secret-value")
        };

        var sanitized = ArgumentSanitizer.Sanitize(input);

        Assert.NotNull(sanitized);
        Assert.True(sanitized.TryGetValue(keyName, out var value));
        Assert.Equal("***REDACTED***", value);
    }

    [Fact]
    public void Sanitize_PreservesNonSensitiveValues()
    {
        var input = new Dictionary<string, JsonElement>
        {
            ["query"] = JsonSerializer.SerializeToElement("select * from users"),
            ["limit"] = JsonSerializer.SerializeToElement(50),
            ["isActive"] = JsonSerializer.SerializeToElement(true)
        };

        var sanitized = ArgumentSanitizer.Sanitize(input);

        Assert.NotNull(sanitized);
        var queryEl = (JsonElement)sanitized["query"]!;
        var limitEl = (JsonElement)sanitized["limit"]!;
        var activeEl = (JsonElement)sanitized["isActive"]!;
        Assert.Equal("select * from users", queryEl.GetString());
        Assert.Equal(50, limitEl.GetInt32());
        Assert.True(activeEl.GetBoolean());
    }

    [Fact]
    public void Sanitize_RecursivelyRedactsNestedObjectsAndArrays()
    {
        var jsonDoc = JsonDocument.Parse("""
        {
            "user": {
                "name": "Alice",
                "PASSWORD": "hidden",
                "nested": {
                    "apiKey": "12345"
                }
            },
            "credentialsList": [
                { "token": "abc", "scope": "read" },
                { "connectionString": "server=localhost", "port": 5432 }
            ]
        }
        """);

        var input = new Dictionary<string, JsonElement>
        {
            ["user"] = jsonDoc.RootElement.GetProperty("user"),
            ["credentialsList"] = jsonDoc.RootElement.GetProperty("credentialsList")
        };

        var sanitized = ArgumentSanitizer.Sanitize(input);

        Assert.NotNull(sanitized);

        var user = Assert.IsType<Dictionary<string, object?>>(sanitized["user"]);
        var nameEl = (JsonElement)user["name"]!;
        Assert.Equal("Alice", nameEl.GetString());
        Assert.Equal("***REDACTED***", user["PASSWORD"]);
        var nested = Assert.IsType<Dictionary<string, object?>>(user["nested"]);
        Assert.Equal("***REDACTED***", nested["apiKey"]);

        var list = Assert.IsType<List<object?>>(sanitized["credentialsList"]);
        Assert.Equal(2, list.Count);
        var first = Assert.IsType<Dictionary<string, object?>>(list[0]);
        Assert.Equal("***REDACTED***", first["token"]);
        var scopeEl = (JsonElement)first["scope"]!;
        Assert.Equal("read", scopeEl.GetString());
        var second = Assert.IsType<Dictionary<string, object?>>(list[1]);
        Assert.Equal("***REDACTED***", second["connectionString"]);
        var port = (JsonElement)second["port"]!;
        Assert.Equal(5432, port.GetInt32());
    }

    [Fact]
    public void Sanitize_AcceptsDictionaryOfObject_AndProducesSameOutputAsJsonElementInput()
    {
        var asObjectDict = new Dictionary<string, object?>
        {
            ["text"] = "hello",
            ["limit"] = 50,
            ["nested"] = new Dictionary<string, object?>
            {
                ["k"] = "v"
            }
        };

        var asJsonElementDict = new Dictionary<string, JsonElement>
        {
            ["text"] = JsonSerializer.SerializeToElement("hello"),
            ["limit"] = JsonSerializer.SerializeToElement(50),
            ["nested"] = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["k"] = "v"
            })
        };

        var sanitizedObject = ArgumentSanitizer.Sanitize(asObjectDict);
        var sanitizedElement = ArgumentSanitizer.Sanitize(asJsonElementDict);

        Assert.NotNull(sanitizedObject);
        Assert.NotNull(sanitizedElement);

        var objectJson = JsonSerializer.Serialize(sanitizedObject);
        var elementJson = JsonSerializer.Serialize(sanitizedElement);

        Assert.Equal(elementJson, objectJson);
    }

    [Fact]
    public void Sanitize_RecursivelyRedactsNestedObjectDictionariesAndCollections()
    {
        var input = new Dictionary<string, object?>
        {
            ["request"] = new Dictionary<string, object?>
            {
                ["token"] = "nested-secret",
                ["visible"] = "value"
            },
            ["items"] = new object?[]
            {
                new Dictionary<string, object?> { ["password"] = "array-secret" }
            }
        };

        var sanitized = ArgumentSanitizer.Sanitize(input);

        Assert.NotNull(sanitized);
        var request = Assert.IsType<Dictionary<string, object?>>(sanitized["request"]);
        Assert.Equal("***REDACTED***", request["token"]);
        Assert.Equal("value", request["visible"]);
        var items = Assert.IsType<List<object?>>(sanitized["items"]);
        var item = Assert.IsType<Dictionary<string, object?>>(Assert.Single(items));
        Assert.Equal("***REDACTED***", item["password"]);
    }

    [Fact]
    public void Sanitize_AcceptsJsonObject_AndRedactsNestedSensitiveKeys()
    {
        var jsonObject = new JsonObject
        {
            ["user"] = new JsonObject
            {
                ["name"] = "Alice",
                ["password"] = "hidden"
            }
        };

        var sanitized = ArgumentSanitizer.Sanitize(jsonObject);

        Assert.NotNull(sanitized);
        Assert.True(sanitized.TryGetValue("user", out var userValue));
        var userObj = Assert.IsType<Dictionary<string, object?>>(userValue);
        Assert.Equal("Alice", userObj["name"]);
        Assert.Equal("***REDACTED***", userObj["password"]);
    }

    [Fact]
    public void Sanitize_String_Overload_RedactsKeyValuePairs()
    {
        var keyValueText = "token=abc123 foo=bar";
        var sanitized = ArgumentSanitizer.Sanitize(keyValueText);

        Assert.NotNull(sanitized);
        Assert.DoesNotContain("abc123", sanitized);
        Assert.Contains("token=***REDACTED***", sanitized);
        Assert.Contains("foo=bar", sanitized);

        var jsonLikeText = "\"sessionId\":\"xyz\" password=pw";
        var additional = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sessionId" };
        var sanitizedJson = ArgumentSanitizer.Sanitize(jsonLikeText, additional);

        Assert.NotNull(sanitizedJson);
        Assert.DoesNotContain("xyz", sanitizedJson);
        Assert.Contains("sessionId\":\"***REDACTED***", sanitizedJson);
        Assert.Contains("password=***REDACTED***", sanitizedJson);
    }
}
