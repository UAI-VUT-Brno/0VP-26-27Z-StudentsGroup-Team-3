
using System.Text.Json;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.Data.Json;
using UAI.ZeroVP.DTO;
using Xunit;

namespace UAI.ZeroVP.Tests.Integration;

/// <summary>
/// Tests shared JSON storage across independent repository instances.
/// Each test uses a dedicated temporary JSON file.
/// </summary>
public sealed class JsonFileMultiInstanceTests
{
    [Fact]
    public async Task Changes_AreVisibleAcrossRepositoriesWithoutRestart()
    {
        using var storage = new TemporaryStorage();

        var repositoryA = CreateRepository(storage.FilePath);
        var repositoryB = CreateRepository(storage.FilePath);

        var created = await repositoryA.AddAsync(
            NewTodo("Original title"));

        var itemB = Assert.IsType<TodoItem>(
            await repositoryB.GetByIdAsync(created.Id));

        Assert.Equal("Original title", itemB.Title);
        Assert.Equal(1L, itemB.Version);

        itemB.Title = "Updated by instance B";

        Assert.True(await repositoryB.UpdateAsync(itemB));

        var itemA = Assert.IsType<TodoItem>(
            await repositoryA.GetByIdAsync(created.Id));

        Assert.Equal("Updated by instance B", itemA.Title);
        Assert.Equal(2L, itemA.Version);
    }

    [Fact]
    public async Task ConcurrentCreates_DoNotLoseRecords()
    {
        using var storage = new TemporaryStorage();

        var repositoryA = CreateRepository(storage.FilePath);
        var repositoryB = CreateRepository(storage.FilePath);

        const int requestCount = 80;

        var tasks = Enumerable.Range(0, requestCount)
            .Select(index => Task.Run(async () =>
            {
                var repository = index % 2 == 0
                    ? repositoryA
                    : repositoryB;

                await repository.AddAsync(
                    NewTodo($"Concurrent task {index}"));
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        var itemsA = await repositoryA.GetAllAsync();
        var itemsB = await repositoryB.GetAllAsync();

        Assert.Equal(requestCount, itemsA.Count);
        Assert.Equal(requestCount, itemsB.Count);

        Assert.Equal(
            requestCount,
            itemsA.Select(item => item.Id).Distinct().Count());

        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(storage.FilePath));

        Assert.Equal(
            JsonValueKind.Array,
            document.RootElement.ValueKind);

        Assert.Equal(
            requestCount,
            document.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task StaleUpdate_ThrowsConflictAndPreservesLatestData()
    {
        using var storage = new TemporaryStorage();

        var repositoryA = CreateRepository(storage.FilePath);
        var repositoryB = CreateRepository(storage.FilePath);

        var created = await repositoryA.AddAsync(
            NewTodo("Original title"));

        var itemA = Assert.IsType<TodoItem>(
            await repositoryA.GetByIdAsync(created.Id));

        var itemB = Assert.IsType<TodoItem>(
            await repositoryB.GetByIdAsync(created.Id));

        Assert.Equal(itemA.Version, itemB.Version);

        itemA.Title = "Updated by A";

        Assert.True(await repositoryA.UpdateAsync(itemA));

        itemB.Title = "Stale update by B";

        await Assert.ThrowsAsync<TodoConcurrencyException>(
            async () =>
            {
                await repositoryB.UpdateAsync(itemB);
            });

        var stored = Assert.IsType<TodoItem>(
            await repositoryB.GetByIdAsync(created.Id));

        Assert.Equal("Updated by A", stored.Title);
        Assert.Equal(2L, stored.Version);
    }

    [Fact]
    public async Task ConcurrentUpdates_OnlyOneVersionWins()
    {
        using var storage = new TemporaryStorage();

        var repositoryA = CreateRepository(storage.FilePath);
        var repositoryB = CreateRepository(storage.FilePath);

        var created = await repositoryA.AddAsync(
            NewTodo("Original title"));

        var itemA = Assert.IsType<TodoItem>(
            await repositoryA.GetByIdAsync(created.Id));

        var itemB = Assert.IsType<TodoItem>(
            await repositoryB.GetByIdAsync(created.Id));

        itemA.Title = "Concurrent update A";
        itemB.Title = "Concurrent update B";

        var results = await Task.WhenAll(
            Task.Run(() => TryUpdateAsync(repositoryA, itemA)),
            Task.Run(() => TryUpdateAsync(repositoryB, itemB)));

        Assert.Equal(
            1,
            results.Count(success => success));

        var stored = Assert.IsType<TodoItem>(
            await repositoryA.GetByIdAsync(created.Id));

        Assert.Equal(2L, stored.Version);

        Assert.Contains(
            stored.Title,
            new[]
            {
                "Concurrent update A",
                "Concurrent update B"
            });

        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(storage.FilePath));

        Assert.Single(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task NewRepositoryInstance_LoadsPreviouslySavedData()
    {
        using var storage = new TemporaryStorage();

        var repositoryA = CreateRepository(storage.FilePath);

        var created = await repositoryA.AddAsync(
            NewTodo("Persistent task"));

        // A new repository simulates reopening the storage.
        var repositoryB = CreateRepository(storage.FilePath);

        var restored = Assert.IsType<TodoItem>(
            await repositoryB.GetByIdAsync(created.Id));

        Assert.Equal(created.Id, restored.Id);
        Assert.Equal("Persistent task", restored.Title);
        Assert.Equal(1L, restored.Version);

        Assert.True(await repositoryB.DeleteAsync(created.Id));

        // The original instance must immediately observe deletion.
        Assert.Null(await repositoryA.GetByIdAsync(created.Id));
        Assert.Equal(0, await repositoryA.CountAsync());
    }

    [Fact]
    public async Task CorruptedJson_IsNotSilentlyOverwritten()
    {
        using var storage = new TemporaryStorage();

        var repository = CreateRepository(storage.FilePath);

        await repository.AddAsync(
            NewTodo("Existing task"));

        const string corruptedContent = "{ invalid JSON";

        await File.WriteAllTextAsync(
            storage.FilePath,
            corruptedContent);

        await Assert.ThrowsAsync<JsonException>(
            async () =>
            {
                await repository.GetAllAsync();
            });

        await Assert.ThrowsAsync<JsonException>(
            async () =>
            {
                await repository.AddAsync(
                    NewTodo("Should not be added"));
            });

        Assert.Equal(
            corruptedContent,
            await File.ReadAllTextAsync(storage.FilePath));
    }

    private static JsonFileTodoRepository CreateRepository(
        string filePath)
    {
        return new JsonFileTodoRepository(
            new JsonFileTodoOptions
            {
                FilePath = filePath,
                SeedSampleData = false
            });
    }

    private static TodoItem NewTodo(string title)
    {
        var now = DateTimeOffset.UtcNow;

        return new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = null,
            Priority = TodoPriority.Normal,
            IsCompleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CompletedAtUtc = null,
            Version = 1
        };
    }

    private static async Task<bool> TryUpdateAsync(
        JsonFileTodoRepository repository,
        TodoItem item)
    {
        try
        {
            return await repository.UpdateAsync(item);
        }
        catch (TodoConcurrencyException)
        {
            return false;
        }
    }

    private sealed class TemporaryStorage : IDisposable
    {
        private readonly string _directory;

        public string FilePath { get; }

        public TemporaryStorage()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "ZeroVP-JsonTests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_directory);

            FilePath = Path.Combine(
                _directory,
                "todos.json");
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(
                    _directory,
                    recursive: true);
            }
        }
    }
}
