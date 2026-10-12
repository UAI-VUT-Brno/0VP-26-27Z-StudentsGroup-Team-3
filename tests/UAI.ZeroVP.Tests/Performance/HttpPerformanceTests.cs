using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit.Abstractions;

namespace UAI.ZeroVP.Tests.Performance;

public class HttpPerformanceTests
{
    private const int WarmupCount = 10;
    private const int MeasurementCount = 50;
    private const double ResponseTimeLimitMs = 500;

    private readonly ITestOutputHelper _output;

    public HttpPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetAll_HttpRequest_ReturnsOk()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("LoadTest"));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/todos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            JsonValueKind.Array,
            document.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetAll_HttpRequest_MeasuresResponseTime()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("LoadTest"));
        using var client = factory.CreateClient();

        await MeasureAsync(
            "GET /api/todos",
            () => client.GetAsync("/api/todos"),
            HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_HttpRequest_MeasuresResponseTime()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("LoadTest"));
        using var client = factory.CreateClient();

        await MeasureAsync(
            "POST /api/todos",
            () => client.PostAsJsonAsync(
                "/api/todos",
                new
                {
                    title = "Performance task",
                    priority = "High"
                }),
            HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetById_HttpRequest_MeasuresResponseTime()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("LoadTest"));
        using var client = factory.CreateClient();

        var id = await CreateTodoAsync(client);
        var endpoint = $"/api/todos/{id}";

        await MeasureAsync(
            "GET /api/todos/{id}",
            () => client.GetAsync(endpoint),
            HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_HttpRequest_MeasuresResponseTime()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("LoadTest"));
        using var client = factory.CreateClient();

        Guid currentId = Guid.Empty;

        async Task PrepareAsync()
        {
            currentId = await CreateTodoAsync(client);
        }

        await MeasureAsync(
            "PUT /api/todos/{id}",
            () => client.PutAsJsonAsync(
                $"/api/todos/{currentId}",
                new
                {
                    title = "Updated performance task",
                    description = "Performance measurement",
                    priority = "Normal",
                    isCompleted = false
                }),
            HttpStatusCode.OK,
            PrepareAsync);
    }

    [Fact]
    public async Task Complete_HttpRequest_MeasuresResponseTime()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("LoadTest"));
        using var client = factory.CreateClient();

        Guid currentId = Guid.Empty;

        async Task PrepareAsync()
        {
            currentId = await CreateTodoAsync(client);
        }

        await MeasureAsync(
            "POST /api/todos/{id}/complete",
            () => client.PostAsync(
                $"/api/todos/{currentId}/complete",
                null),
            HttpStatusCode.OK,
            PrepareAsync);
    }

    [Fact]
    public async Task Delete_HttpRequest_MeasuresResponseTime()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("LoadTest"));
        using var client = factory.CreateClient();

        Guid currentId = Guid.Empty;

        async Task PrepareAsync()
        {
            currentId = await CreateTodoAsync(client);
        }

        await MeasureAsync(
            "DELETE /api/todos/{id}",
            () => client.DeleteAsync($"/api/todos/{currentId}"),
            HttpStatusCode.NoContent,
            PrepareAsync);
    }

    private static async Task<Guid> CreateTodoAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "Performance setup task",
                priority = "High"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private async Task MeasureAsync(
        string endpoint,
        Func<Task<HttpResponseMessage>> sendRequest,
        HttpStatusCode expectedStatus,
        Func<Task>? prepareRequest = null)
    {
        var durations = new List<double>();

        // Warm-up: nezapočítává se do statistik.
        for (var i = 0; i < WarmupCount; i++)
        {
            if (prepareRequest is not null)
            {
                await prepareRequest();
            }

            using var response = await sendRequest();
            Assert.Equal(expectedStatus, response.StatusCode);
        }

        // Měření skutečných HTTP požadavků.
        for (var i = 0; i < MeasurementCount; i++)
        {
            // Příprava dat je mimo měřený interval.
            if (prepareRequest is not null)
            {
                await prepareRequest();
            }

            var stopwatch = Stopwatch.StartNew();

            using var response = await sendRequest();

            stopwatch.Stop();

            Assert.Equal(expectedStatus, response.StatusCode);

            durations.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        // Výpočet statistik.
        var sorted = durations.OrderBy(x => x).ToArray();

        var average = durations.Average();
        var minimum = sorted[0];
        var maximum = sorted[^1];

        var p50 = sorted[
            (int)Math.Ceiling(MeasurementCount * 0.50) - 1];

        var p95 = sorted[
            (int)Math.Ceiling(MeasurementCount * 0.95) - 1];

        // Výpis výsledků.
        _output.WriteLine(endpoint);
        _output.WriteLine($"Requests: {MeasurementCount}");
        _output.WriteLine($"Average: {average:F2} ms");
        _output.WriteLine($"Minimum: {minimum:F2} ms");
        _output.WriteLine($"Maximum: {maximum:F2} ms");
        _output.WriteLine($"P50: {p50:F2} ms");
        _output.WriteLine($"P95: {p95:F2} ms");
        _output.WriteLine($"Limit: {ResponseTimeLimitMs:F0} ms");

        // Kontrola požadovaného limitu.
        Assert.True(
            maximum < ResponseTimeLimitMs,
            $"{endpoint}: maximum {maximum:F2} ms " +
            $"exceeded limit {ResponseTimeLimitMs:F0} ms.");
    }
}
