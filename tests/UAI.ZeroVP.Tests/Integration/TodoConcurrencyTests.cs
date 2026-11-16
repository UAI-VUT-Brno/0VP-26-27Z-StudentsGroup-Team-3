
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UAI.ZeroVP.Tests.Integration;

/// <summary>
/// Integration tests for optimistic concurrency through the HTTP API.
/// </summary>
public sealed class TodoConcurrencyTests
{
    [Fact]
    public async Task Update_IncrementsVersion_AfterEachSuccessfulUpdate()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (id, initialVersion) = await CreateTodoAsync(client);

        Assert.Equal(1L, initialVersion);

        using var firstResponse = await UpdateTodoAsync(
            client, id, initialVersion, "First update");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var firstVersion = await ReadVersionAsync(firstResponse);
        Assert.Equal(2L, firstVersion);

        using var secondResponse = await UpdateTodoAsync(
            client, id, firstVersion, "Second update");

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var secondVersion = await ReadVersionAsync(secondResponse);
        Assert.Equal(3L, secondVersion);

        using var getResponse = await client.GetAsync(
            $"/api/todos/{id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var document = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Second update",
            document.RootElement.GetProperty("title").GetString());

        Assert.Equal(
            3L,
            document.RootElement.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Update_WithStaleVersion_Returns409AndPreservesData()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (id, version) = await CreateTodoAsync(client);

        using var firstResponse = await UpdateTodoAsync(
            client, id, version, "Updated by client A");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(2L, await ReadVersionAsync(firstResponse));

        using var staleResponse = await UpdateTodoAsync(
            client, id, version, "Overwritten by client B");

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

        using var getResponse = await client.GetAsync(
            $"/api/todos/{id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var document = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Updated by client A",
            document.RootElement.GetProperty("title").GetString());

        Assert.Equal(
            2L,
            document.RootElement.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task UpdatePriority_ChangesOnlyPriorityAndIncrementsVersion()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var createResponse = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "Priority test task",
                description = "Preserve this description",
                userId = "priority-user",
                priority = "Normal",
                dueAtUtc = "2026-11-20T12:00:00Z"
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var id = createdDocument.RootElement.GetProperty("id").GetGuid();

        using var completeResponse = await client.PostAsync(
            $"/api/todos/{id}/complete",
            null);

        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        using var beforeResponse = await client.GetAsync($"/api/todos/{id}");
        Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);

        using var beforeDocument = JsonDocument.Parse(
            await beforeResponse.Content.ReadAsStringAsync());

        var before = beforeDocument.RootElement;
        var version = before.GetProperty("version").GetInt64();

        using var patchResponse = await PatchPriorityAsync(
            client,
            id,
            version,
            "High");

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        using var updatedDocument = JsonDocument.Parse(
            await patchResponse.Content.ReadAsStringAsync());

        var updated = updatedDocument.RootElement;
        Assert.Equal("High", updated.GetProperty("priority").GetString());
        Assert.Equal(version + 1, updated.GetProperty("version").GetInt64());

        foreach (var property in new[]
        {
            "id",
            "title",
            "description",
            "userId",
            "isCompleted",
            "createdAtUtc",
            "dueAtUtc",
            "completedAtUtc"
        })
        {
            Assert.Equal(
                before.GetProperty(property).GetRawText(),
                updated.GetProperty(property).GetRawText());
        }
    }

    [Fact]
    public async Task UpdatePriority_UnknownId_Returns404ProblemDetails()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await PatchPriorityAsync(
            client,
            Guid.NewGuid(),
            version: 1,
            priority: "High");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            (int)HttpStatusCode.NotFound,
            problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("title", out _));
    }

    [Fact]
    public async Task UpdatePriority_WithStaleVersion_Returns409AndPreservesData()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (id, version) = await CreateTodoAsync(client);

        using var firstResponse = await PatchPriorityAsync(
            client,
            id,
            version,
            "High");
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var staleResponse = await PatchPriorityAsync(
            client,
            id,
            version,
            "Low");

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        using var problem = JsonDocument.Parse(
            await staleResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            (int)HttpStatusCode.Conflict,
            problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("title", out _));

        using var getResponse = await client.GetAsync($"/api/todos/{id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using var document = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "High",
            document.RootElement.GetProperty("priority").GetString());
        Assert.Equal(
            version + 1,
            document.RootElement.GetProperty("version").GetInt64());
    }

    [Theory]
    [InlineData("""{"priority":"Urgent","version":1}""")]
    [InlineData("""{"priority":"High"}""")]
    [InlineData("""{"version":1}""")]
    public async Task UpdatePriority_InvalidRequest_Returns400AndPreservesData(
        string requestBody)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (id, _) = await CreateTodoAsync(client);

        using var beforeResponse = await client.GetAsync($"/api/todos/{id}");
        using var beforeDocument = JsonDocument.Parse(
            await beforeResponse.Content.ReadAsStringAsync());

        using var response = await client.PatchAsync(
            $"/api/todos/{id}/priority",
            new StringContent(
                requestBody,
                System.Text.Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            (int)HttpStatusCode.BadRequest,
            problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("errors", out var errors));
        Assert.NotEmpty(errors.EnumerateObject());

        using var afterResponse = await client.GetAsync($"/api/todos/{id}");
        using var afterDocument = JsonDocument.Parse(
            await afterResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            beforeDocument.RootElement.GetRawText(),
            afterDocument.RootElement.GetRawText());
    }

    [Fact]
    public async Task ConcurrentUpdates_OnlyOneSucceeds()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (id, version) = await CreateTodoAsync(client);

        var startSignal = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<HttpResponseMessage> SendUpdateAsync(string title)
        {
            await startSignal.Task;

            return await UpdateTodoAsync(
                client, id, version, title);
        }

        var taskA = SendUpdateAsync("Concurrent update A");
        var taskB = SendUpdateAsync("Concurrent update B");

        startSignal.SetResult(true);

        using var responseA = await taskA;
        using var responseB = await taskB;

        var statuses = new[]
        {
            responseA.StatusCode,
            responseB.StatusCode
        };

        Assert.Single(
            statuses,
            status => status == HttpStatusCode.OK);

        Assert.Single(
            statuses,
            status => status == HttpStatusCode.Conflict);

        var expectedTitle = responseA.StatusCode == HttpStatusCode.OK
            ? "Concurrent update A"
            : "Concurrent update B";

        using var getResponse = await client.GetAsync(
            $"/api/todos/{id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var document = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            expectedTitle,
            document.RootElement.GetProperty("title").GetString());

        Assert.Equal(
            2L,
            document.RootElement.GetProperty("version").GetInt64());
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseEnvironment("LoadTest"));
    }

    private static async Task<(Guid Id, long Version)> CreateTodoAsync(
        HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "Concurrency test task",
                priority = "Normal"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return (
            document.RootElement.GetProperty("id").GetGuid(),
            document.RootElement.GetProperty("version").GetInt64()
        );
    }

    private static Task<HttpResponseMessage> UpdateTodoAsync(
        HttpClient client,
        Guid id,
        long version,
        string title)
    {
        return client.PutAsJsonAsync(
            $"/api/todos/{id}",
            new
            {
                title,
                description = "Optimistic concurrency test",
                priority = "Normal",
                isCompleted = false,
                version
            });
    }

    private static Task<HttpResponseMessage> PatchPriorityAsync(
        HttpClient client,
        Guid id,
        long version,
        string priority)
    {
        return client.PatchAsJsonAsync(
            $"/api/todos/{id}/priority",
            new
            {
                priority,
                version
            });
    }

    private static async Task<long> ReadVersionAsync(
        HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return document.RootElement
            .GetProperty("version")
            .GetInt64();
    }
}
