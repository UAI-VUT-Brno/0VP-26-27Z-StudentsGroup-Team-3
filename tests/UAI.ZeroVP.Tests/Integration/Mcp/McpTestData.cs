using System.Text.Json;
using System.Text.Json.Serialization;
using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Tests.Integration.Mcp;

/// <summary>
/// 100 deterministic todos used by the MCP integration tests:
/// 40 for user-a, 40 for user-b and 20 without a user,
/// each group split evenly into completed and incomplete items.
/// </summary>
public static class McpTestData
{
    public const string UserA = "user-a";
    public const string UserB = "user-b";
    public const string UnknownUser = "user-unknown";

    private static readonly DateTimeOffset BaseTime =
        new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    // Same format as JsonFileTodoRepository uses.
    private static readonly JsonSerializerOptions StorageJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>All seeded todos as stored entities.</summary>
    public static IReadOnlyList<TodoItem> Todos { get; } = Create();

    /// <summary>Writes the seeded todos to the given JSON storage file.</summary>
    public static Task WriteStorageAsync(string path) =>
        File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(Todos, StorageJsonOptions));

    /// <summary>Expected public DTO for a seeded todo.</summary>
    public static TodoItemDto ToExpectedDto(TodoItem item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Description = item.Description,
        UserId = item.UserId,
        Priority = item.Priority,
        IsCompleted = item.IsCompleted,
        CreatedAtUtc = item.CreatedAtUtc,
        DueAtUtc = item.DueAtUtc,
        CompletedAtUtc = item.CompletedAtUtc,
        Version = item.Version
    };

    private static List<TodoItem> Create()
    {
        var items = new List<TodoItem>();

        AddGroup(items, 'a', UserA, completed: 20, incomplete: 20);
        AddGroup(items, 'b', UserB, completed: 20, incomplete: 20);
        AddGroup(items, 'c', userId: null, completed: 10, incomplete: 10);

        return items;
    }

    private static void AddGroup(
        List<TodoItem> items,
        char idPrefix,
        string? userId,
        int completed,
        int incomplete)
    {
        for (var n = 1; n <= completed + incomplete; n++)
        {
            var isCompleted = n <= completed;
            var createdAt = BaseTime.AddHours(items.Count);
            var completedAt = isCompleted ? createdAt.AddDays(1) : (DateTimeOffset?)null;

            items.Add(new TodoItem
            {
                Id = Guid.Parse($"{idPrefix}0000000-0000-0000-0000-{n:D12}"),
                Title = $"{userId ?? "no-user"} {(isCompleted ? "completed" : "open")} {n:D2}",
                Description = $"MCP test todo {idPrefix}{n:D2}",
                UserId = userId,
                Priority = (TodoPriority)(n % 3),
                IsCompleted = isCompleted,
                CreatedAtUtc = createdAt,
                DueAtUtc = n % 2 == 0 ? createdAt.AddDays(7) : null,
                CompletedAtUtc = completedAt,
                UpdatedAtUtc = completedAt ?? createdAt,
                Version = 1
            });
        }
    }
}