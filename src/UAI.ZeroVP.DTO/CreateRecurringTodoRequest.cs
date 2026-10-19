
using System.ComponentModel.DataAnnotations;

namespace UAI.ZeroVP.DTO;

/// <summary>
/// Request for creating a weekly sequence of todo items.
/// The total number of items is RepeatCount + 1.
/// </summary>
public sealed record CreateRecurringTodoRequest : IValidatableObject
{
    /// <summary>Required task title, 1–200 characters.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "Title must be between 1 and 200 characters.")]
    public string Title { get; init; } = string.Empty;

    /// <summary>Optional description, maximum 1000 characters.</summary>
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; init; }

    /// <summary>Task priority.</summary>
    [EnumDataType(typeof(TodoPriority), ErrorMessage = "Invalid priority value.")]
    public TodoPriority Priority { get; init; } = TodoPriority.Normal;

    /// <summary>UTC due date of the first occurrence.</summary>
    [Required(ErrorMessage = "First due date is required.")]
    public DateTimeOffset? FirstDueAtUtc { get; init; }

    /// <summary>Number of additional weekly occurrences (1–52).</summary>
    [Range(1, 52, ErrorMessage = "Repeat count must be between 1 and 52.")]
    public int RepeatCount { get; init; }

    /// <summary>Validates the UTC timezone requirement.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FirstDueAtUtc.HasValue &&
            FirstDueAtUtc.Value.Offset != TimeSpan.Zero)
        {
            yield return new ValidationResult(
                "First due date must be in UTC.",
                new[] { nameof(FirstDueAtUtc) });
        }

        if (FirstDueAtUtc.HasValue &&
            RepeatCount is >= 1 and <= 52 &&
            FirstDueAtUtc.Value > DateTimeOffset.MaxValue.AddDays(-7 * RepeatCount))
        {
            yield return new ValidationResult(
                "The recurrence sequence exceeds the supported date range.",
                new[] { nameof(FirstDueAtUtc) });
        }
    }
}
