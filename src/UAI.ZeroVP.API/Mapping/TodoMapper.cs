using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.API.Mapping;

/// <summary>
/// Conversions between the data layer entity and API data transfer objects.
/// Mapping belongs in the API layer, as it is the only layer aware of both sides.
/// </summary>
public static class TodoMapper
{
    /// <summary>Converts an entity to an output DTO (internal fields are omitted).</summary>
    public static TodoItemDto ToDto(this TodoItem entity) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        Description = entity.Description,
        UserId = entity.UserId,
        Priority = entity.Priority,
        IsCompleted = entity.IsCompleted,
        CreatedAtUtc = entity.CreatedAtUtc,
        DueAtUtc = entity.DueAtUtc,
        CompletedAtUtc = entity.CompletedAtUtc,
        Version = entity.Version,
    };

    /// <summary>Creates a new entity from a request to create a todo item.</summary>
    /// <param name="request">Data from the request body.</param>
    /// <param name="nowUtc">Current timestamp passed externally to ensure testability.</param>
    public static TodoItem ToEntity(this CreateTodoItemRequest request, DateTimeOffset nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        Title = request.Title.Trim(),
        Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
        UserId = NormalizeUserId(request.UserId),
        Priority = request.Priority,
        IsCompleted = false,
        CreatedAtUtc = nowUtc,
        DueAtUtc = null,
        CompletedAtUtc = null,
        UpdatedAtUtc = nowUtc
    };

    /// <summary>
    /// Maps request data onto an existing entity.
    /// Handles completion state transitions to keep <c>CompletedAtUtc</c> in sync with the flag.
    /// </summary>
    public static void ApplyTo(this UpdateTodoItemRequest request, TodoItem entity, DateTimeOffset nowUtc)
    {
        entity.Title = request.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.UserId = NormalizeUserId(request.UserId);
        entity.Priority = request.Priority;
        entity.SetCompletionState(request.IsCompleted, nowUtc);
        entity.UpdatedAtUtc = nowUtc;
    }

    /// <summary>Sets the completion flag and calculates the completion timestamp.</summary>
    public static void SetCompletionState(this TodoItem entity, bool isCompleted, DateTimeOffset nowUtc)
    {
        if (isCompleted && !entity.IsCompleted)
        {
            entity.CompletedAtUtc = nowUtc;
        }
        else if (!isCompleted)
        {
            entity.CompletedAtUtc = null;
        }

        entity.IsCompleted = isCompleted;
        entity.UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Normalizes a user identifier: trims surrounding whitespace and converts
    /// an empty or whitespace-only value to <c>null</c> (no user assigned).
    /// </summary>
    public static string? NormalizeUserId(string? userId) =>
        string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
}
