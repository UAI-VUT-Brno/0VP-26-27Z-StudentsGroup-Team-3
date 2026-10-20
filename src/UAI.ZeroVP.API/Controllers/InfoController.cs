using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.API.Controllers;

/// <summary>
/// Provides diagnostic information about the running API instance.
/// </summary>
[ApiController]
[Route("api/info")]
[Produces("application/json")]
public sealed class InfoController : ControllerBase
{
    private readonly ITodoRepository _repository;
    private readonly IHostEnvironment _environment;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes the controller with dependencies from DI container.</summary>
    public InfoController(ITodoRepository repository, IHostEnvironment environment, TimeProvider timeProvider)
    {
        _repository = repository;
        _environment = environment;
        _timeProvider = timeProvider;
    }

    /// <summary>Returns information about the running API instance.</summary>
    /// <response code="200">Instance description including the count of in-memory todos.</response>
    [HttpGet]
    [ProducesResponseType(typeof(InstanceInfoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InstanceInfoDto>> Get(CancellationToken cancellationToken)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "unknown";

        // Process implements IDisposable. Without using, each request to /api/info
        // would hold a native process handle until the next garbage collection.
        using var process = Process.GetCurrentProcess();

        return Ok(new InstanceInfoDto
        {
            MachineName = Environment.MachineName,
            Environment = _environment.EnvironmentName,
            Version = version,
            UtcNow = _timeProvider.GetUtcNow(),
            Uptime = _timeProvider.GetUtcNow() - process.StartTime.ToUniversalTime(),
            TodoCount = await _repository.CountAsync(cancellationToken)
        });
    }
}
