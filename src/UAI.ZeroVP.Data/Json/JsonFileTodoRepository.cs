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

    /// <summary>
    /// Acquires an exclusive cross-process lock for the shared JSON storage.
    /// The lock is released when the returned stream is disposed.
    /// </summary>
    private FileStream AcquireCrossProcessLock(
        CancellationToken cancellationToken = default)
    {
        var lockPath = Path.GetFullPath(_filePath) + ".lock";
        var directory = Path.GetDirectoryName(lockPath)!;

        Directory.CreateDirectory(directory);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }

        throw new TimeoutException(
            $"Could not acquire storage lock for '{_filePath}'.");
    }

    private void InitializeStorage()
    {
        lock (FileLock)
        {
            using var processLock = AcquireCrossProcessLock();

            if (!File.Exists(_filePath))
            {
                _items.Clear();

                if (_seedSampleData)
                {
                    Seed();
                    PersistToFile();
                }

                return;
            }

            try
            {
                ReloadFromFile();
            }
            catch (JsonException ex)
            {
                var backupPath = _filePath
                    + ".corrupt-"
                    + Guid.NewGuid().ToString("N")
                    + ".bak";

                File.Move(_filePath, backupPath);

                _logger.LogError(
                    ex,
                    "Corrupted JSON storage moved from '{FilePath}' to '{BackupPath}'.",
                    _filePath,
                    backupPath);

                _items.Clear();

                if (_seedSampleData)
                {
                    Seed();
                }

                PersistToFile();
            }
        }
    }

    /// <summary>
    /// Reloads the latest tasks from the shared JSON file.
    /// Must be called while holding both storage locks.
    /// Invalid JSON must never silently replace the current state.
    /// </summary>
    private void ReloadFromFile()
    {
        if (!File.Exists(_filePath))
        {
            _items.Clear();
            return;
        }

        var json = File.ReadAllText(_filePath);

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException("JSON storage file is empty.");
        }

        var items = JsonSerializer.Deserialize<List<TodoItem>>(
            json, JsonOptions)
            ?? throw new JsonException("JSON storage contains null.");

        var loaded = new Dictionary<Guid, TodoItem>();

        foreach (var item in items)
        {
            if (item is null)
            {
                throw new JsonException(
                    "JSON storage contains a null todo item.");
            }

            if (!loaded.TryAdd(item.Id, item))
            {
                throw new JsonException(
                    $"JSON storage contains duplicate ID {item.Id}.");
            }
        }

        _items.Clear();

        foreach (var item in loaded.Values)
        {
            _items[item.Id] = item;
        }
    }

    /// <summary>
    /// Executes a synchronized read-modify-write operation.
    /// Prevents concurrent processes from overwriting each other's changes.
    /// Restores the previous in-memory state if the operation fails.
    /// </summary>
    private TResult ExecuteWrite<TResult>(
        Func<(TResult Result, bool Changed)> mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        cancellationToken.ThrowIfCancellationRequested();

        lock (FileLock)
        {
            using var processLock = AcquireCrossProcessLock(cancellationToken);

            ReloadFromFile();
            cancellationToken.ThrowIfCancellationRequested();

            var previousState = _items.Values
                .Select(item => item.Clone())
                .ToList();

            try
            {
                var (result, changed) = mutation();

                if (changed)
                {
                    PersistToFile();
                }

                return result;
            }
            catch
            {
                _items.Clear();

                foreach (var item in previousState)
                {
                    _items[item.Id] = item;
                }

                throw;
            }
        }
    }

    private void PersistToFile()
    {
        lock (FileLock)
        {
            var directory = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var itemsList = _items.Values
                .Select(item => item.Clone())
                .ToList();

            var json = JsonSerializer.Serialize(
                itemsList,
                JsonOptions);

            var tempFilePath = _filePath + "." +
                Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                File.WriteAllText(tempFilePath, json);

                File.Move(
                    tempFilePath,
                    _filePath,
                    overwrite: true);
            }
            finally
            {
                // Remove an unfinished temporary file if persistence fails.
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception ex) when (
                    ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not delete temporary JSON file '{FilePath}'.",
                        tempFilePath);
                }
            }
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TodoItem>> GetAllAsync(
        bool? isCompleted = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (FileLock)
        {
            using var processLock = AcquireCrossProcessLock(cancellationToken);
            ReloadFromFile();

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
    public Task<TodoItem?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (FileLock)
        {
            using var processLock = AcquireCrossProcessLock(cancellationToken);
            ReloadFromFile();

            return Task.FromResult(
                _items.TryGetValue(id, out var item)
                    ? item.Clone()
                    : null);
        }
    }

    /// <inheritdoc />
    public Task<TodoItem> AddAsync(
        TodoItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var stored = item.Clone();

        var created = ExecuteWrite(() =>
        {
            if (!_items.TryAdd(stored.Id, stored))
            {
                throw new InvalidOperationException(
                    $"A todo item with ID {stored.Id} already exists.");
            }

            return (Result: stored.Clone(), Changed: true);
        }, cancellationToken);

        return Task.FromResult(created);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TodoItem>> AddRangeAsync(
        IReadOnlyList<TodoItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var stored = items.Select(item =>
        {
            ArgumentNullException.ThrowIfNull(item);
            return item.Clone();
        }).ToList();

        IReadOnlyList<TodoItem> created = ExecuteWrite(() =>
        {
            var ids = new HashSet<Guid>();

            foreach (var item in stored)
            {
                if (!ids.Add(item.Id) || _items.ContainsKey(item.Id))
                {
                    throw new InvalidOperationException(
                        $"A todo item with ID {item.Id} already exists.");
                }
            }

            foreach (var item in stored)
            {
                _items[item.Id] = item;
            }

            IReadOnlyList<TodoItem> result = stored
                .Select(item => item.Clone())
                .ToList();

            return (Result: result, Changed: stored.Count > 0);
        }, cancellationToken);

        return Task.FromResult(created);
    }

    /// <inheritdoc />
    public Task<bool> UpdateAsync(
        TodoItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var replacement = item.Clone();

        var updated = ExecuteWrite(() =>
        {
            if (!_items.TryGetValue(replacement.Id, out var existing))
            {
                return (Result: false, Changed: false);
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

            return (Result: true, Changed: true);
        }, cancellationToken);

        if (updated)
        {
            item.Version = replacement.Version;
        }

        return Task.FromResult(updated);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var deleted = ExecuteWrite(() =>
        {
            var removed = _items.TryRemove(id, out _);

            return (Result: removed, Changed: removed);
        }, cancellationToken);

        return Task.FromResult(deleted);
    }

    /// <inheritdoc />
    public Task<int> CountAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (FileLock)
        {
            using var processLock = AcquireCrossProcessLock(cancellationToken);
            ReloadFromFile();

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
