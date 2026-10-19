namespace UAI.ZeroVP.DTO;

/// <summary>
/// Todo item as returned by the API to the client.
/// Intentionally excludes internal entity fields (e.g., <c>UpdatedAtUtc</c>) –
/// which is the primary reason the DTO exists as a separate layer.
/// </summary>
public sealed record TodoItemDto
{
    /// <summary>Unique identifier of the todo item.</summary>
    public required Guid Id { get; init; }

    /// <summary>Short title of the todo item.</summary>
    public required string Title { get; init; }

    /// <summary>Optional detailed description.</summary>
    public string? Description { get; init; }

    /// <summary>Priority of the todo item.</summary>
    public TodoPriority Priority { get; init; }

    /// <summary>Indicates whether the todo item is completed.</summary>
    public bool IsCompleted { get; init; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Optional due date and time (UTC), or null if no due date is set.</summary>
    public DateTimeOffset? DueAtUtc { get; init; }

    /// <summary>Completion timestamp (UTC), or <c>null</c> if the item is not completed.</summary>
    public DateTimeOffset? CompletedAtUtc { get; init; }
}
