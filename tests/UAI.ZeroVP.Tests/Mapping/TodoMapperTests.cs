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
}