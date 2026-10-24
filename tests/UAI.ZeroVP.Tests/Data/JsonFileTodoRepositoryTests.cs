using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.Data.Json;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Tests.Data;

public sealed class JsonFileTodoRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _tempFilePath;

    public JsonFileTodoRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ZeroVPTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _tempFilePath = Path.Combine(_tempDirectory, "todos.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Tasks_PersistAcrossRepositoryInstances()
    {
        var options = new JsonFileTodoOptions { FilePath = _tempFilePath, SeedSampleData = false };
        var repo1 = new JsonFileTodoRepository(options, TimeProvider.System, NullLogger<JsonFileTodoRepository>.Instance);

        var item = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Persistent Task",
            Priority = TodoPriority.High,
            IsCompleted = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await repo1.AddAsync(item);

        // Act - create a second repository instance targeting the same file
        var repo2 = new JsonFileTodoRepository(options, TimeProvider.System, NullLogger<JsonFileTodoRepository>.Instance);
        var loaded = await repo2.GetByIdAsync(item.Id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("Persistent Task", loaded.Title);
        Assert.Equal(1, await repo2.CountAsync());
    }

    [Fact]
    public async Task NonExistingFile_InitializesCleanStateWithoutCrashing()
    {
        var options = new JsonFileTodoOptions { FilePath = _tempFilePath, SeedSampleData = false };
        var repo = new JsonFileTodoRepository(options, TimeProvider.System, NullLogger<JsonFileTodoRepository>.Instance);

        Assert.Equal(0, await repo.CountAsync());
        var all = await repo.GetAllAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task AddRangeAsync_PersistsAllItemsAcrossInstances()
    {
        var options = new JsonFileTodoOptions
        {
            FilePath = _tempFilePath,
            SeedSampleData = false
        };

        var repository = new JsonFileTodoRepository(options);
        var now = DateTimeOffset.UtcNow;

        var items = Enumerable.Range(1, 3)
            .Select(index => new TodoItem
            {
                Id = Guid.NewGuid(),
                Title = $"Batch task {index}",
                Priority = TodoPriority.Normal,
                IsCompleted = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            })
            .ToList();

        var created = await repository.AddRangeAsync(items);

        Assert.Equal(3, created.Count);

        var anotherRepository = new JsonFileTodoRepository(options);
        var stored = await anotherRepository.GetAllAsync();

        Assert.Equal(3, stored.Count);

        foreach (var item in items)
        {
            Assert.Contains(stored, storedItem =>
                storedItem.Id == item.Id &&
                storedItem.Title == item.Title);
        }
    }

    [Fact]
    public async Task AddRangeAsync_WithExistingId_DoesNotPersistPartialBatch()
    {
        var options = new JsonFileTodoOptions
        {
            FilePath = _tempFilePath,
            SeedSampleData = false
        };

        var repository = new JsonFileTodoRepository(options);
        var now = DateTimeOffset.UtcNow;

        var existing = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Existing task",
            Priority = TodoPriority.High,
            IsCompleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.AddAsync(existing);

        var newItem = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Should not be persisted",
            Priority = TodoPriority.Normal,
            IsCompleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await repository.AddRangeAsync(
                new[] { newItem, existing.Clone() }));

        var anotherRepository = new JsonFileTodoRepository(options);
        var stored = await anotherRepository.GetAllAsync();

        Assert.Single(stored);
        Assert.Equal(existing.Id, stored[0].Id);
        Assert.Null(await anotherRepository.GetByIdAsync(newItem.Id));
    }

    [Fact]
    public async Task CorruptedJsonFile_LogsErrorAndStartsCleanWithoutThrowing()
    {
        const string corruptedContent = "{ corrupted invalid json content [[[";

        await File.WriteAllTextAsync(_tempFilePath, corruptedContent);

        var options = new JsonFileTodoOptions
        {
            FilePath = _tempFilePath,
            SeedSampleData = false
        };

        var logger = new RecordingLogger();

        var repository = new JsonFileTodoRepository(
            options,
            TimeProvider.System,
            logger);

        // The repository must recover with an empty valid state.
        Assert.Equal(0, await repository.CountAsync());
        Assert.Empty(await repository.GetAllAsync());

        // The corrupted file must be preserved as a backup.
        var backups = Directory.GetFiles(
            _tempDirectory,
            "todos.json.corrupt-*.bak");

        var backupPath = Assert.Single(backups);

        Assert.Equal(
            corruptedContent,
            await File.ReadAllTextAsync(backupPath));

        // The replacement storage must contain valid JSON.
        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(_tempFilePath));

        Assert.Equal(
            JsonValueKind.Array,
            document.RootElement.ValueKind);

        Assert.Equal(0, document.RootElement.GetArrayLength());

        // A real error log must be emitted.
        Assert.Contains(
            logger.Entries,
            entry =>
                entry.Level == LogLevel.Error &&
                entry.Exception is JsonException &&
                entry.Message.Contains(
                    "Corrupted JSON storage moved",
                    StringComparison.Ordinal));
    }

    private sealed class RecordingLogger : ILogger<JsonFileTodoRepository>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)>
            Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((
                logLevel,
                formatter(state, exception),
                exception));
        }
    }

}
