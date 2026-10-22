using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Data.Json;

/// <summary>
/// Persistent file-based repository that serializes tasks into a JSON file.
/// </summary>
public sealed class JsonFileTodoRepository : ITodoRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly object FileLock = new();

    private readonly ConcurrentDictionary<Guid, TodoItem> _items = new();
    private readonly string _filePath;
    private readonly bool _seedSampleData;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JsonFileTodoRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonFileTodoRepository"/> class.
    /// </summary>
    /// <param name="options">Configuration options for persistent JSON storage.</param>
    /// <param name="timeProvider">Time provider abstraction for testability.</param>
    /// <param name="logger">Logger instance for capturing storage diagnostic events.</param>
    public JsonFileTodoRepository(
        JsonFileTodoOptions? options = null,
        TimeProvider? timeProvider = null,
        ILogger<JsonFileTodoRepository>? logger = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<JsonFileTodoRepository>.Instance;

        var opt = options ?? new JsonFileTodoOptions();
        _filePath = opt.FilePath;
        _seedSampleData = opt.SeedSampleData;

        InitializeStorage();
    }

    private void InitializeStorage()
    {
        lock (FileLock)
        {
            if (!File.Exists(_filePath))
            {
                if (_seedSampleData)
                {
                    Seed();
                    PersistToFile();
                }
                return;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                var deserialized = JsonSerializer.Deserialize<List<TodoItem>>(json, JsonOptions);
                if (deserialized is not null)
                {
                    foreach (var item in deserialized)
                    {
                        _items[item.Id] = item;
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Persistent JSON file at '{FilePath}' is corrupted. Initializing clean repository state.", _filePath);
                _items.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading tasks from '{FilePath}'. Initializing clean state.", _filePath);
                _items.Clear();
            }
        }
    }

    private void PersistToFile()
    {
        lock (FileLock)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var itemsList = _items.Values.Select(i => i.Clone()).ToList();
            var json = JsonSerializer.Serialize(itemsList, JsonOptions);

            var tempFilePath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tempFilePath, json);
            File.Move(tempFilePath, _filePath, overwrite: true);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TodoItem>> GetAllAsync(bool? isCompleted = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (FileLock)
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

        lock (FileLock)
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

        lock (FileLock)
        {
            if (!_items.TryAdd(stored.Id, stored))
            {
                throw new InvalidOperationException($"A todo item with ID {stored.Id} already exists.");
            }

            PersistToFile();
        }

        return Task.FromResult(stored.Clone());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TodoItem>> AddRangeAsync(IReadOnlyList<TodoItem> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        cancellationToken.ThrowIfCancellationRequested();

        var stored = items.Select(item =>
        {
            ArgumentNullException.ThrowIfNull(item);
            return item.Clone();
        }).ToList();

        lock (FileLock)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ids = new HashSet<Guid>();
            foreach (var item in stored)
            {
                if (!ids.Add(item.Id) || _items.ContainsKey(item.Id))
                {
                    throw new InvalidOperationException($"A todo item with ID {item.Id} already exists.");
                }
            }

            var addedIds = new List<Guid>();
            try
            {
                foreach (var item in stored)
                {
                    if (!_items.TryAdd(item.Id, item))
                    {
                        throw new InvalidOperationException($"Failed to add todo item with ID {item.Id}.");
                    }
                    addedIds.Add(item.Id);
                }

                PersistToFile();

                IReadOnlyList<TodoItem> result = stored.Select(item => item.Clone()).ToList();
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
    public Task<bool> UpdateAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();

        var replacement = item.Clone();

        lock (FileLock)
        {
            cancellationToken.ThrowIfCancellationRequested();

            while (_items.TryGetValue(item.Id, out var existing))
            {
                if (_items.TryUpdate(item.Id, replacement, existing))
                {
                    PersistToFile();
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (FileLock)
        {
            if (_items.TryRemove(id, out _))
            {
                PersistToFile();
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (FileLock)
        {
            return Task.FromResult(_items.Count);
        }
    }

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
