namespace UAI.ZeroVP.DTO;

/// <summary>
/// Information about the running API instance.
/// In Docker lessons, this illustrates which container replica handled the request
/// and demonstrates that each replica maintains its own isolated in-memory state.
/// </summary>
public sealed record InstanceInfoDto
{
    /// <summary>Machine name – inside a container, this matches its hostname (short container ID).</summary>
    public required string MachineName { get; init; }

    /// <summary>ASP.NET Core environment name (Development / Production / ...).</summary>
    public required string Environment { get; init; }

    /// <summary>Assembly version of the API.</summary>
    public required string Version { get; init; }

    /// <summary>Current instance timestamp in UTC.</summary>
    public required DateTimeOffset UtcNow { get; init; }

    /// <summary>Process uptime – resets to zero when the container restarts.</summary>
    public required TimeSpan Uptime { get; init; }

    /// <summary>Number of todo items currently held in memory by this instance.</summary>
    public required int TodoCount { get; init; }

    /// <summary>Unique identifier of the running API instance.</summary>
    public string InstanceId { get; init; } = string.Empty;
}
