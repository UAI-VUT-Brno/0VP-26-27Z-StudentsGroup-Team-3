using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UAI.ZeroVP.Data.Json;

namespace UAI.ZeroVP.Tests.Integration;

/// <summary>
/// Verifies REST API validation, error responses, and data preservation
/// using isolated JSON storage for each test.
/// </summary>
public sealed class ApiValidationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _storagePath;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiValidationTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "ZeroVP-ApiValidation",
            Guid.NewGuid().ToString("N"));

        _storagePath = Path.Combine(_testDirectory, "todos.json");

        Directory.CreateDirectory(_testDirectory);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<JsonFileTodoOptions>();
                    services.AddSingleton(new JsonFileTodoOptions
                    {
                        FilePath = _storagePath,
                        SeedSampleData = false
                    });
                });
            });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task UnknownPathUnderApi_Returns404ProblemDetails()
    {
        using var response = await _client.GetAsync("/api/non-existent-route");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnsupportedMethod_Returns405WithSupportedMethods()
    {
        using var response = await _client.DeleteAsync("/api/todos");

        await AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed);

        var methods = response.Content.Headers.Allow
            .OrderBy(method => method, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "GET", "POST" }, methods);
    }

    [Theory]
    [InlineData("GET", "/api/todos/not-a-guid")]
    [InlineData("PUT", "/api/todos/not-a-guid")]
    [InlineData("DELETE", "/api/todos/not-a-guid")]
    [InlineData("POST", "/api/todos/not-a-guid/complete")]
    public async Task MalformedId_Returns404AndPreservesStorage(
        string method,
        string path)
    {
        await CreateTodoAsync();
        var before = await File.ReadAllTextAsync(_storagePath);

        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (method == "PUT")
        {
            request.Content = JsonContent("""
                {
                  "title": "Attempted change",
                  "priority": "Normal",
                  "isCompleted": false,
                  "version": 1
                }
                """);
        }

        using var response = await _client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);

        Assert.Equal(before, await File.ReadAllTextAsync(_storagePath));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public async Task UnknownQueryParameter_IsIgnored(string isCompleted)
    {
        var created = await CreateTodoAsync();
        var id = created["id"]!.GetValue<string>();

        using var complete = await _client.PostAsync(
            $"/api/todos/{id}/complete", null);

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        await CreateTodoAsync("Incomplete todo");

        var before = await File.ReadAllTextAsync(_storagePath);

        var expected = await GetJsonAsync(
            $"/api/todos?isCompleted={isCompleted}");

        var actual = await GetJsonAsync(
            $"/api/todos?isCompleted={isCompleted}&unknownParameter=ignored");

        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            "An unknown query parameter changed the result.");

        Assert.Equal(before, await File.ReadAllTextAsync(_storagePath));
    }

    [Fact]
    public async Task UnknownJsonProperty_IsIgnoredAndNotPersisted()
    {
        using var content = JsonContent("""
            {
              "title": "Todo with extra property",
              "priority": "Normal",
              "unknownProperty": "must-not-be-persisted"
            }
            """);

        using var response = await _client.PostAsync("/api/todos", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await ReadObjectAsync(response);
        Assert.False(created.ContainsKey("unknownProperty"));

        var id = created["id"]!.GetValue<string>();
        var retrieved = await GetJsonAsync($"/api/todos/{id}");

        Assert.True(JsonNode.DeepEquals(created, retrieved));

        var storage = await File.ReadAllTextAsync(_storagePath);

        Assert.DoesNotContain("unknownProperty", storage);
        Assert.DoesNotContain("must-not-be-persisted", storage);
    }

    [Fact]
    public async Task UnknownJsonProperty_OnUpdate_IsIgnoredAndNotPersisted()
    {
        var created = await CreateTodoAsync();
        var id = created["id"]!.GetValue<string>();
        var version = created["version"]!.GetValue<long>();

        using var response = await _client.PutAsJsonAsync(
            $"/api/todos/{id}",
            new
            {
                title = "Updated todo",
                priority = "Normal",
                isCompleted = false,
                version,
                unknownProperty = "must-not-be-persisted"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await ReadObjectAsync(response);

        Assert.Equal("Updated todo", updated["title"]!.GetValue<string>());
        Assert.Equal(version + 1, updated["version"]!.GetValue<long>());
        Assert.False(updated.ContainsKey("unknownProperty"));

        var retrieved = await GetJsonAsync($"/api/todos/{id}");
        Assert.True(JsonNode.DeepEquals(updated, retrieved));

        var storage = await File.ReadAllTextAsync(_storagePath);

        Assert.DoesNotContain("unknownProperty", storage);
        Assert.DoesNotContain("must-not-be-persisted", storage);
    }

    [Fact]
    public async Task InvalidKnownQueryValue_ReturnsValidationProblemDetails()
    {
        await CreateTodoAsync();
        var before = await File.ReadAllTextAsync(_storagePath);

        using var response = await _client.GetAsync(
            "/api/todos?isCompleted=not-a-boolean");

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            expectValidationErrors: true);

        Assert.Equal(before, await File.ReadAllTextAsync(_storagePath));
    }

    [Theory]
    [InlineData("POST", "/api/todos")]
    [InlineData("PUT", "/api/todos/{id}")]
    [InlineData("DELETE", "/api/todos/{id}")]
    [InlineData("POST", "/api/todos/{id}/complete")]
    public async Task NonexistentTodo_Returns404AndPreservesStorage(
        string method,
        string path)
    {
        // POST /api/todos is a create endpoint, not an ID endpoint.
        // Its invalid-route case is covered separately.
        if (path == "/api/todos")
        {
            using var unknownResponse = await _client.PostAsync(
                $"/api/todos/{Guid.NewGuid()}", null);

            await AssertProblemAsync(
                unknownResponse,
                HttpStatusCode.MethodNotAllowed);

            return;
        }

        await CreateTodoAsync();
        var before = await File.ReadAllTextAsync(_storagePath);

        path = path.Replace("{id}", Guid.NewGuid().ToString());

        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (method == "PUT")
        {
            request.Content = JsonContent("""
                {
                  "title": "Missing todo",
                  "priority": "Normal",
                  "isCompleted": false,
                  "version": 1
                }
                """);
        }

        using var response = await _client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);

        Assert.Equal(before, await File.ReadAllTextAsync(_storagePath));
    }

    [Fact]
    public async Task GetNonexistentTodo_Returns404ProblemDetails()
    {
        using var response = await _client.GetAsync(
            $"/api/todos/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("malformed-json")]
    [InlineData("empty-body")]
    [InlineData("missing-title")]
    [InlineData("empty-title")]
    [InlineData("invalid-field-type")]
    [InlineData("unsupported-content-type")]
    [InlineData("unknown-only")]
    [InlineData("stale-version")]
    public async Task RejectedWrite_ReturnsProblemAndPreservesStorage(
        string scenario)
    {
        var created = await CreateTodoAsync();
        var id = created["id"]!.GetValue<string>();
        var initialVersion = created["version"]!.GetValue<long>();

        // Create a genuinely stale version rather than using an arbitrary number.
        if (scenario == "stale-version")
        {
            using var update = await _client.PutAsJsonAsync(
                $"/api/todos/{id}",
                new
                {
                    title = "Updated by another client",
                    priority = "High",
                    isCompleted = false,
                    version = initialVersion
                });

            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        }

        var before = await File.ReadAllTextAsync(_storagePath);
        var method = HttpMethod.Post;
        var path = "/api/todos";
        var expectedStatus = HttpStatusCode.BadRequest;
        var expectValidationErrors = true;
        HttpContent content;

        switch (scenario)
        {
            case "malformed-json":
                content = JsonContent("{ malformed json");
                break;

            case "empty-body":
                content = JsonContent("");
                break;

            case "missing-title":
                content = JsonContent("""{"priority":"Normal"}""");
                break;

            case "empty-title":
                content = JsonContent("""{"title":"","priority":"Normal"}""");
                break;

            case "invalid-field-type":
                method = HttpMethod.Put;
                path = $"/api/todos/{id}";
                content = JsonContent("""
                    {
                      "title": "Attempted invalid change",
                      "priority": "Normal",
                      "isCompleted": false,
                      "version": "not-a-number"
                    }
                    """);
                break;

            case "unsupported-content-type":
                content = new StringContent(
                    "title=Test", Encoding.UTF8, "text/plain");
                expectedStatus = HttpStatusCode.UnsupportedMediaType;
                expectValidationErrors = false;
                break;

            case "unknown-only":
                content = JsonContent("""
                    {"unknownProperty":"cannot-replace-required-title"}
                    """);
                break;

            case "stale-version":
                method = HttpMethod.Put;
                path = $"/api/todos/{id}";
                content = System.Net.Http.Json.JsonContent.Create(new
                {
                    title = "Attempted stale overwrite",
                    priority = "Normal",
                    isCompleted = false,
                    version = initialVersion
                });
                expectedStatus = HttpStatusCode.Conflict;
                expectValidationErrors = false;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        using var request = new HttpRequestMessage(method, path)
        {
            Content = content
        };

        using var response = await _client.SendAsync(request);

        await AssertProblemAsync(
            response,
            expectedStatus,
            expectValidationErrors);

        // Compare the complete persisted data, including all fields and versions.
        Assert.Equal(before, await File.ReadAllTextAsync(_storagePath));
    }

    private async Task<JsonObject> CreateTodoAsync(
        string title = "Initial valid todo")
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title,
                description = "Validation test",
                priority = "Normal",
                userId = "validation-user"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadObjectAsync(response);
    }

    private async Task<JsonNode> GetJsonAsync(string path)
    {
        using var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = JsonNode.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.NotNull(result);
        return result;
    }

    private static async Task<JsonObject> ReadObjectAsync(
        HttpResponseMessage response)
    {
        var node = JsonNode.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.NotNull(node);
        return Assert.IsType<JsonObject>(node);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        bool expectValidationErrors = false)
    {
        Assert.Equal(expectedStatus, response.StatusCode);

        var problem = await ReadObjectAsync(response);

        Assert.Equal(
            (int)expectedStatus,
            problem["status"]!.GetValue<int>());

        Assert.False(
            string.IsNullOrWhiteSpace(problem["title"]?.GetValue<string>()),
            "ProblemDetails must contain a meaningful title.");

        if (expectValidationErrors)
        {
            var errors = Assert.IsType<JsonObject>(problem["errors"]);

            Assert.NotEmpty(errors);

            foreach (var error in errors)
            {
                var messages = Assert.IsType<JsonArray>(error.Value);

                Assert.NotEmpty(messages);

                foreach (var message in messages)
                {
                    Assert.False(
                        string.IsNullOrWhiteSpace(message?.GetValue<string>()));
                }
            }
        }

        Assert.False(problem.ContainsKey("exception"));
        Assert.False(problem.ContainsKey("stackTrace"));

        var detail = problem["detail"]?.GetValue<string>() ?? "";

        Assert.DoesNotContain("System.", detail);
        Assert.DoesNotContain(" at ", detail);
    }

    private static StringContent JsonContent(string json) =>
        new(json, Encoding.UTF8, "application/json");

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();

        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
