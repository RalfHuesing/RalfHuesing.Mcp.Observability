using ModelContextProtocol.Protocol;
using RalfHuesing.Mcp.Observability.Internal;

namespace RalfHuesing.Mcp.Observability.Tests.Internal;

public sealed class ResponseLoggingTests
{
    [Fact]
    public void Response_EnableResponseLogging_True_AppearsConcatenatedWithNewline()
    {
        // @covers ToolCallLoggingHandler.ExtractResponse
        var additionalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var options = new McpObservabilityOptions
        {
            EnableResponseLogging = true
        };

        var result = new CallToolResult
        {
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = "a" },
                new TextContentBlock { Text = "b" }
            }
        };

        var extracted = ToolCallLoggingHandler.ExtractResponse(result, additionalKeys, options);

        Assert.Equal("a\nb", extracted.Response);
        Assert.Equal(3, extracted.Length);
        Assert.Equal(2, extracted.Lines);
        Assert.False(extracted.Truncated);
        Assert.Equal(0, extracted.NonTextCount);
    }

    [Fact]
    public void Response_EnableResponseLogging_False_AllFieldsAreDefaults()
    {
        // @covers ToolCallLoggingHandler.ExtractResponse
        var additionalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var options = new McpObservabilityOptions
        {
            EnableResponseLogging = false
        };

        var result = new CallToolResult
        {
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = "secret output" }
            }
        };

        var extracted = ToolCallLoggingHandler.ExtractResponse(result, additionalKeys, options);

        Assert.Null(extracted.Response);
        Assert.Equal("secret output".Length, extracted.Length);
        Assert.Equal(1, extracted.Lines);
        Assert.False(extracted.Truncated);
        Assert.Equal(0, extracted.NonTextCount);
    }

    [Fact]
    public void Response_MaxResponseLength_TruncatesAndAddsMarker()
    {
        // @covers ToolCallLoggingHandler.ExtractResponse
        var additionalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var longText = new string('x', 250);
        var options = new McpObservabilityOptions
        {
            EnableResponseLogging = true,
            MaxResponseLength = 100
        };

        var result = new CallToolResult
        {
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = longText }
            }
        };

        var extracted = ToolCallLoggingHandler.ExtractResponse(result, additionalKeys, options);

        Assert.NotNull(extracted.Response);
        Assert.Equal(100, extracted.Response!.Length - "... [truncated at 100 chars]".Length);
        Assert.EndsWith("... [truncated at 100 chars]", extracted.Response);
        Assert.Equal(250, extracted.Length);
        Assert.True(extracted.Truncated);
    }

    [Fact]
    public void Response_IsErrorResult_True_ContainsErrorText()
    {
        // @covers ToolCallLoggingHandler.ExtractResponse
        var additionalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var options = new McpObservabilityOptions
        {
            EnableResponseLogging = true
        };

        var result = new CallToolResult
        {
            IsError = true,
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = "boom" },
                new TextContentBlock { Text = "details" }
            }
        };

        var extracted = ToolCallLoggingHandler.ExtractResponse(result, additionalKeys, options);

        Assert.Equal("boom\ndetails", extracted.Response);
        Assert.Equal(12, extracted.Length);
        Assert.Equal(2, extracted.Lines);
        Assert.False(extracted.Truncated);
        Assert.Equal(0, extracted.NonTextCount);
    }

    [Fact]
    public void Response_NonTextContentBlocks_AreCountedAndNotInResponse()
    {
        // @covers ToolCallLoggingHandler.ExtractResponse
        var additionalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var options = new McpObservabilityOptions
        {
            EnableResponseLogging = true
        };

        var result = new CallToolResult
        {
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = "ok" },
                ImageContentBlock.FromBytes(new byte[] { 1, 2, 3 }, "image/png"),
                AudioContentBlock.FromBytes(new byte[] { 4, 5, 6 }, "audio/mpeg"),
                new EmbeddedResourceBlock
                {
                    Resource = new TextResourceContents
                    {
                        Uri = "file:///sample.txt",
                        Text = "embedded"
                    }
                }
            }
        };

        var extracted = ToolCallLoggingHandler.ExtractResponse(result, additionalKeys, options);

        Assert.Equal("ok", extracted.Response);
        Assert.Equal(3, extracted.NonTextCount);
        Assert.DoesNotContain("data:", extracted.Response);
        Assert.DoesNotContain("base64", extracted.Response, StringComparison.OrdinalIgnoreCase);
    }
}
