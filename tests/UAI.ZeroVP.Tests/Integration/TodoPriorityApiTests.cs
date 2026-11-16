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
/// Verifies PATCH /api/todos/{id}/priority over HTTP
/// using isolated JSON storage for each test.
/// </summary>
public sealed class TodoPriorityApiTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _storagePath;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TodoPriorityApiTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "ZeroVP-TodoPriority",
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
    public async Task PatchPriority_ValidRequest_ChangesOnlyPriorityAndIncrementsVersion()
    {
        // Arrange
        var created = await CreateTodoAsync();
        var id = created["id"]!.GetValue<string>();

        Assert.Equal(1L, created["version"]!.GetValue<long>());

        // Act
        using var response = await PatchPriorityAsync(
            id,
            """{"priority":"High","version":1}""");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await ReadObjectAsync(response);

        Assert.Equal("High", updated["priority"]!.GetValue<string>());
        Assert.Equal(2L, updated["version"]!.GetValue<long>());

        foreach (var property in created)
        {
            if (property.Key is "priority" or "version")
            {
                continue;
            }

            Assert.True(
                JsonNode.DeepEquals(property.Value, updated[property.Key]),
                $"Property '{property.Key}' must not change.");
        }

        Assert.Equal(created.Count, updated.Count);

        var retrieved = await GetJsonAsync($"/api/todos/{id}");

        Assert.True(JsonNode.DeepEquals(updated, retrieved));
    }

    [Fact]
    public async Task PatchPriority_UnknownId_Returns404ProblemDetailsAndPreservesStorage()
    {
        // Arrange
        await CreateTodoAsync();
        var before = await File.ReadAllTextAsync(_storagePath);

        // Act
        using var response = await PatchPriorityAsync(
            Guid.NewGuid().ToString(),
            """{"priority":"High","version":1}""");

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.NotFound);

        Assert.Equal(before, await File.ReadAllTextAsync(_storagePath));
    }

    [Fact]
    public async Task PatchPriority_StaleVersion_Returns409ProblemDetailsAndPreservesStorage()
    {
        // Arrange
        var created = await CreateTodoAsync();
        var id = created["id"]!.GetValue<string>();

        using (var first = await PatchPriorityAsync(
            id,
            """{"priority":"High","version":1}"""))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        var before = await File.ReadAllTextAsync(_storagePath);

        // Act
        using var response = await PatchPriorityAsync(
            id,
            """{"priority":"Low","version":1}""");

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Conflict);

        Assert.Equal(before, await File.ReadAllTextAsync(_storagePath));
    }

    [Theory]
    [InlineData("""{"priority":"Urgent","version":1}""")]
    [InlineData("""{"priority":99,"version":1}""")]
    [InlineData("""{"priority":"High"}""")]
    [InlineData("""{"priority":"High","version":0}""")]
    [InlineData("""{"version":1}""")]
    public async Task PatchPriority_InvalidBody_Returns400ValidationProblemAndPreservesStorage(
        string body)
    {
        // Arrange
        var created = await CreateTodoAsync();
        var id = created["id"]!.GetValue<string>();
        var before = await File.ReadAllTextAsync(_storagePath);

        // Act
        using var response = await PatchPriorityAsync(id, body);

        // Assert
        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            expectValidationErrors: true);

        Assert.Equal(before, await File.ReadAllTextAsync(_storagePath));
    }

    private async Task<HttpResponseMessage> PatchPriorityAsync(
        string id,
        string json)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/todos/{id}/priority")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        return await _client.SendAsync(request);
    }

    private async Task<JsonObject> CreateTodoAsync()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "Priority test todo",
                description = "Priority test",
                priority = "Normal",
                userId = "priority-user"
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
        }

        Assert.False(problem.ContainsKey("exception"));
        Assert.False(problem.ContainsKey("stackTrace"));
    }

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
