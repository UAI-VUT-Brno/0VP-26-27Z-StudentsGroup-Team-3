using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;

namespace UAI.ZeroVP.Tests.Integration.Mcp;

/// <summary>
/// Starts the real API on a free loopback port with MCP enabled
/// and isolated temporary JSON storage. Shared by all tests in a class.
/// </summary>
public sealed class McpServerFixture : IAsyncLifetime
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "ZeroVP-McpTests",
        Guid.NewGuid().ToString("N"));

    private WebApplicationFactory<Program>? _factory;
    private Task? _startupTask;
    private McpClient? _client;

    /// <summary>Path to the isolated JSON storage file.</summary>
    public string StoragePath => Path.Combine(_testDirectory, "todos.json");

    /// <summary>Base address of the running server, e.g. http://127.0.0.1:51234.</summary>
    public Uri BaseAddress { get; private set; } = null!;

    /// <summary>Storage file content right after seeding, used to verify nothing changed.</summary>
    public string InitialStorageContent { get; private set; } = "";

    /// <summary>Connected MCP client.</summary>
    public McpClient Client =>
        _client ?? throw new InvalidOperationException("MCP client is not initialized.");

    public async Task InitializeAsync()
    {
        try
        {
            Directory.CreateDirectory(_testDirectory);

            await McpTestData.WriteStorageAsync(StoragePath);
            InitialStorageContent = await File.ReadAllTextAsync(StoragePath);

            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Development");
                    builder.UseSetting("Mcp:Enabled", "true");
                    builder.UseSetting("TodoStorage:FilePath", StoragePath);
                    builder.UseSetting("TodoStorage:SeedSampleData", "false");
                });

            // Real Kestrel server on 127.0.0.1 with an OS-assigned free port.
            _factory.UseKestrel(options =>
                options.Listen(IPAddress.Loopback, 0));

            _startupTask = TestServerStartup.Start(_factory);
            await _startupTask.WaitAsync(TestServerStartup.Timeout);

            var addressesFeature = _factory.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException(
                    "Server addresses are not available.");

            BaseAddress = new Uri(addressesFeature.Addresses.Single());

            using var timeout = new CancellationTokenSource(TestServerStartup.Timeout);

            _client = await McpClient.CreateAsync(
                new HttpClientTransport(new HttpClientTransportOptions
                {
                    Endpoint = new Uri(BaseAddress, "/mcp"),
                    TransportMode = HttpTransportMode.StreamableHttp
                }),
                cancellationToken: timeout.Token);
        }
        catch
        {
            // Clean up even when startup fails.
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }

        // Never dispose the factory while StartServer() is still running.
        await TestServerStartup.WaitUntilStoppedAsync(_startupTask);
        _startupTask = null;

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }

        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}