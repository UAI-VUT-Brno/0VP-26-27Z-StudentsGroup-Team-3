using System.ComponentModel.DataAnnotations;

namespace UAI.ZeroVP.DTO;

/// <summary>
/// Input for a full update of a todo item (HTTP PUT /api/todos/{id}).
/// This is a full update – omitted properties will be overwritten with default values.
/// </summary>
public sealed record UpdateTodoItemRequest
{
    /// <summary>
    /// Short title of the todo item. Required, 1–200 characters.
    /// <para>
    /// Declared without the <c>required</c> keyword for the same reason as
    /// <see cref="CreateTodoItemRequest.Title"/>.
    /// </para>
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters.")]
    public string Title { get; init; } = string.Empty;

    /// <summary>Optional description, maximum of 1000 characters.</summary>
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; init; }

    /// <summary>Priority of the todo item.</summary>
    [EnumDataType(typeof(TodoPriority), ErrorMessage = "Invalid priority value.")]
    public TodoPriority Priority { get; init; } = TodoPriority.Normal;

    /// <summary>Indicates whether the todo item is completed.</summary>
    public bool IsCompleted { get; init; }

    /// <summary>Expected version of the todo item being updated.
    /// Must match the currently stored version.</summary>
    [Range(1, long.MaxValue)]
    public long Version { get; init; }
}
