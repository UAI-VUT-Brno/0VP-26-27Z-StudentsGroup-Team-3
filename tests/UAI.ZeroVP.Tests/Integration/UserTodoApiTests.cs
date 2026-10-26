using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.InMemory;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Tests.Integration;

/// <summary>
/// Integration tests for user identification and "My Tasks" filtering
/// on the HTTP API (GET /api/todos?userId=...).
/// </summary>
public class UserTodoApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ITodoRepository>();

                    services.AddSingleton<ITodoRepository>(
                        new InMemoryTodoRepository(
                            new InMemoryTodoOptions
                            {
                                SeedSampleData = false
                            }));
                });
            });
    }

    private static async Task<TodoItemDto> CreateTodoAsync(HttpClient client, string title, string? userId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new { title, priority = "Normal", userId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TodoItemDto>(JsonOptions);
        Assert.NotNull(created);
        return created;
    }

    private static async Task<List<TodoItemDto>> GetTodosAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<List<TodoItemDto>>(JsonOptions);
        Assert.NotNull(items);
        return items;
    }

    [Fact]
    public async Task Create_WithUserId_ReturnsNormalizedUserId()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var created = await CreateTodoAsync(client, "Karel task", "  karel  ");

        Assert.Equal("karel", created.UserId);
    }

    [Fact]
    public async Task GetAll_WithUserId_ReturnsOnlyTodosOfThatUser()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        await CreateTodoAsync(client, "Karel task 1", "karel");
        await CreateTodoAsync(client, "Karel task 2", "karel");
        await CreateTodoAsync(client, "Jana task", "jana");
        await CreateTodoAsync(client, "Unassigned task", null);

        var items = await GetTodosAsync(client, "/api/todos?userId=karel");

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal("karel", item.UserId));
    }

    [Fact]
    public async Task GetAll_WithUserIdAndIsCompleted_CombinesFilters()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        await CreateTodoAsync(client, "Karel open", "karel");
        var karelDone = await CreateTodoAsync(client, "Karel done", "karel");
        await CreateTodoAsync(client, "Jana open", "jana");

        var completeResponse = await client.PostAsync($"/api/todos/{karelDone.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var open = await GetTodosAsync(client, "/api/todos?userId=karel&isCompleted=false");
        var done = await GetTodosAsync(client, "/api/todos?userId=karel&isCompleted=true");

        Assert.Equal("Karel open", Assert.Single(open).Title);
        Assert.Equal("Karel done", Assert.Single(done).Title);
    }

    [Fact]
    public async Task GetAll_WithoutUserId_ReturnsAllTodos_BackwardCompatible()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Request body in the original format, without the userId field at all.
        var legacyResponse = await client.PostAsJsonAsync(
            "/api/todos",
            new { title = "Legacy client task", priority = "Normal" });
        Assert.Equal(HttpStatusCode.Created, legacyResponse.StatusCode);

        var legacy = await legacyResponse.Content.ReadFromJsonAsync<TodoItemDto>(JsonOptions);
        Assert.NotNull(legacy);
        Assert.Null(legacy.UserId);

        await CreateTodoAsync(client, "Karel task", "karel");

        var all = await GetTodosAsync(client, "/api/todos");
        var emptyFilter = await GetTodosAsync(client, "/api/todos?userId=");

        Assert.Equal(2, all.Count);
        Assert.Equal(2, emptyFilter.Count);
    }

    [Fact]
    public async Task GetAll_UnknownUserId_ReturnsEmptyList()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        await CreateTodoAsync(client, "Karel task", "karel");

        var items = await GetTodosAsync(client, "/api/todos?userId=nobody");

        Assert.Empty(items);
    }

    [Fact]
    public async Task Create_TooLongUserId_Returns400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new { title = "Task", priority = "Normal", userId = new string('x', 101) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesUserId()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var created = await CreateTodoAsync(client, "Shared task", "karel");

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/todos/{created.Id}",
            new
            {
                title = "Shared task",
                priority = "Normal",
                isCompleted = false,
                userId = "jana",
                version = created.Version
            });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var karel = await GetTodosAsync(client, "/api/todos?userId=karel");
        var jana = await GetTodosAsync(client, "/api/todos?userId=jana");

        Assert.Empty(karel);
        Assert.Equal(created.Id, Assert.Single(jana).Id);
    }

    [Fact]
    public async Task Repeat_WithUserId_AssignsUserToAllOccurrences()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/todos/repeat",
            new
            {
                title = "Weekly report",
                priority = "Normal",
                userId = "karel",
                firstDueAtUtc = "2026-11-02T09:00:00Z",
                repeatCount = 2
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var items = await GetTodosAsync(client, "/api/todos?userId=karel");

        Assert.Equal(3, items.Count);
    }
}