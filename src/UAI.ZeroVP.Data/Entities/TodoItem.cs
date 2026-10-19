using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Data.Entities;

/// <summary>
/// Todo item entity as stored in the data layer.
/// Unlike <see cref="TodoItemDto"/>, it also contains the internal <see cref="UpdatedAtUtc"/> field,
/// which is never exposed through the API.
/// </summary>
public sealed class TodoItem
{
    /// <summary>Unique identifier of the todo item.</summary>
    public required Guid Id { get; init; }

    /// <summary>Short title of the todo item.</summary>
    public required string Title { get; set; }

    /// <summary>Optional detailed description.</summary>
    public string? Description { get; set; }

    /// <summary>Priority of the todo item.</summary>
    public TodoPriority Priority { get; set; } = TodoPriority.Normal;

    /// <summary>Indicates whether the todo item is completed.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Optional due date and time (UTC).</summary>
    public DateTimeOffset? DueAtUtc { get; set; }

    /// <summary>Completion timestamp (UTC), or <c>null</c> if the item is not completed.</summary>
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>Internal audit timestamp for the last modification. Not exposed in the DTO.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Creates a deep copy of the entity. Used by the in-memory repository to prevent
    /// callers from mutating stored state outside the repository
    /// (in a database-backed store, this boundary is naturally handled by ORM/row mapping).
    /// </summary>
    public TodoItem Clone() => new()
    {
        Id = Id,
        Title = Title,
        Description = Description,
        Priority = Priority,
        IsCompleted = IsCompleted,
        CreatedAtUtc = CreatedAtUtc,
        DueAtUtc = DueAtUtc,
        CompletedAtUtc = CompletedAtUtc,
        UpdatedAtUtc = UpdatedAtUtc
    };
}
