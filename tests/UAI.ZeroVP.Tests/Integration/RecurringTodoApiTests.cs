using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.InMemory;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Tests.Integration;

public class RecurringTodoApiTests
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
    public async Task Repeat_ValidRequest_Returns201AndPersistsAllOccurrences()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var request = new
        {
            title = "Weekly meeting",
            description = "Team sync",
            priority = "High",
            firstDueAtUtc = "2026-10-20T10:00:00Z",
            repeatCount = 3
        };

        var response = await client.PostAsJsonAsync("/api/todos/repeat", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<List<TodoItemDto>>(JsonOptions);

        Assert.NotNull(created);
        Assert.Equal(4, created.Count);
        Assert.Equal(4, created.Select(x => x.Id).Distinct().Count());

        var firstDueAt = new DateTimeOffset(
            2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < created.Count; i++)
        {
            Assert.Equal(firstDueAt.AddDays(i * 7), created[i].DueAtUtc);
            Assert.False(created[i].IsCompleted);

            var stored = await client.GetFromJsonAsync<TodoItemDto>(
                $"/api/todos/{created[i].Id}", JsonOptions);

            Assert.NotNull(stored);
            Assert.Equal(created[i].DueAtUtc, stored.DueAtUtc);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(53)]
    public async Task Repeat_InvalidCount_Returns400AndCreatesNothing(int count)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/todos/repeat",
            new
            {
                title = "Invalid weekly task",
                priority = "Normal",
                firstDueAtUtc = "2026-10-20T10:00:00Z",
                repeatCount = count
            });

        await AssertValidationFailureAsync(client, response);
    }

    [Theory]
    [InlineData((string?)null)]
    [InlineData("not-a-date")]
    [InlineData("2026-10-20T12:00:00+02:00")]
    public async Task Repeat_InvalidDate_Returns400AndCreatesNothing(
        string? firstDueAtUtc)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/todos/repeat",
            new
            {
                title = "Invalid weekly task",
                priority = "Normal",
                firstDueAtUtc,
                repeatCount = 3
            });

        await AssertValidationFailureAsync(client, response);
    }

    [Fact]
    public async Task Repeat_InvalidTitle_Returns400AndCreatesNothing()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/todos/repeat",
            new
            {
                title = "",
                priority = "Normal",
                firstDueAtUtc = "2026-10-20T10:00:00Z",
                repeatCount = 3
            });

        await AssertValidationFailureAsync(client, response);
    }

    private static async Task AssertValidationFailureAsync(
        HttpClient client,
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content
            .ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.NotEmpty(problem.Errors);

        var todos = await client.GetFromJsonAsync<List<TodoItemDto>>(
            "/api/todos", JsonOptions);

        Assert.NotNull(todos);
        Assert.Empty(todos);
    }
}
