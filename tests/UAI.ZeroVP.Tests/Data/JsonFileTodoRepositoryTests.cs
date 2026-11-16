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

    [Fact]
    public async Task UserId_PersistsAcrossRepositoryInstances()
    {
        // Arrange
        var options = new JsonFileTodoOptions { FilePath = _tempFilePath, SeedSampleData = false };
        var repository = new JsonFileTodoRepository(options);
        var item = CreateUserItem("Alice task", "alice", isCompleted: false);

        await repository.AddAsync(item);

        // Act
        var anotherRepository = new JsonFileTodoRepository(options);
        var loaded = await anotherRepository.GetByIdAsync(item.Id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("alice", loaded.UserId);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByUserIdAndCompletionStatus()
    {
        // Arrange
        var options = new JsonFileTodoOptions { FilePath = _tempFilePath, SeedSampleData = false };
        var repository = new JsonFileTodoRepository(options);

        await repository.AddAsync(CreateUserItem("Alice open", "alice", isCompleted: false));
        await repository.AddAsync(CreateUserItem("Alice completed", "alice", isCompleted: true));
        await repository.AddAsync(CreateUserItem("Bob completed", "bob", isCompleted: true));
        await repository.AddAsync(CreateUserItem("Untagged completed", null, isCompleted: true));

        // Act
        var aliceItems = await repository.GetAllAsync(userId: "alice");
        var aliceCompleted = await repository.GetAllAsync(isCompleted: true, userId: "alice");

        // Assert
        Assert.Equal(2, aliceItems.Count);
        Assert.All(aliceItems, todo => Assert.Equal("alice", todo.UserId));

        var item = Assert.Single(aliceCompleted);
        Assert.Equal("Alice completed", item.Title);
    }

    [Fact]
    public async Task LegacyJsonWithoutUserId_LoadsWithNullUserId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var legacyJson = $$"""
            [
              {
                "Id": "{{id}}",
                "Title": "Legacy task",
                "Description": null,
                "Priority": "Normal",
                "IsCompleted": false,
                "CreatedAtUtc": "2026-10-01T10:00:00+00:00",
                "DueAtUtc": null,
                "CompletedAtUtc": null,
                "UpdatedAtUtc": "2026-10-01T10:00:00+00:00",
                "Version": 1
              }
            ]
            """;

        await File.WriteAllTextAsync(_tempFilePath, legacyJson);

        var options = new JsonFileTodoOptions { FilePath = _tempFilePath, SeedSampleData = false };

        // Act
        var repository = new JsonFileTodoRepository(options);
        var loaded = await repository.GetByIdAsync(id);
        var all = await repository.GetAllAsync();
        var filtered = await repository.GetAllAsync(userId: "alice");

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("Legacy task", loaded.Title);
        Assert.Null(loaded.UserId);

        var stored = Assert.Single(all);
        Assert.Equal(id, stored.Id);

        Assert.Empty(filtered);

        Assert.Empty(Directory.GetFiles(_tempDirectory, "todos.json.corrupt-*.bak"));
    }

    private static TodoItem CreateUserItem(string title, string? userId, bool isCompleted)
    {
        var now = DateTimeOffset.UtcNow;

        return new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Priority = TodoPriority.Normal,
            IsCompleted = isCompleted,
            CreatedAtUtc = now,
            CompletedAtUtc = isCompleted ? now : null,
            UpdatedAtUtc = now,
            UserId = userId
        };
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
