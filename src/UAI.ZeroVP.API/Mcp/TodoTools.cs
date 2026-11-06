using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using UAI.ZeroVP.API.Mapping;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.API.Mcp;

/// <summary>
/// Read-only MCP tools backed by the existing todo repository.
/// </summary>
[McpServerToolType]
public sealed class TodoTools
{
    /// <summary>Lists todos using optional completion and user filters.</summary>
    [McpServerTool(
        Name = "list_todos",
        ReadOnly = true,
        Destructive = false,
        UseStructuredContent = true)]
    [Description(
        "Returns all matching todos in a todos array. " +
        "Both filters must match when supplied together. " +
        "No matches return an empty array.")]
    public static async Task<TodoListResult> ListTodos(
        [FromServices] ITodoRepository repository,
        [Description("Optional completion filter; omit to include both states.")]
        bool? isCompleted = null,
        [Description(
            "Optional user ID; trimmed, exact and case-sensitive. " +
            "Omit for all users. Empty or whitespace-only values are invalid.")]
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (userId is not null && string.IsNullOrWhiteSpace(userId))
        {
            throw new McpException(
                "INVALID_ARGUMENT: userId must not be empty or whitespace.");
        }

        var items = await repository.GetAllAsync(
            isCompleted,
            TodoMapper.NormalizeUserId(userId),
            cancellationToken);

        return new TodoListResult(
            items.Select(item => item.ToDto()).ToArray());
    }

    /// <summary>Retrieves one todo by its GUID identifier.</summary>
    [McpServerTool(
        Name = "get_todo",
        ReadOnly = true,
        Destructive = false,
        UseStructuredContent = true)]
    [Description(
        "Returns a todo object for the supplied ID. " +
        "A nonexistent ID produces a tool error containing TODO_NOT_FOUND.")]
    public static async Task<TodoDetailResult> GetTodo(
        [FromServices] ITodoRepository repository,
        [Description("Required todo ID in GUID format.")]
        string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var todoId))
        {
            throw new McpException(
                "INVALID_ARGUMENT: id must be a valid GUID.");
        }

        var item = await repository.GetByIdAsync(todoId, cancellationToken);

        if (item is null)
        {
            throw new McpException(
                "TODO_NOT_FOUND: No todo exists with the supplied ID.");
        }

        return new TodoDetailResult(item.ToDto());
    }
}

/// <summary>Structured result containing matching todos.</summary>
/// <param name="Todos">All matching public todo DTOs.</param>
public sealed record TodoListResult(
    [property: JsonPropertyName("todos")]
    IReadOnlyList<TodoItemDto> Todos);

/// <summary>Structured result containing a single todo.</summary>
/// <param name="Todo">The requested public todo DTO.</param>
public sealed record TodoDetailResult(
    [property: JsonPropertyName("todo")]
    TodoItemDto Todo);
