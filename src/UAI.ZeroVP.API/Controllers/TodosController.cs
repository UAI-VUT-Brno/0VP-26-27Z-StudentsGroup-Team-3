
using Microsoft.AspNetCore.Mvc;
using UAI.ZeroVP.API.Mapping;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.API.Controllers;

/// <summary>
/// REST API endpoints for managing todo items.
/// The controller depends strictly on ITodoRepository,
/// without knowledge of the underlying storage implementation.
/// </summary>
[ApiController]
[Route("api/todos")]
[Produces("application/json")]
public sealed class TodosController : ControllerBase
{
    private readonly ITodoRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TodosController> _logger;

    /// <summary>
    /// Initializes the controller with dependencies from the DI container.
    /// </summary>
    public TodosController(
        ITodoRepository repository,
        TimeProvider timeProvider,
        ILogger<TodosController> logger)
    {
        _repository = repository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Returns a list of todos, optionally filtered by completion status and user.
    /// </summary>
    /// <param name="isCompleted">
    /// Optional filter (true/false); omit for all items.
    /// </param>
    /// <param name="userId">
    /// Optional user identifier ("My Tasks"); omit for todos of all users.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">
    /// List of todos sorted by priority and creation date.
    /// </response>
    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<TodoItemDto>),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TodoItemDto>>> GetAll(
        [FromQuery] bool? isCompleted,
        [FromQuery] string? userId,
        CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(
            isCompleted,
            TodoMapper.NormalizeUserId(userId),
            cancellationToken);

        return Ok(items.Select(item => item.ToDto()).ToList());
    }

    /// <summary>Returns a single todo item by ID.</summary>
    /// <response code="200">The requested todo item.</response>
    /// <response code="404">Todo item was not found.</response>
    [HttpGet("{id:guid}", Name = nameof(GetById))]
    [ProducesResponseType(
        typeof(TodoItemDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TodoItemDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(
            id,
            cancellationToken);

        return item is null
            ? NotFoundTodo(id)
            : Ok(item.ToDto());
    }

    /// <summary>Creates a new todo item.</summary>
    /// <response code="201">Todo created successfully.</response>
    /// <response code="400">Request validation failed.</response>
    [HttpPost]
    [ProducesResponseType(
        typeof(TodoItemDto),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TodoItemDto>> Create(
        [FromBody] CreateTodoItemRequest request,
        CancellationToken cancellationToken)
    {
        var entity = request.ToEntity(_timeProvider.GetUtcNow());

        var created = await _repository.AddAsync(
            entity,
            cancellationToken);

        _logger.LogInformation(
            "Created todo item {TodoId} ({Title}).",
            created.Id,
            created.Title);

        return CreatedAtRoute(
            nameof(GetById),
            new { id = created.Id },
            created.ToDto());
    }

    /// <summary>
    /// Creates a weekly sequence of independent todo items.
    /// </summary>
    /// <remarks>
    /// Creates the first occurrence and RepeatCount additional
    /// occurrences, each exactly seven days apart.
    /// Every occurrence has a unique ID.
    /// </remarks>
    /// <response code="201">
    /// All weekly occurrences created successfully.
    /// </response>
    /// <response code="400">Request validation failed.</response>
    [HttpPost("repeat")]
    [ProducesResponseType(
        typeof(IReadOnlyList<TodoItemDto>),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TodoItemDto>>> Repeat(
        [FromBody] CreateRecurringTodoRequest request,
        CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow();
        var firstDueAtUtc = request.FirstDueAtUtc!.Value;

        var occurrences = new List<TodoItem>(
            request.RepeatCount + 1);

        for (var i = 0; i <= request.RepeatCount; i++)
        {
            occurrences.Add(new TodoItem
            {
                Id = Guid.NewGuid(),
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description)
                    ? null
                    : request.Description.Trim(),
                UserId = TodoMapper.NormalizeUserId(request.UserId),
                Priority = request.Priority,
                IsCompleted = false,
                CreatedAtUtc = nowUtc,
                DueAtUtc = firstDueAtUtc.AddDays(i * 7),
                CompletedAtUtc = null,
                UpdatedAtUtc = nowUtc
            });
        }

        var created = await _repository.AddRangeAsync(
            occurrences,
            cancellationToken);

        var result = created
            .Select(item => item.ToDto())
            .ToList();

        _logger.LogInformation(
            "Created {Count} weekly todo occurrences starting at {FirstDueAtUtc}.",
            result.Count,
            firstDueAtUtc);

        return StatusCode(
            StatusCodes.Status201Created,
            result);
    }

    /// <summary>
    /// Updates an existing todo item using optimistic concurrency control.
    /// </summary>
    /// <response code="200">Updated todo item.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="404">Todo item was not found.</response>
    /// <response code="409">
    /// The supplied todo version is outdated.
    /// </response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(
        typeof(TodoItemDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TodoItemDto>> Update(
        Guid id,
        [FromBody] UpdateTodoItemRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(
            id,
            cancellationToken);

        if (entity is null)
        {
            return NotFoundTodo(id);
        }

        request.ApplyTo(
            entity,
            _timeProvider.GetUtcNow());

        // Use the version supplied by the client.
        // The repository validates it atomically.
        entity.Version = request.Version;

        try
        {
            var updated = await _repository.UpdateAsync(
                entity,
                cancellationToken);

            return updated
                ? Ok(entity.ToDto())
                : NotFoundTodo(id);
        }
        catch (TodoConcurrencyException ex)
        {
            return VersionConflict(ex);
        }
    }

    /// <summary>
    /// Marks a todo item as completed using optimistic concurrency control.
    /// </summary>
    /// <response code="200">
    /// Todo item with updated completion state.
    /// </response>
    /// <response code="404">Todo item was not found.</response>
    /// <response code="409">
    /// The todo was modified by another operation.
    /// </response>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(
        typeof(TodoItemDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TodoItemDto>> Complete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(
            id,
            cancellationToken);

        if (entity is null)
        {
            return NotFoundTodo(id);
        }

        entity.SetCompletionState(
            isCompleted: true,
            _timeProvider.GetUtcNow());

        try
        {
            var updated = await _repository.UpdateAsync(
                entity,
                cancellationToken);

            return updated
                ? Ok(entity.ToDto())
                : NotFoundTodo(id);
        }
        catch (TodoConcurrencyException ex)
        {
            return VersionConflict(ex);
        }
    }

    /// <summary>Deletes a todo item.</summary>
    /// <response code="204">Todo item deleted successfully.</response>
    /// <response code="404">Todo item was not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!await _repository.DeleteAsync(
            id,
            cancellationToken))
        {
            return NotFoundTodo(id);
        }

        _logger.LogInformation(
            "Deleted todo item {TodoId}.",
            id);

        return NoContent();
    }

    /// <summary>
    /// Returns a standardized HTTP 404 ProblemDetails response.
    /// </summary>
    private ObjectResult NotFoundTodo(Guid id) =>
        Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Todo Not Found",
            detail: $"Todo item with ID {id} does not exist.");

    /// <summary>
    /// Returns HTTP 409 when a stale update is detected.
    /// </summary>
    private ObjectResult VersionConflict(
        TodoConcurrencyException ex) =>
        Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Todo Version Conflict",
            detail: ex.Message);
}
