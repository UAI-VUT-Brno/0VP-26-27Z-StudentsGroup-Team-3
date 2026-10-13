namespace UAI.ZeroVP.Data.InMemory;

/// <summary>
/// Configuration options for the in-memory store.
/// In a container, this can be overridden via an environment variable, e.g., <c>Todo__SeedSampleData=false</c>.
/// </summary>
public sealed class InMemoryTodoOptions
{
    /// <summary>Configuration section name in <c>appsettings.json</c>.</summary>
    public const string SectionName = "Todo";

    /// <summary>Populate the repository with sample todo items on application startup.</summary>
    public bool SeedSampleData { get; set; } = true;
}