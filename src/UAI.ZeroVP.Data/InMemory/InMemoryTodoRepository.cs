using System.Collections.Concurrent;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Data.InMemory;

/// <summary>
/// In-memory storage for todo items within the process memory space.
/// <para>
/// NOTE – key takeaway for Docker learning: data persists only inside the running process.
/// Stopping the container (or simply restarting the app) causes permanent data loss,
/// and each container replica maintains its own isolated, independent dataset.
/// </para>
/// <para>
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> ensures thread-safe concurrent access,
/// as the web server handles multiple incoming requests across parallel threads.
/// </para>
/// </summary>
public sealed class InMemoryTodoRepository : ITodoRepository
{
    private readonly ConcurrentDictionary<Guid, TodoItem> _items = new();
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes the repository and optionally seeds it with sample data.
    /// </summary>
    /// <param name="options">Repository configuration options.</param>
    /// <param name="timeProvider">Time provider instance, allowing time mocking in unit tests.</param>
    public InMemoryTodoRepository(InMemoryTodoOptions? options = null, TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;

        if (options?.SeedSampleData ?? true)
        {
            Seed();
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TodoItem>> GetAllAsync(bool? isCompleted = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<TodoItem> result = _items.Values
            .Where(item => isCompleted is null || item.IsCompleted == isCompleted)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.CreatedAtUtc)
            .Select(item => item.Clone())
            .ToList();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_items.TryGetValue(id, out var item) ? item.Clone() : null);
    }

    /// <inheritdoc />
    public Task<TodoItem> AddAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();

        var stored = item.Clone();
        if (!_items.TryAdd(stored.Id, stored))
        {
            throw new InvalidOperationException($"A todo item with ID {stored.Id} already exists.");
        }

        return Task.FromResult(stored.Clone());
    }

    /// <inheritdoc />
    public Task<bool> UpdateAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();

        var replacement = item.Clone();

        // TryUpdate compares the currently stored instance with the one read previously.
        // If swapped by another thread in the meantime, comparison fails and it retries.
        // Without this loop, a concurrent write would return false (404 "not found")
        // even though the record exists. Last write wins.
        while (_items.TryGetValue(item.Id, out var existing))
        {
            if (_items.TryUpdate(item.Id, replacement, existing))
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_items.TryRemove(id, out _));
    }

    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_items.Count);
    }

    /// <summary>Seeds sample todo items so the API does not return an empty collection on startup.</summary>
    private void Seed()
    {
        var now = _timeProvider.GetUtcNow();

        AddSeedItem("Prepare sample API", "Three projects: DTO, Data, API.", TodoPriority.High, isCompleted: true, now.AddDays(-2));
        AddSeedItem("Write Dockerfile", "Multi-stage build using SDK and runtime images.", TodoPriority.High, isCompleted: false, now.AddDays(-1));
        AddSeedItem("Test docker compose", "Run the API in two replicas.", TodoPriority.Normal, isCompleted: false, now.AddHours(-3));
        AddSeedItem("Clean up unused images", "docker image prune", TodoPriority.Low, isCompleted: false, now.AddMinutes(-30));
    }

    private void AddSeedItem(string title, string description, TodoPriority priority, bool isCompleted, DateTimeOffset createdAtUtc)
    {
        var item = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Priority = priority,
            IsCompleted = isCompleted,
            CreatedAtUtc = createdAtUtc,
            CompletedAtUtc = isCompleted ? createdAtUtc.AddHours(1) : null,
            UpdatedAtUtc = createdAtUtc
        };

        _items[item.Id] = item;
    }
}