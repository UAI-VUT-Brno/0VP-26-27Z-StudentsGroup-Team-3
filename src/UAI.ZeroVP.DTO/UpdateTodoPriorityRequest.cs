using System.ComponentModel.DataAnnotations;

namespace UAI.ZeroVP.DTO;

/// <summary>
/// Input for changing only the priority of a todo item
/// (HTTP PATCH /api/todos/{id}/priority).
/// All other properties of the todo item stay unchanged.
/// </summary>
public sealed record UpdateTodoPriorityRequest
{
    /// <summary>
    /// New priority of the todo item. Required.
    /// <para>
    /// Declared as nullable so that a missing value is reported by
    /// <see cref="RequiredAttribute"/> instead of silently defaulting.
    /// </para>
    /// </summary>
    [Required(ErrorMessage = "Priority is required.")]
    [EnumDataType(typeof(TodoPriority), ErrorMessage = "Invalid priority value.")]
    public TodoPriority? Priority { get; init; }

    /// <summary>Expected version of the todo item being updated.
    /// Required; must match the currently stored version.</summary>
    [Required(ErrorMessage = "Version is required.")]
    [Range(1, long.MaxValue, ErrorMessage = "Version must be at least 1.")]
    public long? Version { get; init; }
}
