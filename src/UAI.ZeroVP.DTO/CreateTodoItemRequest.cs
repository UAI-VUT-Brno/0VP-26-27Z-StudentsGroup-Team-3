using System.ComponentModel.DataAnnotations;

namespace UAI.ZeroVP.DTO;

/// <summary>
/// Input for creating a new todo item (HTTP POST /api/todos).
/// Validation attributes are automatically evaluated by ASP.NET Core via <c>[ApiController]</c>.
/// </summary>
public sealed record CreateTodoItemRequest
{
    /// <summary>
    /// Short title of the todo item. Required, 1–200 characters.
    /// <para>
    /// Intentionally declared without the <c>required</c> keyword: JSON deserialization enforces that,
    /// so a completely missing field would result in a deserialization failure and the custom
    /// <c>[Required]</c> error message would never reach the client.
    /// </para>
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters.")]
    public string Title { get; init; } = string.Empty;

    /// <summary>Optional description, maximum of 1000 characters.</summary>
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; init; }

    /// <summary>Priority of the todo item. Defaults to <see cref="TodoPriority.Normal"/>.</summary>
    [EnumDataType(typeof(TodoPriority), ErrorMessage = "Invalid priority value.")]
    public TodoPriority Priority { get; init; } = TodoPriority.Normal;
}
