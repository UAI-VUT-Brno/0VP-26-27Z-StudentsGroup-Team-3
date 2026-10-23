namespace UAI.ZeroVP.Data.Json;

/// <summary>
/// Configuration options for the JSON file-backed todo repository.
/// </summary>
public sealed class JsonFileTodoOptions
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "TodoStorage";

    /// <summary>Path to the JSON file where tasks are persisted.</summary>
    public string FilePath { get; set; } = "data/todos.json";

    /// <summary>Populate the repository with sample todo items if storage is newly created.</summary>
    public bool SeedSampleData { get; set; } = true;
}
