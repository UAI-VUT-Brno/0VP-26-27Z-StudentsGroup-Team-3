using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace UAI.ZeroVP.Tests.Integration.Mcp;

/// <summary>
/// Verifies that /mcp is unavailable on a running API when MCP is disabled
/// or the environment is not Development.
/// </summary>
public sealed class McpDisabledEndpointTests
{
    [Theory]
    [InlineData("Development", "false")] // disabled by configuration
    [InlineData("Production", "true")]   // enabled, but outside Development
    public async Task Mcp_WhenNotAvailable_RunningApiReturns404(
        string environment,
        string mcpEnabled)
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "ZeroVP-McpTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(testDirectory);

        WebApplicationFactory<Program>? factory = null;
        Task? startupTask = null;

        try
        {
            var server = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment(environment);
                    builder.UseSetting("Mcp:Enabled", mcpEnabled);
                    builder.UseSetting(
                        "TodoStorage:FilePath",
                        Path.Combine(testDirectory, "todos.json"));
                    builder.UseSetting("TodoStorage:SeedSampleData", "false");
                });

            factory = server;

            server.UseKestrel(options =>
                options.Listen(IPAddress.Loopback, 0));

            startupTask = TestServerStartup.Start(server);
            await startupTask.WaitAsync(TestServerStartup.Timeout);

            var addressesFeature = server.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException(
                    "Server addresses are not available.");

            using var http = new HttpClient
            {
                BaseAddress = new Uri(addressesFeature.Addresses.Single()),
                Timeout = TestServerStartup.Timeout
            };

            // The API must be running. A connection failure throws
            // and fails the test – it is never a passing result.
            using var health = await http.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);

            // A real MCP initialize request must get 404.
            using var initialize = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = new StringContent(
                    """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""",
                    Encoding.UTF8,
                    "application/json")
            };

            initialize.Headers.Accept.ParseAdd("application/json");
            initialize.Headers.Accept.ParseAdd("text/event-stream");

            using var postResponse = await http.SendAsync(initialize);
            Assert.Equal(HttpStatusCode.NotFound, postResponse.StatusCode);

            using var getResponse = await http.GetAsync("/mcp");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
        finally
        {
            // Never dispose the factory while StartServer() is still running.
            await TestServerStartup.WaitUntilStoppedAsync(startupTask);

            if (factory is not null)
            {
                await factory.DisposeAsync();
            }

            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }
}