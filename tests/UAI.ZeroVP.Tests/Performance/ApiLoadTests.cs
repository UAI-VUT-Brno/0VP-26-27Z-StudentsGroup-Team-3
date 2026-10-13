using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Xunit.Abstractions;

namespace UAI.ZeroVP.Tests.Performance;

public class ApiLoadTests
{
    private const int WarmupCount = 10;
    private const int RoundCount = 3;
    private const double ResponseTimeLimitMs = 500;

    private readonly ITestOutputHelper _output;

    public ApiLoadTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [Trait("Category", "Load")]
    [InlineData("GET /api/todos", 10)]
    [InlineData("GET /api/todos", 50)]
    [InlineData("GET /api/todos", 100)]
    [InlineData("GET /api/todos/{id}", 10)]
    [InlineData("GET /api/todos/{id}", 50)]
    [InlineData("GET /api/todos/{id}", 100)]
    [InlineData("POST /api/todos", 10)]
    [InlineData("POST /api/todos", 50)]
    [InlineData("POST /api/todos", 100)]
    [InlineData("PUT /api/todos/{id}", 10)]
    [InlineData("PUT /api/todos/{id}", 50)]
    [InlineData("PUT /api/todos/{id}", 100)]
    [InlineData("POST /api/todos/{id}/complete", 10)]
    [InlineData("POST /api/todos/{id}/complete", 50)]
    [InlineData("POST /api/todos/{id}/complete", 100)]
    [InlineData("DELETE /api/todos/{id}", 10)]
    [InlineData("DELETE /api/todos/{id}", 50)]
    [InlineData("DELETE /api/todos/{id}", 100)]
    public async Task ApiEndpoints_ConcurrentLoad_MeasuresResponseTime(
        string endpoint,
        int concurrentRequests)
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseEnvironment("LoadTest"));

        using var client = factory.CreateClient();

        var totalRequests =
            WarmupCount + concurrentRequests * RoundCount;

        // Each request operating on an ID gets its own dedicated todo item.
        // Item creation is excluded from the measured timeframe.
        var requiresExistingTodo = endpoint.Contains(
            "{id}",
            StringComparison.Ordinal);

        var ids = requiresExistingTodo
            ? new Guid[totalRequests]
            : Array.Empty<Guid>();

        if (requiresExistingTodo)
        {
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = await CreateTodoAsync(client);
            }
        }

        var expectedStatus = endpoint switch
        {
            "POST /api/todos" => HttpStatusCode.Created,
            "DELETE /api/todos/{id}" => HttpStatusCode.NoContent,
            _ => HttpStatusCode.OK
        };

        // Dispatches the actual HTTP request based on the target endpoint.
        Task<HttpResponseMessage> SendRequestAsync(int index)
        {
            return endpoint switch
            {
                "GET /api/todos" =>
                    client.GetAsync("/api/todos"),

                "GET /api/todos/{id}" =>
                    client.GetAsync($"/api/todos/{ids[index]}"),

                "POST /api/todos" =>
                    client.PostAsJsonAsync(
                        "/api/todos",
                        new
                        {
                            title = $"Load test task {index}",
                            priority = "High"
                        }),

                "PUT /api/todos/{id}" =>
                    client.PutAsJsonAsync(
                        $"/api/todos/{ids[index]}",
                        new
                        {
                            title = $"Updated load test task {index}",
                            description = "Concurrent load testing",
                            priority = "Normal",
                            isCompleted = false
                        }),

                "POST /api/todos/{id}/complete" =>
                    client.PostAsync(
                        $"/api/todos/{ids[index]}/complete",
                        null),

                "DELETE /api/todos/{id}" =>
                    client.DeleteAsync(
                        $"/api/todos/{ids[index]}"),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(endpoint),
                    endpoint,
                    "Unknown API endpoint.")
            };
        }

        // Warm-up phase – requests are not included in final statistics.
        for (var i = 0; i < WarmupCount; i++)
        {
            using var response = await SendRequestAsync(i);
            Assert.Equal(expectedStatus, response.StatusCode);
        }

        var samples = new List<RequestSample>(
            concurrentRequests * RoundCount);

        // Three measured rounds of concurrent load.
        for (var round = 0; round < RoundCount; round++)
        {
            var startSignal = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var offset =
                WarmupCount + round * concurrentRequests;

            var tasks = Enumerable
                .Range(offset, concurrentRequests)
                .Select(async index =>
                {
                    // Requests wait for a synchronized start signal.
                    await startSignal.Task;

                    var stopwatch = Stopwatch.StartNew();

                    try
                    {
                        using var response =
                            await SendRequestAsync(index);

                        stopwatch.Stop();

                        return new RequestSample(
                            stopwatch.Elapsed.TotalMilliseconds,
                            response.StatusCode,
                            null);
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();

                        return new RequestSample(
                            stopwatch.Elapsed.TotalMilliseconds,
                            null,
                            $"{ex.GetType().Name}: {ex.Message}");
                    }
                })
                .ToArray();

            startSignal.SetResult(true);

            var roundResults = await Task.WhenAll(tasks);

            samples.AddRange(roundResults);
        }

        // Computes statistics including failed requests.
        var durations = samples
            .Select(sample => sample.DurationMs)
            .OrderBy(duration => duration)
            .ToArray();

        var average = durations.Average();
        var minimum = durations[0];
        var maximum = durations[^1];

        static double Percentile(double[] sorted, double percentile)
        {
            var index = (int)Math.Ceiling(
                sorted.Length * percentile) - 1;

            return sorted[Math.Max(0, index)];
        }

        var p50 = Percentile(durations, 0.50);
        var p95 = Percentile(durations, 0.95);
        var p99 = Percentile(durations, 0.99);

        var exceeded = durations.Count(
            duration => duration >= ResponseTimeLimitMs);

        var successful = samples.Count(
            sample => sample.Status == expectedStatus &&
                      sample.Error is null);

        var httpErrors = samples.Count(
            sample => sample.Status is not null &&
                      sample.Status != expectedStatus);

        var transportErrors = samples.Count(
            sample => sample.Error is not null);

        // Output results to xUnit test output.
        _output.WriteLine($"Endpoint: {endpoint}");
        _output.WriteLine(
            $"Concurrent requests: {concurrentRequests}");

        _output.WriteLine($"Rounds: {RoundCount}");
        _output.WriteLine($"Measured requests: {samples.Count}");

        _output.WriteLine($"Average: {average:F2} ms");
        _output.WriteLine($"Minimum: {minimum:F2} ms");
        _output.WriteLine($"P50: {p50:F2} ms");
        _output.WriteLine($"P95: {p95:F2} ms");
        _output.WriteLine($"P99: {p99:F2} ms");
        _output.WriteLine($"Maximum: {maximum:F2} ms");

        _output.WriteLine(
            $"Responses >= 500 ms: {exceeded}/{samples.Count}");

        _output.WriteLine(
            $"500 ms target: {(exceeded == 0 ? "MET" : "EXCEEDED")}");

        _output.WriteLine(
            $"HTTP success: {successful}/{samples.Count}");

        _output.WriteLine($"HTTP errors: {httpErrors}");
        _output.WriteLine($"Transport errors: {transportErrors}");

        foreach (var failure in samples
            .Where(sample =>
                sample.Error is not null ||
                sample.Status != expectedStatus)
            .Take(3))
        {
            _output.WriteLine(
                $"Failure: HTTP {failure.Status}, {failure.Error}");
        }

        // Functional correctness must hold under load.
        // The latency limit is diagnostic to prevent flaky tests
        // caused by runner machine performance variations.
        Assert.True(
            successful == samples.Count,
            $"{endpoint}: only {successful}/{samples.Count} " +
            "requests returned the expected HTTP status.");
    }

    private static async Task<Guid> CreateTodoAsync(
        HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = $"Load test setup {Guid.NewGuid():N}",
                priority = "High"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        return document.RootElement
            .GetProperty("id")
            .GetGuid();
    }

    private sealed record RequestSample(
        double DurationMs,
        HttpStatusCode? Status,
        string? Error);
}