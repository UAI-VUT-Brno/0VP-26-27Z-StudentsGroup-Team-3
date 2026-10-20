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

    [Fact]
    public async Task UpdateAsync_UpdatesExistingTodoItem()
    {
        // Arrange
        var repository = CreateEmptyRepository();

        var item = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Original title",
            Priority = TodoPriority.Normal,
            IsCompleted = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await repository.AddAsync(item);

        item.Title = "Updated title";
        item.IsCompleted = true;
        item.UpdatedAtUtc = DateTimeOffset.UtcNow;

        // Act
        var updated = await repository.UpdateAsync(item);

        // Assert
        Assert.True(updated);

        var result = await repository.GetByIdAsync(item.Id);

        Assert.NotNull(result);
        Assert.Equal("Updated title", result.Title);
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsFalseForUnknownTodoItem()
    {
        // Arrange
        var repository = CreateEmptyRepository();

        var item = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Does not exist",
            Priority = TodoPriority.Normal,
            IsCompleted = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        // Act
        var updated = await repository.UpdateAsync(item);

        // Assert
        Assert.False(updated);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByCompletionStatus()
    {
        // Arrange
        var repository = CreateEmptyRepository();

        await repository.AddAsync(new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Completed task",
            Priority = TodoPriority.Normal,
            IsCompleted = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        await repository.AddAsync(new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Open task",
            Priority = TodoPriority.Normal,
            IsCompleted = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        // Act
        var completedItems = await repository.GetAllAsync(true);
        var openItems = await repository.GetAllAsync(false);

        // Assert
        Assert.Single(completedItems);
        Assert.Equal("Completed task", completedItems[0].Title);

        Assert.Single(openItems);
        Assert.Equal("Open task", openItems[0].Title);
    }

    [Fact]
    public async Task GetAllAsync_SortsByPriorityThenCreatedAt()
    {
        // Arrange
        var repository = CreateEmptyRepository();

        var older = DateTimeOffset.UtcNow.AddMinutes(-10);
        var newer = DateTimeOffset.UtcNow;

        await repository.AddAsync(new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Low priority",
            Priority = TodoPriority.Low,
            IsCompleted = false,
            CreatedAtUtc = older,
            UpdatedAtUtc = older
        });

        await repository.AddAsync(new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "High priority newer",
            Priority = TodoPriority.High,
            IsCompleted = false,
            CreatedAtUtc = newer,
            UpdatedAtUtc = newer
        });

        await repository.AddAsync(new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "High priority older",
            Priority = TodoPriority.High,
            IsCompleted = false,
            CreatedAtUtc = older,
            UpdatedAtUtc = older
        });

        // Act
        var items = await repository.GetAllAsync();

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Equal("High priority older", items[0].Title);
        Assert.Equal("High priority newer", items[1].Title);
        Assert.Equal("Low priority", items[2].Title);
    }

    [Fact]
    public async Task AddRangeAsync_ValidItems_PersistsAllItems()
    {
        // Arrange
        var repository = CreateEmptyRepository();
        var now = DateTimeOffset.UtcNow;

        var items = Enumerable.Range(0, 4)
            .Select(i => new TodoItem
            {
                Id = Guid.NewGuid(),
                Title = "Weekly task",
                Priority = TodoPriority.Normal,
                IsCompleted = false,
                CreatedAtUtc = now,
                DueAtUtc = now.AddDays(i * 7),
                UpdatedAtUtc = now
            })
            .ToList();

        // Act
        var created = await repository.AddRangeAsync(items);

        // Assert
        Assert.Equal(4, created.Count);
        Assert.Equal(4, await repository.CountAsync());

        foreach (var item in created)
        {
            var stored = await repository.GetByIdAsync(item.Id);

            Assert.NotNull(stored);
            Assert.Equal(item.DueAtUtc, stored.DueAtUtc);
        }
    }

    [Fact]
    public async Task AddRangeAsync_DuplicateExistingId_PersistsNoNewItems()
    {
        // Arrange
        var repository = CreateEmptyRepository();
        var now = DateTimeOffset.UtcNow;

        var existing = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Existing task",
            Priority = TodoPriority.Normal,
            IsCompleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.AddAsync(existing);

        var newItem = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "New task",
            Priority = TodoPriority.Normal,
            IsCompleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var duplicate = new TodoItem
        {
            Id = existing.Id,
            Title = "Duplicate task",
            Priority = TodoPriority.Normal,
            IsCompleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.AddRangeAsync([newItem, duplicate]));

        Assert.Equal(1, await repository.CountAsync());
        Assert.Null(await repository.GetByIdAsync(newItem.Id));

        var stored = await repository.GetByIdAsync(existing.Id);
        Assert.NotNull(stored);
        Assert.Equal("Existing task", stored.Title);
    }

    [Fact]
    public async Task AddRangeAsync_DuplicateIdsWithinBatch_PersistsNothing()
    {
        // Arrange
        var repository = CreateEmptyRepository();
        var now = DateTimeOffset.UtcNow;
        var duplicateId = Guid.NewGuid();

        var items = new List<TodoItem>
        {
            new()
            {
                Id = duplicateId,
                Title = "First task",
                Priority = TodoPriority.Normal,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new()
            {
                Id = duplicateId,
                Title = "Second task",
                Priority = TodoPriority.Normal,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.AddRangeAsync(items));

        Assert.Equal(0, await repository.CountAsync());
        Assert.Null(await repository.GetByIdAsync(duplicateId));
    }

}
