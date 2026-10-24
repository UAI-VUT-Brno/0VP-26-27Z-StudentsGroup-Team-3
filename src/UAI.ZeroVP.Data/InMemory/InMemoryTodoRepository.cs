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
    private readonly object _writeLock = new();
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
    public Task<IReadOnlyList<TodoItem>> GetAllAsync(
        bool? isCompleted = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_writeLock)
        {
            IReadOnlyList<TodoItem> result = _items.Values
                .Where(item => isCompleted is null || item.IsCompleted == isCompleted)
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => item.CreatedAtUtc)
                .Select(item => item.Clone())
                .ToList();

            return Task.FromResult(result);
        }
    }


    /// <inheritdoc />
    public Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_writeLock)
        {
            return Task.FromResult(_items.TryGetValue(id, out var item) ? item.Clone() : null);
        }
    }


    /// <inheritdoc />
    public Task<TodoItem> AddAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();

        var stored = item.Clone();

        lock (_writeLock)
        {
            if (!_items.TryAdd(stored.Id, stored))
            {
                throw new InvalidOperationException(
                    $"A todo item with ID {stored.Id} already exists.");
            }
        }

        return Task.FromResult(stored.Clone());
    }


    /// <inheritdoc />
    public Task<IReadOnlyList<TodoItem>> AddRangeAsync(
        IReadOnlyList<TodoItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        cancellationToken.ThrowIfCancellationRequested();

        var stored = items.Select(item =>
        {
            ArgumentNullException.ThrowIfNull(item);
            return item.Clone();
        }).ToList();

        lock (_writeLock)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ids = new HashSet<Guid>();

            foreach (var item in stored)
            {
                if (!ids.Add(item.Id) || _items.ContainsKey(item.Id))
                {
                    throw new InvalidOperationException(
                        $"A todo item with ID {item.Id} already exists.");
                }
            }

            var addedIds = new List<Guid>();

            try
            {
                foreach (var item in stored)
                {
                    if (!_items.TryAdd(item.Id, item))
                    {
                        throw new InvalidOperationException(
                            $"Failed to add todo item with ID {item.Id}.");
                    }

                    addedIds.Add(item.Id);
                }

                IReadOnlyList<TodoItem> result =
                    stored.Select(item => item.Clone()).ToList();

                return Task.FromResult(result);
            }
            catch
            {
                foreach (var id in addedIds)
                {
                    _items.TryRemove(id, out _);
                }

                throw;
            }
        }
    }

    /// <inheritdoc />
    public Task<bool> UpdateAsync(
        TodoItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();

        var replacement = item.Clone();

        lock (_writeLock)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_items.TryGetValue(replacement.Id, out var existing))
            {
                return Task.FromResult(false);
            }

            if (existing.Version != replacement.Version)
            {
                throw new TodoConcurrencyException(
                    replacement.Id,
                    replacement.Version,
                    existing.Version);
            }

            replacement.Version = checked(existing.Version + 1);

            _items[replacement.Id] = replacement;

            item.Version = replacement.Version;

            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_writeLock)
        {
            return Task.FromResult(_items.TryRemove(id, out _));
        }
    }


    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_writeLock)
        {
            return Task.FromResult(_items.Count);
        }
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
