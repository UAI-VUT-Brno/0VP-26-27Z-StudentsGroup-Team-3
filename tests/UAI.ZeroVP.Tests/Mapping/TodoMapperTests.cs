using UAI.ZeroVP.API.Mapping;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Tests.Mapping;

public class TodoMapperTests
{
    [Fact]
    public void ToEntity_TrimsTextAndInitializesNewTodo()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        var request = new CreateTodoItemRequest
        {
            Title = "  Test task  ",
            Description = "  Test description  ",
            Priority = TodoPriority.High
        };

        // Act
        var entity = request.ToEntity(now);

        // Assert
        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal("Test task", entity.Title);
        Assert.Equal("Test description", entity.Description);
        Assert.Equal(TodoPriority.High, entity.Priority);
        Assert.False(entity.IsCompleted);
        Assert.Equal(now, entity.CreatedAtUtc);
        Assert.Null(entity.CompletedAtUtc);
        Assert.Equal(now, entity.UpdatedAtUtc);
    }

    [Fact]
    public void ToEntity_ConvertsWhitespaceDescriptionToNull()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        var request = new CreateTodoItemRequest
        {
            Title = "Test task",
            Description = "   ",
            Priority = TodoPriority.Normal
        };

        // Act
        var entity = request.ToEntity(now);

        // Assert
        Assert.Null(entity.Description);
    }

    [Fact]
    public void SetCompletionState_CompletingTodoSetsCompletedAtUtc()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 10, 6, 13, 0, 0, TimeSpan.Zero);

        var entity = new UAI.ZeroVP.Data.Entities.TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Test task",
            Priority = TodoPriority.Normal,
            IsCompleted = false,
            CreatedAtUtc = now.AddHours(-1),
            UpdatedAtUtc = now.AddHours(-1),
            CompletedAtUtc = null
        };

        // Act
        entity.SetCompletionState(true, now);

        // Assert
        Assert.True(entity.IsCompleted);
        Assert.Equal(now, entity.CompletedAtUtc);
        Assert.Equal(now, entity.UpdatedAtUtc);
    }

    [Fact]
    public void SetCompletionState_ReopeningTodoClearsCompletedAtUtc()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 10, 6, 14, 0, 0, TimeSpan.Zero);

        var entity = new UAI.ZeroVP.Data.Entities.TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Completed task",
            Priority = TodoPriority.Normal,
            IsCompleted = true,
            CreatedAtUtc = now.AddHours(-2),
            UpdatedAtUtc = now.AddHours(-1),
            CompletedAtUtc = now.AddHours(-1)
        };

        // Act
        entity.SetCompletionState(false, now);

        // Assert
        Assert.False(entity.IsCompleted);
        Assert.Null(entity.CompletedAtUtc);
        Assert.Equal(now, entity.UpdatedAtUtc);
    }

    [Fact]
    public void ApplyTo_UpdatesTodoEntity()
    {
        // Arrange
        var originalTime = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        var updateTime = new DateTimeOffset(2026, 10, 6, 15, 0, 0, TimeSpan.Zero);

        var entity = new UAI.ZeroVP.Data.Entities.TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Old title",
            Description = "Old description",
            Priority = TodoPriority.Low,
            IsCompleted = false,
            CreatedAtUtc = originalTime,
            UpdatedAtUtc = originalTime,
            CompletedAtUtc = null
        };

        var request = new UpdateTodoItemRequest
        {
            Title = "  New title  ",
            Description = "  New description  ",
            Priority = TodoPriority.High,
            IsCompleted = true
        };

        // Act
        request.ApplyTo(entity, updateTime);

        // Assert
        Assert.Equal("New title", entity.Title);
        Assert.Equal("New description", entity.Description);
        Assert.Equal(TodoPriority.High, entity.Priority);
        Assert.True(entity.IsCompleted);
        Assert.Equal(updateTime, entity.CompletedAtUtc);
        Assert.Equal(updateTime, entity.UpdatedAtUtc);
    }
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("karel", "karel")]
    [InlineData("  karel  ", "karel")]
    public void NormalizeUserId_TrimsAndConvertsEmptyToNull(string? input, string? expected)
    {
        Assert.Equal(expected, TodoMapper.NormalizeUserId(input));
    }

    [Fact]
    public void ToEntity_MapsNormalizedUserId()
    {
        var request = new CreateTodoItemRequest
        {
            Title = "Task",
            UserId = "  karel  "
        };

        var entity = request.ToEntity(DateTimeOffset.UtcNow);

        Assert.Equal("karel", entity.UserId);
    }
}
