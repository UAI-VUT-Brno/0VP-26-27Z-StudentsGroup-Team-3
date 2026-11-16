using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.InMemory;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Tests.Integration;

public class UserTodoApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(
        JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
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

    [Fact]
    public async Task Todos_CreateUpdateAndFilterByUserAndCompletion()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var aliceTask = await CreateTodoAsync(client, "Alice task", "alice");
        var otherUserTask = await CreateTodoAsync(client, "Bob task", "bob");
        var legacyTask = await CreateTodoAsync(client, "Legacy task", null);

        Assert.Equal("alice", aliceTask.UserId);
        Assert.Null(legacyTask.UserId);

        var tasksBeforeUpdate = await client.GetFromJsonAsync<List<TodoItemDto>>(
            "/api/todos",
            JsonOptions);
        Assert.NotNull(tasksBeforeUpdate);
        Assert.Contains(tasksBeforeUpdate, item => item.UserId is null);

        var completeResponse = await client.PostAsync(
            $"/api/todos/{aliceTask.Id}/complete",
            content: null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/todos/{legacyTask.Id}",
            new
            {
                title = legacyTask.Title,
                description = legacyTask.Description,
                userId = "alice",
                priority = "Normal",
                isCompleted = false,
                version = legacyTask.Version
            });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updatedTask = await updateResponse.Content
            .ReadFromJsonAsync<TodoItemDto>(JsonOptions);
        Assert.NotNull(updatedTask);
        Assert.Equal("alice", updatedTask.UserId);

        var aliceTasks = await client.GetFromJsonAsync<List<TodoItemDto>>(
            "/api/todos?userId=alice",
            JsonOptions);
        var completedAliceTasks = await client.GetFromJsonAsync<List<TodoItemDto>>(
            "/api/todos?userId=alice&isCompleted=true",
            JsonOptions);
        var allTasks = await client.GetFromJsonAsync<List<TodoItemDto>>(
            "/api/todos",
            JsonOptions);

        Assert.NotNull(aliceTasks);
        Assert.Equal(2, aliceTasks.Count);
        Assert.All(aliceTasks, item => Assert.Equal("alice", item.UserId));

        var completedAliceTask = Assert.Single(completedAliceTasks!);
        Assert.Equal(aliceTask.Id, completedAliceTask.Id);
        Assert.True(completedAliceTask.IsCompleted);

        Assert.NotNull(allTasks);
        Assert.Equal(3, allTasks.Count);
        Assert.Contains(allTasks, item => item.Id == otherUserTask.Id);
    }

    private static async Task<TodoItemDto> CreateTodoAsync(
        HttpClient client,
        string title,
        string? userId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title,
                userId,
                priority = "Normal"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TodoItemDto>(JsonOptions))!;
    }
}
