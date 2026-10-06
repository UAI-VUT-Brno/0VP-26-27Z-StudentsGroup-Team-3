using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.Data.InMemory;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Tests.Data;

public class InMemoryTodoRepositoryTests
{
    private static InMemoryTodoRepository CreateEmptyRepository()
    {
        return new InMemoryTodoRepository(
            new InMemoryTodoOptions
            {
                SeedSampleData = false
            });
    }

    [Fact]
    public async Task AddAsync_AddsTodoItem()
    {
        // Arrange
        var repository = CreateEmptyRepository();

        var item = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Test task",
            Description = "Test description",
            Priority = TodoPriority.Normal,
            IsCompleted = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        // Act
        var result = await repository.AddAsync(item);

        // Assert
        Assert.Equal(item.Id, result.Id);
        Assert.Equal("Test task", result.Title);
        Assert.Equal(1, await repository.CountAsync());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsExistingTodoItem()
    {
        // Arrange
        var repository = CreateEmptyRepository();

        var item = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Find me",
            Priority = TodoPriority.Normal,
            IsCompleted = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await repository.AddAsync(item);

        // Act
        var result = await repository.GetByIdAsync(item.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(item.Id, result.Id);
        Assert.Equal("Find me", result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForUnknownId()
    {
        // Arrange
        var repository = CreateEmptyRepository();

        // Act
        var result = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesExistingTodoItem()
    {
        // Arrange
        var repository = CreateEmptyRepository();

        var item = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Delete me",
            Priority = TodoPriority.Normal,
            IsCompleted = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await repository.AddAsync(item);

        // Act
        var deleted = await repository.DeleteAsync(item.Id);

        // Assert
        Assert.True(deleted);
        Assert.Null(await repository.GetByIdAsync(item.Id));
        Assert.Equal(0, await repository.CountAsync());
    }
}
