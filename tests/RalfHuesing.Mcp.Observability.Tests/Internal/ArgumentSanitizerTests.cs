using System.Text.Json;
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
        Assert.True(sanitized.TryGetValue(keyName, out var element));
        Assert.Equal("***REDACTED***", element.GetString());
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
        Assert.Equal("select * from users", sanitized["query"].GetString());
        Assert.Equal(50, sanitized["limit"].GetInt32());
        Assert.True(sanitized["isActive"].GetBoolean());
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

        var sanitizedUser = sanitized["user"];
        Assert.Equal("Alice", sanitizedUser.GetProperty("name").GetString());
        Assert.Equal("***REDACTED***", sanitizedUser.GetProperty("PASSWORD").GetString());
        Assert.Equal("***REDACTED***", sanitizedUser.GetProperty("nested").GetProperty("apiKey").GetString());

        var sanitizedList = sanitized["credentialsList"];
        Assert.Equal(2, sanitizedList.GetArrayLength());
        Assert.Equal("***REDACTED***", sanitizedList[0].GetProperty("token").GetString());
        Assert.Equal("read", sanitizedList[0].GetProperty("scope").GetString());
        Assert.Equal("***REDACTED***", sanitizedList[1].GetProperty("connectionString").GetString());
        Assert.Equal(5432, sanitizedList[1].GetProperty("port").GetInt32());
    }
}
