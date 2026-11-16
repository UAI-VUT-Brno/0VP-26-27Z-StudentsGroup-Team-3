using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.InMemory;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Tests.Integration;

public class UserTodoFilterApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(
        JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
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

    [Fact]
    public async Task Create_WithUserId_Returns201WithUserId()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new { title = "Alice task", userId = "  alice  " });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TodoItemDto>(JsonOptions);

        Assert.NotNull(created);
        Assert.Equal("alice", created.UserId);

        var stored = await client.GetFromJsonAsync<TodoItemDto>(
            $"/api/todos/{created.Id}", JsonOptions);

        Assert.NotNull(stored);
        Assert.Equal("alice", stored.UserId);
    }

    [Fact]
    public async Task Create_WithoutUserId_Returns201WithNullUserId()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new { title = "Untagged task" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TodoItemDto>(JsonOptions);

        Assert.NotNull(created);
        Assert.Null(created.UserId);
    }

    [Fact]
    public async Task Create_UserIdTooLong_Returns400()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new { title = "Too long user", userId = new string('a', 101) });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.NotEmpty(problem.Errors);

        var todos = await client.GetFromJsonAsync<List<TodoItemDto>>(
            "/api/todos", JsonOptions);

        Assert.NotNull(todos);
        Assert.Empty(todos);
    }

    [Fact]
    public async Task GetAll_WithUserId_ReturnsOnlyThatUsersTodos()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        await CreateTodoAsync(client, "Alice task 1", "alice");
        await CreateTodoAsync(client, "Alice task 2", "alice");
        await CreateTodoAsync(client, "Bob task", "bob");
        await CreateTodoAsync(client, "Untagged task", null);

        // Act
        var todos = await client.GetFromJsonAsync<List<TodoItemDto>>(
            "/api/todos?userId=alice", JsonOptions);

        // Assert
        Assert.NotNull(todos);
        Assert.Equal(2, todos.Count);
        Assert.All(todos, todo => Assert.Equal("alice", todo.UserId));
    }

    [Fact]
    public async Task GetAll_WithUserIdAndIsCompleted_ReturnsMatchingSubset()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        await CreateTodoAsync(client, "Alice open", "alice");
        var aliceCompleted = await CreateTodoAsync(client, "Alice completed", "alice");
        var bobCompleted = await CreateTodoAsync(client, "Bob completed", "bob");
        var untaggedCompleted = await CreateTodoAsync(client, "Untagged completed", null);

        foreach (var id in new[] { aliceCompleted.Id, bobCompleted.Id, untaggedCompleted.Id })
        {
            var completeResponse = await client.PostAsync(
                $"/api/todos/{id}/complete",
                content: null);

            Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        }

        // Act
        var todos = await client.GetFromJsonAsync<List<TodoItemDto>>(
            "/api/todos?userId=alice&isCompleted=true", JsonOptions);

        // Assert
        Assert.NotNull(todos);
        var todo = Assert.Single(todos);
        Assert.Equal(aliceCompleted.Id, todo.Id);
        Assert.True(todo.IsCompleted);
        Assert.Equal("alice", todo.UserId);
    }

    [Fact]
    public async Task GetAll_WithoutUserId_ReturnsTodosWithAndWithoutUserId()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        await CreateTodoAsync(client, "Alice task", "alice");
        await CreateTodoAsync(client, "Untagged task", null);

        // Act
        var todos = await client.GetFromJsonAsync<List<TodoItemDto>>(
            "/api/todos", JsonOptions);

        // Assert
        Assert.NotNull(todos);
        Assert.Equal(2, todos.Count);
        Assert.Contains(todos, todo => todo.UserId == "alice");
        Assert.Contains(todos, todo => todo.UserId is null);
    }

    [Fact]
    public async Task GetAll_UnknownUserId_ReturnsEmptyList()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        await CreateTodoAsync(client, "Alice task", "alice");

        // Act
        var response = await client.GetAsync("/api/todos?userId=nobody");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var todos = await response.Content.ReadFromJsonAsync<List<TodoItemDto>>(JsonOptions);

        Assert.NotNull(todos);
        Assert.Empty(todos);
    }

    [Fact]
    public async Task GetAll_UserIdTooLong_Returns400()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(
            $"/api/todos?userId={new string('a', 101)}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.NotEmpty(problem.Errors);
    }

    [Fact]
    public async Task Update_WithUserId_ChangesUserId()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var created = await CreateTodoAsync(client, "Shared task", "alice");

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/todos/{created.Id}",
            new
            {
                title = "Shared task",
                priority = "Normal",
                isCompleted = false,
                version = created.Version,
                userId = "bob"
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<TodoItemDto>(JsonOptions);

        Assert.NotNull(updated);
        Assert.Equal("bob", updated.UserId);

        var bobTodos = await client.GetFromJsonAsync<List<TodoItemDto>>(
            "/api/todos?userId=bob", JsonOptions);
        var aliceTodos = await client.GetFromJsonAsync<List<TodoItemDto>>(
            "/api/todos?userId=alice", JsonOptions);

        Assert.NotNull(bobTodos);
        Assert.Equal(created.Id, Assert.Single(bobTodos).Id);

        Assert.NotNull(aliceTodos);
        Assert.Empty(aliceTodos);
    }

    private static async Task<TodoItemDto> CreateTodoAsync(
        HttpClient client,
        string title,
        string? userId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new { title, userId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TodoItemDto>(JsonOptions);

        Assert.NotNull(created);

        return created;
    }
}
