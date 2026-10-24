
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
