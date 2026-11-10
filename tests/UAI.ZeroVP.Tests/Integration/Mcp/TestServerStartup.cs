using Microsoft.AspNetCore.Mvc.Testing;

namespace UAI.ZeroVP.Tests.Integration.Mcp;

/// <summary>
/// Starts a WebApplicationFactory server with a timeout and ensures
/// cleanup never disposes the factory while startup is still running.
/// </summary>
internal static class TestServerStartup
{
    /// <summary>Maximum time for server startup and for waiting until it stops.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Starts the server in the background. StartServer() is synchronous
    /// and cannot be cancelled, so the returned task must be awaited
    /// by <see cref="WaitUntilStoppedAsync"/> before disposing the factory.
    /// </summary>
    public static Task Start(WebApplicationFactory<Program> factory) =>
        Task.Run(factory.StartServer);

    /// <summary>
    /// Waits until a startup task has finished, successfully or not.
    /// Throws if startup is still running, so the caller does not dispose
    /// the factory during startup.
    /// </summary>
    public static async Task WaitUntilStoppedAsync(Task? startupTask)
    {
        if (startupTask is null)
        {
            return;
        }

        var finished = await Task.WhenAny(startupTask, Task.Delay(Timeout))
            == startupTask;

        if (!finished)
        {
            throw new InvalidOperationException(
                "Server startup is still running after the timeout; " +
                "the factory was not disposed to avoid disposing it during startup.");
        }

        // Startup has stopped. Observe a possible startup exception
        // (already reported by the failing test) so it is not unobserved.
        _ = startupTask.Exception;
    }
}