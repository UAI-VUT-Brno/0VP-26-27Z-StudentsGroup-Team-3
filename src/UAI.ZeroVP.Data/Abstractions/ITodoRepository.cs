using UAI.ZeroVP.Data.Entities;

namespace UAI.ZeroVP.Data.Abstractions;

/// <summary>
/// Contract for the todo item repository.
/// Methods are intentionally asynchronous: today the interface is backed by in-memory storage,
/// tomorrow it may be backed by a database in another container without requiring API changes.
/// </summary>
public interface ITodoRepository
{
    /// <summary>Returns all todo items, optionally filtered by completion status.</summary>
    /// <param name="isCompleted">Completion status filter; <c>null</c> = no filter.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    Task<IReadOnlyList<TodoItem>> GetAllAsync(bool? isCompleted = null, CancellationToken cancellationToken = default);

    /// <summary>Returns a todo item by its identifier, or <c>null</c> if it does not exist.</summary>
    Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Saves a new todo item and returns its resulting state, including the assigned <c>Id</c>.</summary>
    Task<TodoItem> AddAsync(TodoItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically saves a collection of new todo items.
    /// Returns the persisted items, including their assigned identifiers.
    /// The operation must not persist a partial collection.
    /// </summary>
    Task<IReadOnlyList<TodoItem>> AddRangeAsync(
        IReadOnlyList<TodoItem> items,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an existing todo item. Returns <c>false</c> if the record does not exist.</summary>
    Task<bool> UpdateAsync(TodoItem item, CancellationToken cancellationToken = default);

    /// <summary>Deletes a todo item. Returns <c>false</c> if the record does not exist.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Total count of todo items in the repository, used by the diagnostics endpoint.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
