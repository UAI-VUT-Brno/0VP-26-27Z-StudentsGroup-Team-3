using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Tests.Integration.Mcp;

/// <summary>
/// Integration tests of the MCP protocol over HTTP against the running API.
/// </summary>
public sealed class McpProtocolTests(McpServerFixture server)
    : IClassFixture<McpServerFixture>
{
    // ---------------------------------------------------------------- discovery

    [Fact]
    public async Task Discovery_ExposesExactlyTheReadOnlyTools()
    {
        var tools = await server.Client.ListToolsAsync();

        var names = tools.Select(tool => tool.Name).Order().ToArray();
        Assert.Equal(new[] { "get_todo", "list_todos" }, names);

        // No write tools: every tool is annotated read-only and non-destructive.
        foreach (var tool in tools)
        {
            var annotations = tool.ProtocolTool.Annotations;

            Assert.NotNull(annotations);
            Assert.True(annotations.ReadOnlyHint, $"{tool.Name} must be read-only.");
            Assert.False(annotations.DestructiveHint, $"{tool.Name} must not be destructive.");
        }
    }

    [Fact]
    public async Task Discovery_ListTodosSchemaHasOnlyOptionalFilters()
    {
        var schema = await GetInputSchemaAsync("list_todos");
        var properties = schema["properties"]!.AsObject();

        Assert.Equal(
            new[] { "isCompleted", "userId" },
            properties.Select(p => p.Key).Order().ToArray());

        Assert.Contains("boolean", SchemaTypes(properties["isCompleted"]!));
        Assert.Contains("string", SchemaTypes(properties["userId"]!));

        // Both filters are optional.
        Assert.Empty(RequiredParameters(schema));
    }

    [Fact]
    public async Task Discovery_GetTodoSchemaRequiresId()
    {
        var schema = await GetInputSchemaAsync("get_todo");
        var properties = schema["properties"]!.AsObject();

        Assert.Equal(
            new[] { "id" },
            properties.Select(p => p.Key).ToArray());

        Assert.Contains("string", SchemaTypes(properties["id"]!));
        Assert.Equal(new[] { "id" }, RequiredParameters(schema));
    }

    // ---------------------------------------------------------------- list_todos

    [Theory]
    [InlineData(null, null, 100)]
    [InlineData(true, null, 50)]
    [InlineData(false, null, 50)]
    [InlineData(null, McpTestData.UserA, 40)]
    [InlineData(true, McpTestData.UserA, 20)]
    [InlineData(false, McpTestData.UserB, 20)]
    public async Task ListTodos_ReturnsExactMatchingTodos(
        bool? isCompleted,
        string? userId,
        int expectedCount)
    {
        var arguments = new Dictionary<string, object?>();

        if (isCompleted is not null)
        {
            arguments["isCompleted"] = isCompleted;
        }

        if (userId is not null)
        {
            arguments["userId"] = userId;
        }

        var result = await server.Client.CallToolAsync("list_todos", arguments);

        var actual = ReadTodos(result);

        var expected = McpTestData.Todos
            .Where(t => isCompleted is null || t.IsCompleted == isCompleted)
            .Where(t => userId is null || t.UserId == userId)
            .Select(McpTestData.ToExpectedDto)
            .OrderBy(t => t.Id)
            .ToList();

        // Count from the issue, independent of the filter logic above.
        Assert.Equal(expectedCount, actual.Count);

        // Exact content, independent of ordering.
        Assert.Equal(expected, actual.OrderBy(t => t.Id).ToList());
    }

    [Fact]
    public async Task ListTodos_UnknownUser_ReturnsSuccessfulEmptyList()
    {
        var result = await server.Client.CallToolAsync(
            "list_todos",
            new Dictionary<string, object?> { ["userId"] = McpTestData.UnknownUser });

        Assert.Empty(ReadTodos(result));
    }

    [Fact]
    public async Task ListTodos_InvalidCompletionType_IsRejected()
    {
        await CallExpectingRejectionAsync(
            "list_todos",
            new Dictionary<string, object?> { ["isCompleted"] = "not-a-boolean" });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ListTodos_EmptyUserId_IsRejected(string userId)
    {
        var message = await CallExpectingRejectionAsync(
            "list_todos",
            new Dictionary<string, object?> { ["userId"] = userId });

        Assert.Contains("INVALID_ARGUMENT", message);
    }

    // ---------------------------------------------------------------- get_todo

    private static readonly string[] PublicTodoFields =
    [
        "completedAtUtc", "createdAtUtc", "description", "dueAtUtc", "id",
        "isCompleted", "priority", "title", "userId", "version"
    ];

    [Theory]
    [InlineData("a0000000-0000-0000-0000-000000000002")] // user-a, completed, with due date
    [InlineData("b0000000-0000-0000-0000-000000000040")] // user-b, incomplete
    [InlineData("c0000000-0000-0000-0000-000000000001")] // no user, completed, no due date
    public async Task GetTodo_ExistingId_ReturnsAllPublicFields(string id)
    {
        var result = await server.Client.CallToolAsync(
            "get_todo",
            new Dictionary<string, object?> { ["id"] = id });

        Assert.NotEqual(true, result.IsError);

        var todo = Assert.IsType<JsonObject>(ToNode(result.StructuredContent)["todo"]);

        // Exactly the public fields – no internal UpdatedAtUtc.
        Assert.Equal(PublicTodoFields, todo.Select(p => p.Key).Order().ToArray());

        // Priority is serialized as a string.
        Assert.Equal(JsonValueKind.String, todo["priority"]!.GetValueKind());

        var expected = McpTestData.ToExpectedDto(
            McpTestData.Todos.Single(t => t.Id == Guid.Parse(id)));

        Assert.Equal(expected, todo.Deserialize<TodoItemDto>(DtoJsonOptions));
    }

    [Fact]
    public async Task GetTodo_NonexistentId_ReturnsTodoNotFound()
    {
        var message = await CallExpectingRejectionAsync(
            "get_todo",
            new Dictionary<string, object?> { ["id"] = "d0000000-0000-0000-0000-000000000001" });

        Assert.Contains("TODO_NOT_FOUND", message);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("")]
    public async Task GetTodo_MalformedId_IsRejected(string id)
    {
        var message = await CallExpectingRejectionAsync(
            "get_todo",
            new Dictionary<string, object?> { ["id"] = id });

        Assert.Contains("INVALID_ARGUMENT", message);
    }

    [Fact]
    public async Task GetTodo_MissingId_IsRejected()
    {
        await CallExpectingRejectionAsync("get_todo", new Dictionary<string, object?>());
    }
        // ---------------------------------------------------------------- data

    [Fact]
    public async Task ToolCalls_LeaveStorageAndVersionsUnchanged()
    {
        // Successful calls.
        await server.Client.ListToolsAsync();
        await server.Client.CallToolAsync("list_todos");
        await server.Client.CallToolAsync(
            "list_todos",
            new Dictionary<string, object?>
            {
                ["userId"] = McpTestData.UserA,
                ["isCompleted"] = true
            });

        foreach (var todo in McpTestData.Todos.Take(5))
        {
            await server.Client.CallToolAsync(
                "get_todo",
                new Dictionary<string, object?> { ["id"] = todo.Id.ToString() });
        }

        // Rejected calls.
        await CallExpectingRejectionAsync(
            "get_todo",
            new Dictionary<string, object?> { ["id"] = "d0000000-0000-0000-0000-000000000001" });

        await CallExpectingRejectionAsync(
            "list_todos",
            new Dictionary<string, object?> { ["userId"] = "" });

        // Storage file is byte-for-byte identical to the seeded content.
        Assert.Equal(
            server.InitialStorageContent,
            await File.ReadAllTextAsync(server.StoragePath));

        // All versions are still 1.
        var all = ReadTodos(await server.Client.CallToolAsync("list_todos"));
        Assert.Equal(100, all.Count);
        Assert.All(all, todo => Assert.Equal(1L, todo.Version));
    }
    // ---------------------------------------------------------------- helpers

    private static readonly JsonSerializerOptions DtoJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

    /// <summary>Asserts a successful result and returns the structured "todos" array as DTOs.</summary>
    private static List<TodoItemDto> ReadTodos(CallToolResult result)
    {
        Assert.NotEqual(true, result.IsError);

        var todos = ToNode(result.StructuredContent)["todos"];
        Assert.IsType<JsonArray>(todos);

        return todos.Deserialize<List<TodoItemDto>>(DtoJsonOptions)
            ?? throw new InvalidOperationException("todos must not be null.");
    }

    /// <summary>
    /// Calls a tool expecting rejection – either a tool error (isError=true)
    /// or a protocol-level error. Returns the error message.
    /// </summary>
    private async Task<string> CallExpectingRejectionAsync(
        string toolName,
        Dictionary<string, object?> arguments)
    {
        string message;

        try
        {
            var result = await server.Client.CallToolAsync(toolName, arguments);

            Assert.True(result.IsError, $"{toolName} should reject the arguments.");

            message = string.Join(
                " ",
                result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        }
        catch (McpException ex)
        {
            // Protocol-level rejection, e.g. missing required argument.
            message = ex.Message;
        }

        Assert.False(string.IsNullOrWhiteSpace(message), "Error must have a message.");
        AssertNoInternals(message);

        return message;
    }

    private void AssertNoInternals(string message)
    {
        Assert.DoesNotContain("System.", message);
        Assert.DoesNotContain("UAI.ZeroVP", message);
        Assert.DoesNotContain("   at ", message);
        Assert.DoesNotContain(".cs:line", message);
        Assert.DoesNotContain(server.StoragePath, message);
        Assert.DoesNotContain(Path.GetTempPath(), message);
    }

    private async Task<JsonNode> GetInputSchemaAsync(string toolName)
    {
        var tools = await server.Client.ListToolsAsync();
        var tool = Assert.Single(tools, t => t.Name == toolName);

        var schema = ToNode(tool.ProtocolTool.InputSchema);
        Assert.Equal("object", schema["type"]!.GetValue<string>());

        return schema;
    }

    /// <summary>Converts any SDK JSON value (JsonElement, JsonNode, object) to JsonNode.</summary>
    private static JsonNode ToNode(object? value) =>
        JsonNode.Parse(JsonSerializer.Serialize(value))
        ?? throw new InvalidOperationException("Expected a JSON value.");

    /// <summary>Returns schema types whether "type" is a string or an array, e.g. ["boolean","null"].</summary>
    private static string[] SchemaTypes(JsonNode property) =>
        property["type"] switch
        {
            JsonArray array => array.Select(t => t!.GetValue<string>()).ToArray(),
            JsonValue single => new[] { single.GetValue<string>() },
            _ => Array.Empty<string>()
        };

    private static string[] RequiredParameters(JsonNode schema) =>
        schema["required"] is JsonArray required
            ? required.Select(r => r!.GetValue<string>()).Order().ToArray()
            : Array.Empty<string>();
}