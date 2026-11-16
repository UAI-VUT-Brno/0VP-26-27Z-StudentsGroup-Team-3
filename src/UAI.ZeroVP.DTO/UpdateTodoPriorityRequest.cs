using System.ComponentModel.DataAnnotations;

namespace UAI.ZeroVP.DTO;

/// <summary>
/// Input for changing only the priority of a todo item.
/// </summary>
public sealed record UpdateTodoPriorityRequest
{
    /// <summary>New priority of the todo item.</summary>
    [Required(ErrorMessage = "Priority is required.")]
    [EnumDataType(typeof(TodoPriority), ErrorMessage = "Invalid priority value.")]
    public TodoPriority? Priority { get; init; }

    /// <summary>
    /// Expected version of the todo item being updated.
    /// Must match the currently stored version.
    /// </summary>
    [Range(1, long.MaxValue)]
    public long Version { get; init; }
}
