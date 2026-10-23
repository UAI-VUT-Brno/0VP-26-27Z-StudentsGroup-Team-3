using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task CorruptedJsonFile_LogsErrorAndStartsCleanWithoutThrowing()
    {
        await File.WriteAllTextAsync(_tempFilePath, "{ corrupted invalid json content [[[");

        var options = new JsonFileTodoOptions { FilePath = _tempFilePath, SeedSampleData = false };
        var repo = new JsonFileTodoRepository(options, TimeProvider.System, NullLogger<JsonFileTodoRepository>.Instance);

        Assert.Equal(0, await repo.CountAsync());
        var all = await repo.GetAllAsync();
        Assert.Empty(all);
    }
}
