
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using UAI.ZeroVP.API.Controllers;
using UAI.ZeroVP.Data.InMemory;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Tests.Controllers;

public class RecurringTodoTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(52)]
    public async Task Repeat_ValidRequest_CreatesCorrectWeeklyOccurrences(int repeatCount)
    {
        // Arrange
        var repository = new InMemoryTodoRepository(
            new InMemoryTodoOptions
            {
                SeedSampleData = false
            });

        var controller = new TodosController(
            repository,
            TimeProvider.System,
            NullLogger<TodosController>.Instance);

        var firstDueAtUtc = new DateTimeOffset(
            2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

        var request = new CreateRecurringTodoRequest
        {
            Title = "  Weekly meeting  ",
            Description = "  Team sync  ",
            Priority = TodoPriority.High,
            FirstDueAtUtc = firstDueAtUtc,
            RepeatCount = repeatCount
        };

        // Act
        var response = await controller.Repeat(
            request,
            CancellationToken.None);

        // Assert HTTP response
        var created = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);

        var todos = Assert.IsAssignableFrom<IReadOnlyList<TodoItemDto>>(
            created.Value);

        Assert.Equal(repeatCount + 1, todos.Count);
        Assert.Equal(repeatCount + 1, todos.Select(x => x.Id).Distinct().Count());

        for (var i = 0; i < todos.Count; i++)
        {
            var todo = todos[i];

            Assert.NotEqual(Guid.Empty, todo.Id);
            Assert.Equal("Weekly meeting", todo.Title);
            Assert.Equal("Team sync", todo.Description);
            Assert.Equal(TodoPriority.High, todo.Priority);
            Assert.False(todo.IsCompleted);
            Assert.Null(todo.CompletedAtUtc);
            Assert.Equal(firstDueAtUtc.AddDays(i * 7), todo.DueAtUtc);

            // Every occurrence must be persisted independently.
            var stored = await repository.GetByIdAsync(todo.Id);

            Assert.NotNull(stored);
            Assert.Equal(todo.DueAtUtc, stored.DueAtUtc);
        }

        Assert.Equal(repeatCount + 1, await repository.CountAsync());
    }
}
